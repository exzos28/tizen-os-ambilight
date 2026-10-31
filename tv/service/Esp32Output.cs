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

        public Esp32Output(Discovery discovery, Action<string, string> log)
        {
            _discovery = discovery;
            _log = log;
        }

        public void Send(EdgeFrame frame)
        {
            var hosts = _discovery.Hosts;
            if (hosts.Length == 0) return;

            var packet = new byte[2 + frame.Rgb.Length];
            packet[0] = (byte)frame.H;
            packet[1] = (byte)frame.V;
            Buffer.BlockCopy(frame.Rgb, 0, packet, 2, frame.Rgb.Length);

            foreach (var host in hosts)
            {
                try { _udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(host), Config.UdpPort)); }
                catch (Exception ex)
                {
                    if (_failures++ % 30 == 0)
                        _log("UdpSendError", $"target={host} failures={_failures} err={ex.Message}");
                }
            }
        }

        public void Dispose() => _udp.Dispose();
    }
}
