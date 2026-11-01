using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using static System.FormattableString;

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
        private long _framesSent;                                  // frames that reached at least one socket send
        private volatile string _lastError;
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

            bool sent = false;
            foreach (var endpoint in _endpoints)
            {
                try { _udp.Send(_packet, _packet.Length, endpoint); sent = true; }
                catch (ObjectDisposedException) { return; }   // turned off by set-outputs while this frame was in flight
                catch (Exception ex)
                {
                    _lastError = $"{endpoint.Address}: {ex.Message}";
                    int failures = Interlocked.Increment(ref _failures);
                    if (failures % 30 == 1)
                        _log("UdpSendError", Invariant($"target={endpoint.Address} failures={failures} err={ex.Message}"));
                }
            }
            if (sent) Interlocked.Increment(ref _framesSent);
        }

        // Status panel section. UDP has no replies, so "sent" means handed to the network,
        // not received by the controller. Hosts include tools/server.py instances.
        public void WriteStatus(Utf8JsonWriter w)
        {
            w.WriteStartObject();
            w.WriteBoolean("enabled", true);
            w.WriteStartArray("hosts");
            foreach (var host in _discovery.Hosts) w.WriteStringValue(host);
            w.WriteEndArray();
            w.WriteNumber("framesSent", Interlocked.Read(ref _framesSent));
            w.WriteNumber("sendErrors", Volatile.Read(ref _failures));
            w.WriteString("lastError", _lastError);
            w.WriteEndObject();
        }

        // Safe while Send runs on the capture thread: that frame is then dropped.
        public void Dispose() => _udp.Dispose();
    }
}
