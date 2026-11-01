using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static System.FormattableString;

namespace Service
{
    // Output settings, read from res/ambilight.json (bundled into the package at build time).
    // A missing file means the defaults below: ESP32 only, exactly the behaviour before
    // outputs became configurable. Invalid sections are disabled and reported in Warnings.
    sealed class OutputConfig
    {
        public bool Esp32Enabled { get; private set; } = true;
        public HyperHdrSettings HyperHdr { get; private set; } = new HyperHdrSettings();
        public List<string> Warnings { get; } = new List<string>();

        public static OutputConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var defaults = new OutputConfig();
                defaults.Warnings.Add($"{path} not found — using defaults (ESP32 only)");
                return defaults;
            }
            return Parse(File.ReadAllText(path));
        }

        public static OutputConfig Parse(string json)
        {
            var cfg = new OutputConfig();
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException ex)
            {
                cfg.Warnings.Add($"config is not valid JSON ({ex.Message}) — using defaults (ESP32 only)");
                return cfg;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    cfg.Warnings.Add("config root must be an object — using defaults (ESP32 only)");
                    return cfg;
                }

                if (root.TryGetProperty("esp32", out var esp32))
                {
                    if (esp32.ValueKind == JsonValueKind.Object)
                        cfg.Esp32Enabled = GetBool(esp32, "enabled", true, "esp32", cfg.Warnings);
                    else
                        cfg.Warnings.Add(@"esp32 must be an object, e.g. ""esp32"": { ""enabled"": false } — ESP32 output left enabled");
                }

                if (root.TryGetProperty("hyperhdr", out var hyper))
                    cfg.HyperHdr = HyperHdrSettings.Parse(hyper, cfg.Warnings);
            }
            return cfg;
        }

        internal static bool GetBool(JsonElement obj, string name, bool fallback, string section, List<string> warnings)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return fallback;
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            warnings.Add($"{section}.{name} must be true or false");
            return fallback;
        }

        internal static int GetInt(JsonElement obj, string name, int fallback, int min, int max, string section, List<string> warnings, ref bool valid)
        {
            if (!obj.TryGetProperty(name, out var v)) return fallback;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= min && n <= max) return n;
            warnings.Add(Invariant($"{section}.{name} must be an integer {min}..{max}"));
            valid = false;
            return fallback;
        }
    }

    sealed class HyperHdrSettings
    {
        public bool Enabled { get; private set; }
        public string Host { get; private set; } = "";
        public int Port { get; private set; } = 19400;
        public int Priority { get; private set; } = 150;
        public int ImageWidth { get; private set; } = 64;
        public int ImageHeight { get; private set; } = 36;
        public string Origin => "Tizen Ambilight";

        internal static HyperHdrSettings Parse(JsonElement obj, List<string> warnings)
        {
            var s = new HyperHdrSettings();
            if (obj.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("hyperhdr must be an object — HyperHDR output disabled");
                return s;
            }

            bool valid = true;
            bool enabled = OutputConfig.GetBool(obj, "enabled", false, "hyperhdr", warnings);

            if (obj.TryGetProperty("host", out var host))
            {
                if (host.ValueKind == JsonValueKind.String) s.Host = host.GetString().Trim();
                else { warnings.Add("hyperhdr.host must be a string"); valid = false; }
            }
            s.Port        = OutputConfig.GetInt(obj, "port",        s.Port,        1, 65535, "hyperhdr", warnings, ref valid);
            s.Priority    = OutputConfig.GetInt(obj, "priority",    s.Priority,    HyperHdrProtocol.MinPriority, HyperHdrProtocol.MaxPriority, "hyperhdr", warnings, ref valid);
            s.ImageWidth  = OutputConfig.GetInt(obj, "imageWidth",  s.ImageWidth,  EdgeImage.MinSize, EdgeImage.MaxSize, "hyperhdr", warnings, ref valid);
            s.ImageHeight = OutputConfig.GetInt(obj, "imageHeight", s.ImageHeight, EdgeImage.MinSize, EdgeImage.MaxSize, "hyperhdr", warnings, ref valid);

            if (enabled && string.IsNullOrEmpty(s.Host))
            {
                warnings.Add("hyperhdr.enabled is true but hyperhdr.host is empty");
                valid = false;
            }
            s.Enabled = enabled && valid;
            if (enabled && !valid) warnings.Add("HyperHDR output disabled because of the errors above");
            return s;
        }
    }
}
