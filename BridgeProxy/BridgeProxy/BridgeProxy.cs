using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace BridgeProxy
{
    class BridgeProxy
    {
        public ProxySettings ProxySettings { get; private set; }


        public ConcurrentBag<Socket> AdditionalSockets { get; set; } = new ConcurrentBag<Socket>();

        public ConcurrentBag<Socket> AdditionalSocketsReuse { get; set; } = new ConcurrentBag<Socket>();

        public Socket TwoWayConnectManager { get; set; }

        public TcpListener TwoWayConnectListener { get; set; }

        private ConcurrentQueue<SocketShell> _twoWayConnectManagerQueue = new ConcurrentQueue<SocketShell>();


        public BridgeProxy(ProxySettings proxySettings)
        {
            ProxySettings = proxySettings ?? throw new Exception("Настройки не заданы");
        }

        public void Start()
        {
            if (ProxySettings.ListenAddress != null)
                StartListen();

            if (ProxySettings.AdditionalListenAddress != null)
                AdditionalListen();

            if (ProxySettings.AdditionalListenAddressReuse != null)
                AdditionalListenReuse();

            if (ProxySettings.ConnectAddress != null)
                Connect();

            if (ProxySettings.TwoWayConnectListenAddress != null)
                ListenConnect();
        }

        private async void AdditionalListen()
        {
            var listener = new TcpListener(ProxySettings.AdditionalListenAddress);

            listener.Start();
            Console.WriteLine("Start additional listen on " + listener.LocalEndpoint);
            while (true)
            {
                var socket = await listener.AcceptSocketAsync();

                Console.WriteLine($"Additional Listener {listener.LocalEndpoint} accept client {socket.RemoteEndPoint}");

                AdditionalSockets.Add(socket);
            }
        }

        private async void AdditionalListenReuse()
        {
            var listener = new TcpListener(ProxySettings.AdditionalListenAddressReuse);

            listener.Start();
            Console.WriteLine("Start additional listen reuse on " + listener.LocalEndpoint);
            while (true)
            {
                var socket = await listener.AcceptSocketAsync();

                Console.WriteLine($"Additional Listener reuse {listener.LocalEndpoint} accept client {socket.RemoteEndPoint}");

                AdditionalSocketsReuse.Add(socket);
            }
        }

        private async void StartListen()
        {
            var listener = new TcpListener(ProxySettings.ListenAddress);

            listener.Start();

            Console.WriteLine("Start listen on " + listener.LocalEndpoint);

            while (true)
            {
                var socket = await listener.AcceptSocketAsync();
                Console.WriteLine($"Listener {listener.LocalEndpoint} accept client {socket.RemoteEndPoint}");
                CreateClient(socket);
            }
        }

        private async void ListenConnect()
        {
            TwoWayConnectListener = new TcpListener(ProxySettings.TwoWayConnectListenAddress);
            TwoWayConnectListener.Start();
            Console.WriteLine("Listen connect started");
            while (true)
            {
                var socket = await TwoWayConnectListener.AcceptSocketAsync();

                if (_twoWayConnectManagerQueue.TryPeek(out SocketShell shell) == false)
                {
                    if (TwoWayConnectManager != null)
                    {
                        if (TwoWayConnectManager?.Connected == true)
                        {
                            Console.WriteLine("2way connect manager disconnecting " + TwoWayConnectManager.RemoteEndPoint);
                            TwoWayConnectManager?.Disconnect(false);
                        }
                        Console.WriteLine("2way connect manager disposing " + TwoWayConnectManager.RemoteEndPoint);
                        TwoWayConnectManager?.Dispose();
                    }

                    TwoWayConnectManager = socket;
                    Console.WriteLine("2way connect manager created from " + TwoWayConnectManager.RemoteEndPoint);
                }
                else
                {
                    shell.Socket = socket;
                }
            }
        }


        private async void Connect()
        {
            while (true)
            {
                try
                {
                    Console.WriteLine($"2way manager connecting to {ProxySettings.ConnectAddress}");
                    var client = new TcpClient();
                    await client.ConnectAsync(ProxySettings.ConnectAddress.Address, ProxySettings.ConnectAddress.Port);
                    Console.WriteLine($"2way manager connected to {client.Client.RemoteEndPoint}");

                    TwoWayConnectManager = client.Client;

                    var buffer = new byte[TwoWayConnectManager.ReceiveBufferSize];
                    while (true)
                    {
                        var count = TwoWayConnectManager.Receive(buffer);
                        if (count == 0)
                            throw new Exception("Received 0 bytes");

                        Console.WriteLine($"Need {count} clients");
                        for (int i = 0; i < count; i++)
                        {
                            client = new TcpClient();
                            Console.WriteLine($"Create 1 client");
                            await client.ConnectAsync(ProxySettings.ConnectAddress.Address, ProxySettings.ConnectAddress.Port);
                            Console.WriteLine($"1 client Connected");
                            CreateClient(client.Client);
                            Console.WriteLine($"1 client Created");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("2way Connect error: " + ex);
                }
            }
        }

        private static void RemoveAllNot<T>(ConcurrentBag<T> source, Func<T, bool> predicate)
        {
            var dontRemoveSockets = source
                    .Where(predicate)
                    .ToList();
            source.Clear();
            foreach (var item in dontRemoveSockets)
            {
                source.Add(item);
            }
        }

        private BridgeClient CreateClient(Socket socket)
        {
            RemoveAllNot(AdditionalSockets, x => x.Connected);
            RemoveAllNot(AdditionalSocketsReuse, x => x.Connected);

            var client = new BridgeClient(socket, AdditionalSockets.ToList(), AdditionalSocketsReuse.ToList())
            {
                RedirectAddress = ProxySettings.RedirectAddress,
                AdditionalAddresses = ProxySettings.AdditionalAddresses ?? new List<IPEndPoint>(),
                MirrorMode = ProxySettings.MirrorMode,
                LogMode = ProxySettings.LogMode,
                LogFileNameFormat = ProxySettings.LogFileNameFormat
            };

            if (TwoWayConnectManager != null && TwoWayConnectListener != null)
            {
                try
                {
                    TwoWayConnectManager.Send(new byte[1]);
                    _twoWayConnectManagerQueue.Enqueue(new SocketShell());

                    bool res = false;
                    while ((res = _twoWayConnectManagerQueue.TryPeek(out SocketShell peekShell)) && peekShell.Socket == null) ;
                    if (res == false)
                        throw new Exception("TwoWayConnectManagerQueue was cleared");

                    if (_twoWayConnectManagerQueue.TryDequeue(out SocketShell dequeueShell) == false)
                        throw new Exception("TwoWayConnectManagerQueue was cleared");

                    var toSocket = dequeueShell.Socket;
                    client.Start(toSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TwoWayConnectManager.Send error " + ex);
                    client.Dispose();
                }
            }
            else if (ProxySettings.TwoWayConnectListenAddress != null)
            {
                client.Dispose();
            }
            else
            {
                client.Start(null);
            }

            return client;
        }

        class SocketShell
        {
            public Socket Socket { get; set; } = null;
        }
    }
}