using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Service
{
    // Why capture could not start. Library / Symbols mean the TV does not expose the
    // capture API at all; Hardware means the API is there but reported an unusable state.
    enum CaptureFailure { None, Library, Symbols, Hardware }

    // Capture state for the launcher's status panel (served by StatusPort). Written by the
    // capture thread and the retry loop, read on the main thread — every member locks.
    sealed class CaptureStatus
    {
        private readonly object _lock = new object();

        private string _state = "waiting";   // waiting | starting | running | failed
        private CaptureFailure _failure;
        private string _error;
        private int _failedAttempts;
        private long _retryAtTicks;

        private bool _hasHw;
        private int _hwRet, _blocks, _blockW, _blockH, _fullW, _fullH, _hwDelayMs, _delayMs;

        private long _frames;
        private long _lastFrameTicks;
        private int _frameMs;
        private int _reads, _badReads, _lastBadRet;
        private EdgeFrame _lastFrame;

        // Capture.Run is (re)initialising: forget the previous run.
        public void Starting()
        {
            lock (_lock)
            {
                _state = "starting";
                _failure = CaptureFailure.None;
                _error = null;
                _retryAtTicks = 0;
                _hasHw = false;
                _frames = 0;
                _lastFrame = null;
            }
        }

        public void Hardware(int ret, int blocks, int blockW, int blockH, int fullW, int fullH, int hwDelayMs, int delayMs)
        {
            lock (_lock)
            {
                _hasHw = true;
                _hwRet = ret; _blocks = blocks; _blockW = blockW; _blockH = blockH;
                _fullW = fullW; _fullH = fullH; _hwDelayMs = hwDelayMs; _delayMs = delayMs;
            }
        }

        public void Failed(CaptureFailure kind, string reason)
        {
            lock (_lock)
            {
                _state = "failed";
                _failure = kind;
                _error = reason;
                _failedAttempts++;
            }
        }

        public void RetryIn(int delayMs)
        {
            lock (_lock) _retryAtTicks = Environment.TickCount64 + delayMs;
        }

        // reads = samples read this frame, badReads = how many returned an error (they keep
        // the previous color), lastBadRet = the error code of the last failed read.
        public void Frame(EdgeFrame frame, int frameMs, int reads, int badReads, int lastBadRet)
        {
            lock (_lock)
            {
                _state = "running";
                _frames++;
                _lastFrameTicks = Environment.TickCount64;
                _frameMs = frameMs;
                _reads = reads;
                _badReads = badReads;
                if (badReads > 0) _lastBadRet = lastBadRet;
                _lastFrame = frame;
            }
        }

        public void WriteJson(Utf8JsonWriter w)
        {
            lock (_lock)
            {
                long now = Environment.TickCount64;
                w.WriteStartObject();
                w.WriteString("state", _state);
                w.WriteString("failure", _failure == CaptureFailure.None ? null : _failure.ToString().ToLowerInvariant());
                w.WriteString("error", _error);
                w.WriteNumber("failedAttempts", _failedAttempts);
                if (_state == "failed" && _retryAtTicks > 0) w.WriteNumber("retryInMs", Math.Max(0, _retryAtTicks - now));

                if (_hasHw)
                {
                    w.WriteStartObject("hw");
                    w.WriteNumber("ret", _hwRet);
                    w.WriteNumber("blocks", _blocks);
                    w.WriteNumber("blockW", _blockW);
                    w.WriteNumber("blockH", _blockH);
                    w.WriteNumber("fullW", _fullW);
                    w.WriteNumber("fullH", _fullH);
                    w.WriteNumber("hwDelayMs", _hwDelayMs);
                    w.WriteNumber("delayMs", _delayMs);
                    w.WriteEndObject();
                }

                w.WriteNumber("frames", _frames);
                if (_lastFrame != null)
                {
                    w.WriteNumber("frameMs", _frameMs);
                    w.WriteNumber("lastFrameAgoMs", now - _lastFrameTicks);
                    w.WriteNumber("reads", _reads);
                    w.WriteNumber("badReads", _badReads);
                    w.WriteNumber("lastBadRet", _lastBadRet);
                    w.WriteNumber("h", _lastFrame.H);
                    w.WriteNumber("v", _lastFrame.V);
                    w.WriteString("rgb", Convert.ToHexString(_lastFrame.Rgb));   // EdgeFrame order
                }
                w.WriteEndObject();
            }
        }
    }

    static class StatusJson
    {
        public static string Build(Action<Utf8JsonWriter> write)
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms)) write(w);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
