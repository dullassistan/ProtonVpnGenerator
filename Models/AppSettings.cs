namespace ProtonVpnGenerator.Models
{
    public class AppSettings
    {
        public string? CachedSessionJson { get; set; }
        public long SessionExpiresMs { get; set; }
        public string? CachedWgPrivateKeyBase64 { get; set; }
        public string? CachedCertJson { get; set; }

        public string SelectedClient { get; set; } = "AmneziaWG"; // AmneziaWG, WireSock, Clash
        public string ClashMode { get; set; } = "awg"; // awg, masque, hybrid
        public string SelectedPort { get; set; } = "51820";
        public string Mtu { get; set; } = "1420";

        // AWG 1.0
        public bool IsAwg1 { get; set; } = false;
        public int Awg1JunkPreset { get; set; } = 1; // 1: 3/1/3, 2: 30/10/30, 3: custom
        public string Awg1Jc { get; set; } = "128";
        public string Awg1Jmin { get; set; } = "1279";
        public string Awg1Jmax { get; set; } = "1280";

        // AWG 2.0
        public bool IsAwg2 { get; set; } = true;
        public string Awg2I1 { get; set; } = "";
        public string Awg2I2 { get; set; } = "";
        public string Awg2I3 { get; set; } = "";
        public string Awg2I4 { get; set; } = "";
        public string Awg2I5 { get; set; } = "";

        // WireSock
        public string WireSockId { get; set; } = "apteka.ru";
        public string WireSockIp { get; set; } = "quic";
        public string WireSockIb { get; set; } = "curl";

        // AWG 3.0
        public bool IsAwg3 { get; set; } = false;
        public string Awg3Cpa { get; set; } = "";
        public string Awg3Mha { get; set; } = "";
        public string Awg3Kt { get; set; } = "";
        public string Awg3Rat { get; set; } = "";
        public string Awg3Rkat { get; set; } = "";
        public string Awg3Rt { get; set; } = "";

        // AWG 3.1
        public bool IsAwg31 { get; set; } = false;
        public bool Awg31RandomTrailers { get; set; } = false;
        public bool Awg31DisableCookies { get; set; } = true;

        // General
        public bool ExcludeLan { get; set; } = false;
        public bool EnableIpv6 { get; set; } = false;
        public bool PersistentKeepaliveEnabled { get; set; } = false;
        public string PersistentKeepaliveValue { get; set; } = "25";

        public string SelectedCountryFilter { get; set; } = "all";
        public string? SelectedServerId { get; set; }

        public bool HasValidSession(long nowMs = 0)
        {
            if (nowMs <= 0) nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return !string.IsNullOrWhiteSpace(CachedSessionJson) && SessionExpiresMs > nowMs;
        }
    }
}
