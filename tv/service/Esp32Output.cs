using System;
using System.Net;
using System.Net.Sockets;

namespace Service
{
    // Streams frames to every discovered ESP32 over UDP (port Config.UdpPort).
    // Packet: [hCount][vCount][EdgeFrame.Rgb] — see docs/NETWORKING.md.
    //
    // UDP sends don't block, so this runs inline on the capture thread.
    sealed class Esp32Output : IDisposable
    {
        private readonly Discovery _discovery;
        private readonly Action<string, string> _log;
        private readonly UdpClient _udp = new UdpClient();
        private int _failures;
        private string[] _hosts = Array.Empty<string>();          // snapshot the endpoints were built from
        private IPEndPoint[] _endpoints = Array.Empty<IPEndPoint>();
        private byte[] _packet = Array.Empty<byte>();

        public Esp32Output(Discovery discovery, Action<string, string> log)
        {
            _discovery = discovery;
            _log = log;
        }

        public void Send(EdgeFrame frame)
        {
            var hosts = _discovery.Hosts;
            if (hosts.Length == 0) return;
            if (!ReferenceEquals(hosts, _hosts))   // Discovery publishes a new array only on change
            {
                _endpoints = Array.ConvertAll(hosts, h => new IPEndPoint(IPAddress.Parse(h), Config.UdpPort));
                _hosts = hosts;
            }

            if (_packet.Length != 2 + frame.Rgb.Length) _packet = new byte[2 + frame.Rgb.Length];
            _packet[0] = (byte)frame.H;
            _packet[1] = (byte)frame.V;
            Buffer.BlockCopy(frame.Rgb, 0, _packet, 2, frame.Rgb.Length);

            foreach (var endpoint in _endpoints)
            {
                try { _udp.Send(_packet, _packet.Length, endpoint); }
                catch (Exception ex)
                {
                    if (_failures++ % 30 == 0)
                        _log("UdpSendError", $"target={endpoint.Address} failures={_failures} err={ex.Message}");
                }
            }
        }

        public void Dispose() => _udp.Dispose();
    }
}
