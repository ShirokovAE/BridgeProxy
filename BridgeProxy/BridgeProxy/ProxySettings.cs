using System.Net;

namespace BridgeProxy
{
    public class ProxySettings
    {
        public IPEndPoint? ListenAddress { get; set; }

        public IPEndPoint? AdditionalListenAddress { get; set; }

        public IPEndPoint? AdditionalListenAddressReuse { get; set; }

        public IPEndPoint? RedirectAddress { get; set; }

        public IPEndPoint? ConnectAddress { get; set; }

        public IPEndPoint? TwoWayConnectListenAddress { get; set; }

        public List<IPEndPoint>? AdditionalAddresses { get; set; }

        public int AdditionalConnectTryCount { get; set; }

        /// <summary>
        /// Полученные данные отправляются обратно
        /// </summary>
        public bool MirrorMode { get; set; }

        /// <summary>
        /// Полученные данные записываются в файл
        /// </summary>
        public bool LogMode { get; set; }

        /// <summary>
        /// Формат имени файла, если включен <see cref="LogMode"/>
        /// </summary>
        public string? LogFileNameFormat { get; set; }


        public static implicit operator ProxySettings(ProxySettingsJson proxySettingsJson)
        {
            IPEndPoint convertFromString(string address)
            {
                var data = address?.Split(':');
                if (data?.Length != 2)
                    return null;

                return new IPEndPoint(IPAddress.Parse(data[0]), int.Parse(data[1]));
            }

            return new ProxySettings
            {
                AdditionalAddresses = proxySettingsJson.AdditionalAddresses
                    ?.Select(x => convertFromString(x))
                    .ToList(),
                AdditionalConnectTryCount = proxySettingsJson.AdditionalConnectTryCount ?? 1,
                AdditionalListenAddress = convertFromString(proxySettingsJson.AdditionalListenAddress),
                AdditionalListenAddressReuse = convertFromString(proxySettingsJson.AdditionalListenAddressReuse),
                ConnectAddress = convertFromString(proxySettingsJson.ConnectAddress),
                ListenAddress = convertFromString(proxySettingsJson.ListenAddress),
                MirrorMode = proxySettingsJson.MirrorMode,
                LogMode = proxySettingsJson.LogMode,
                LogFileNameFormat = proxySettingsJson.LogFileNameFormat,
                RedirectAddress = convertFromString(proxySettingsJson.RedirectAddress),
                TwoWayConnectListenAddress = convertFromString(proxySettingsJson.TwoWayConnectListenAddress)
            };
        }
    }
}
