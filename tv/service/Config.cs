namespace Service
{
    static class Config
    {
        public const int HttpPort = 9000;
        public const int UdpPort = 9001;
        public const int DiscoveryPort = 9003;
        public const string DiscoveryMagic = "AMBILIGHT_DISCOVER";
        public const int DiscoveryTimeoutMs = 2000;
        public const int DiscoveryMaxRetries = 30;

        // Edge sample points: h per top/bottom, v per left/right
        public const int TargetCaptureW = 4;
        public const int TargetCaptureH = 3;

        // Delay (ms) between set_position and get_pixel.
        // HW reports 20ms. Lower values = faster but may glitch.
        // Set to -1 to use HW-reported delay.
        // HW needs exactly 20ms to measure. Confirmed by diagnostics:
        // <20ms returns garbage, 20ms+ returns correct values.
        public const int CaptureDelayMs = 20;
    }
}
