namespace Service
{
    static class Config
    {
        public const int HttpPort = 9000;
        public const int UdpPort = 9001;
        public const int DiscoveryPort = 9003;
        public const string DiscoveryMagic = "AMBILIGHT_DISCOVER";
        public const int DiscoveryTimeoutMs = 2000;
        // While no host is known: quick attempts (one per DiscoveryTimeoutMs), then one
        // per DiscoveryRetryDelayMs. Once hosts are known: re-probe every DiscoveryRefreshMs
        // to pick up new controllers and IP changes. Discovery runs in the background and
        // never blocks capture.
        public const int DiscoveryMaxRetries = 30;
        public const int DiscoveryRetryDelayMs = 10_000;
        public const int DiscoveryRefreshMs = 30_000;

        // Output settings (ESP32 on/off, HyperHDR target) — see OutputConfig.cs.
        // Bundled into the package from tv/service/res/; edit before building.
        public const string OutputConfigFile = "ambilight.json";

        // Edge sample points: h per top/bottom, v per left/right
        public const int TargetCaptureW = 4;
        public const int TargetCaptureH = 3;

        // How many pixels to inset from each screen edge when placing sample points.
        // 0 = sample right at the edge; 250 = sample 250 px inward from each side.
        public const int EdgeInsetPx = 250;

        // Delay (ms) between set_position and get_pixel.
        // HW reports 20ms. Lower values = faster but may glitch.
        // Set to -1 to use HW-reported delay.
        // HW needs exactly 20ms to measure. Confirmed by diagnostics:
        // <20ms returns garbage, 20ms+ returns correct values.
        public const int CaptureDelayMs = 20;
    }
}
