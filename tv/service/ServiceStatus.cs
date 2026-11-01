using System;
using System.Collections.Generic;
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
        private bool _paused;                // reported as state "paused", whatever _state is
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

        // Paused by the launcher (CaptureLoop.Pause): the status says "paused" until resumed,
        // even while the stopped run's last state (running, failed) is still in place.
        public void SetPaused(bool paused)
        {
            lock (_lock) _paused = paused;
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
                string state = _paused ? "paused" : _state;
                w.WriteStartObject();
                w.WriteString("state", state);
                w.WriteString("failure", _failure == CaptureFailure.None ? null : _failure.ToString().ToLowerInvariant());
                w.WriteString("error", _error);
                w.WriteNumber("failedAttempts", _failedAttempts);
                if (state == "failed" && _retryAtTicks > 0) w.WriteNumber("retryInMs", Math.Max(0, _retryAtTicks - now));

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
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        // The whole reply for the launcher's status panel (StatusPort). version is the package
        // version (null if it could not be read); a null output writer means that output is
        // off. outputs is the output configuration in effect (the "outputs" section); its
        // Warnings are the configWarnings.
        public static void WriteService(Utf8JsonWriter w, string version, CaptureStatus capture,
            Action<Utf8JsonWriter> esp32, Action<Utf8JsonWriter> hyperHdr, OutputConfig outputs)
        {
            w.WriteStartObject();
            w.WriteString("version", version);
            w.WritePropertyName("capture");
            capture.WriteJson(w);
            w.WritePropertyName("esp32");
            WriteOutput(w, esp32);
            w.WritePropertyName("hyperhdr");
            WriteOutput(w, hyperHdr);
            w.WritePropertyName("outputs");
            outputs.WriteJson(w);
            w.WriteStartArray("configWarnings");
            foreach (var warning in outputs.Warnings) w.WriteStringValue(warning);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteOutput(Utf8JsonWriter w, Action<Utf8JsonWriter> write)
        {
            if (write != null) { write(w); return; }
            w.WriteStartObject();
            w.WriteBoolean("enabled", false);
            w.WriteEndObject();
        }
    }
}
