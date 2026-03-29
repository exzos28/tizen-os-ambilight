namespace Service
{
    static class Config
    {
        // HTTP port for logging events (heartbeat, stats, errors)
        public const int HttpPort = 9000;

        // UDP port for sending edge LED color data
        public const int UdpPort = 9001;

        // UDP port for sending full debug frame (raw RGB image)
        public const int DebugPort = 9002;

        // UDP port used for broadcast discovery of the server
        public const int DiscoveryPort = 9003;

        // Magic string sent/expected during discovery handshake
        public const string DiscoveryMagic = "AMBILIGHT_DISCOVER";

        // How long to wait for a discovery response before retrying (ms)
        public const int DiscoveryTimeoutMs = 2000;

        // How many discovery attempts before giving up
        public const int DiscoveryMaxRetries = 30;

        // Desired capture width in pixels. The actual value will be rounded up
        // to the nearest exact divisor of the screen width to avoid black edges.
        // E.g. for a 1920px screen with target 73, the actual capture will be 80 (1920/24).
        public const int TargetCaptureW = 73;

        // Desired capture height in pixels. Same rounding logic as width.
        // E.g. for a 1080px screen with target 42, the actual capture will be 45 (1080/24).
        public const int TargetCaptureH = 42;

        // Maximum capture framerate. Frames are throttled to this limit.
        public const int MaxFps = 60;

        // How often to report performance stats (every N frames)
        public const int StatsEvery = 100;
    }
}
