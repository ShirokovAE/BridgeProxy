using System.Net;
using System.Net.Sockets;

namespace BridgeProxy
{
    public class BridgeClient : IDisposable
    {
        private static string _defaultLogFileName = "{0:yyyy_MM_dd_HH_mm}_{1}.log.bin";

        private Socket _fromSocket;
        private List<Socket> _additionalSockets;
        private List<Socket> _additionalSocketsReuse;

        private Socket _toSocket;
        private Dictionary<IPEndPoint, Socket> _addSockets = new Dictionary<IPEndPoint, Socket>();

        private DateTime _instanceCreateTime = DateTime.Now;
        private string _intstanceData;

        public IPEndPoint RedirectAddress { get; set; }

        public List<IPEndPoint> AdditionalAddresses { get; set; } = new List<IPEndPoint>();

        public bool MirrorMode { get; set; }

        public bool LogMode { get; set; }

        public string LogFileNameFormat { get; set; }

        public bool Disposed { get; set; }

        public event EventHandler OnDisposed;

        public BridgeClient(Socket socket, List<Socket> additionalSockets, List<Socket> additionalSocketsReuse)
        {
            _fromSocket = socket;
            _additionalSockets = additionalSockets;
            _additionalSocketsReuse = additionalSocketsReuse;

            var separator = '_';
            var data = new object[]
            {
                socket.RemoteEndPoint,
                socket.LocalEndPoint,
                socket.GetHashCode(),
                GetHashCode()
            }.Select(x => x?.ToString().Replace('.', separator).Replace(':', separator));
            _intstanceData = string.Join("_", data);
        }

        public async void Start(Socket toSocket)
        {
            _toSocket = toSocket;
            if (_toSocket != null)
            {
                StartReceive(_toSocket);
            }

            var byteBuffer = new byte[_fromSocket.ReceiveBufferSize];
            var buffer = new ArraySegment<byte>(byteBuffer);

            foreach (var item in AdditionalAddresses)
            {
                _addSockets.Add(item, null);
            }

            try
            {
                while (Disposed == false)
                {
                    int count;
                    try
                    {
                        count = await _fromSocket.ReceiveAsync(buffer, SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        if (_fromSocket != null)
                            Console.WriteLine($"Disconnect on receive > fromSocket {_fromSocket.RemoteEndPoint} ({_fromSocket.LocalEndPoint}) with {ex}");
                        else
                            Console.WriteLine("Disconnect on receive > fromSocket with " + ex);
                        Dispose();
                        break;
                    }
                    if (count == 0)
                    {
                        if (_fromSocket != null)
                            Console.WriteLine($"Disconnect fromSocket {_fromSocket.RemoteEndPoint} ({_fromSocket.LocalEndPoint})");
                        else
                            Console.WriteLine("Disconnect > fromSocket");
                        Dispose();
                        break;
                    }

                    Console.WriteLine($"{_fromSocket.RemoteEndPoint} -> {_fromSocket.LocalEndPoint} data({count})");

                    if (RedirectAddress != null)
                    {
                        if (_toSocket == null)
                        {
                            var client = new TcpClient();

                            try
                            {
                                await client.ConnectAsync(RedirectAddress.Address, RedirectAddress.Port);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error on connect > toSocket " + ex);
                                Dispose();
                                break;
                            }
                            _toSocket = client.Client;
                            StartReceive(_toSocket);
                        }
                    }

                    if (_toSocket != null)
                    {
                        try
                        {
                            await _toSocket.SendAsync(buffer.Slice(0, count), SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error on send > _toSocket " + ex);
                            Dispose();
                            break;
                        }
                    }

                    if (MirrorMode)
                    {
                        try
                        {
                            await _fromSocket.SendAsync(buffer.Slice(0, count), SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error on send with mirrorMode > fromSocket " + ex);
                            Dispose();
                            break;
                        }
                    }

                    if (LogMode)
                    {
                        try
                        {
                            var logFileName = string.Format(LogFileNameFormat ?? _defaultLogFileName, _instanceCreateTime, _intstanceData);
                            new FileInfo(logFileName).Directory.Create();
                            using (var file = File.Open(logFileName, FileMode.Append))
                            {
                                await file.WriteAsync(buffer.Slice(0, count));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error on write to file " + ex);
                            Dispose();
                            break;
                        }
                    }

                    foreach (var address in _addSockets.Keys)
                    {
                        try
                        {
                            if (_addSockets[address] == null)
                            {
                                var client = new TcpClient();
                                await client.ConnectAsync(address.Address, address.Port);
                                _addSockets[address] = client.Client;
                            }

                            await _addSockets[address].SendAsync(buffer.Slice(0, count), SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error on send > addSocket {address}: {ex}");
                            if (_addSockets[address] != null)
                            {
                                if (_addSockets[address].Connected)
                                    _addSockets[address].Disconnect(false);
                                _addSockets[address].Dispose();
                                _addSockets[address] = null;
                            }
                        }
                    }

                    foreach (var item in _additionalSockets.Concat(_additionalSocketsReuse).ToList())
                    {
                        try
                        {
                            await item.SendAsync(buffer.Slice(0, count), SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Additional socket was disconnected with {ex}");
                            _additionalSockets.Remove(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Dispose();
            }
        }

        private async void StartReceive(Socket toSocket)
        {
            var buffer = new ArraySegment<byte>(new byte[toSocket.ReceiveBufferSize]);
            try
            {
                while (Disposed == false)
                {
                    var count = await toSocket.ReceiveAsync(buffer, SocketFlags.None);
                    if (count == 0)
                    {
                        Console.WriteLine($"Disconnect toSocket {_fromSocket.RemoteEndPoint} ({_fromSocket.LocalEndPoint})");
                        Dispose();
                        break;
                    }

                    Console.WriteLine($"{toSocket.RemoteEndPoint} -> {toSocket.LocalEndPoint} -> {_fromSocket.RemoteEndPoint} data({count})");

                    await _fromSocket.SendAsync(buffer.Slice(0, count), SocketFlags.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void Dispose()
        {
            if (_additionalSockets != null)
            {
                foreach (var item in _additionalSockets)
                {
                    try
                    {
                        if (item.Connected)
                            item.Disconnect(false);
                    }
                    catch { }
                    item.Dispose();
                }
                _additionalSockets.Clear();
                _additionalSockets = null;
            }

            if (_addSockets != null)
            {
                foreach (var item in _addSockets)
                {
                    if (item.Value != null)
                    {
                        try
                        {
                            if (item.Value.Connected)
                                item.Value.Disconnect(false);
                        }
                        catch { }
                        item.Value.Dispose();
                    }
                }
                _addSockets.Clear();
                _addSockets = null;
            }

            if (_toSocket != null)
            {
                try
                {
                    if (_toSocket.Connected)
                        _toSocket.Disconnect(false);
                }
                catch { }
                _toSocket.Dispose();
                _toSocket = null;
            }

            if (_fromSocket != null)
            {
                try
                {
                    if (_fromSocket.Connected)
                        _fromSocket.Disconnect(false);
                }
                catch { }
                _fromSocket.Dispose();
                _fromSocket = null;
            }

            Disposed = true;

            OnDisposed?.Invoke(this, EventArgs.Empty);
        }
    }
}