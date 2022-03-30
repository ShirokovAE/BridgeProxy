namespace BridgeProxy
{
    public class ProxySettingsJson
    {
        public string? ListenAddress { get; set; }

        public string? AdditionalListenAddress { get; set; }

        public string? AdditionalListenAddressReuse { get; set; }

        public string? RedirectAddress { get; set; }

        public string? ConnectAddress { get; set; }

        public string? TwoWayConnectListenAddress { get; set; }

        public List<string>? AdditionalAddresses { get; set; }

        public int? AdditionalConnectTryCount { get; set; }

        public bool MirrorMode { get; set; }

        public bool LogMode { get; set; }

        public string? LogFileNameFormat { get; set; }
    }
}