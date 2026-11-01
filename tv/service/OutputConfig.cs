using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using static System.FormattableString;

namespace Service
{
    // Where the settings in effect came from: res/ambilight.json in the package, or the file
    // the launcher's set-outputs command saved in the app's data directory (merged over
    // res/ambilight.json, see LoadEffective).
    enum OutputSource { Package, Saved }

    // Output settings (ESP32 on/off, HyperHDR target).
    //
    //   - res/ambilight.json, bundled into the package (Load / Parse): lenient. A missing file
    //     means the defaults below — ESP32 only, the behaviour before outputs became
    //     configurable. Invalid sections are disabled and reported in Warnings.
    //   - the set-outputs command (ParseCommand) and the file it saves (Save / LoadEffective):
    //     strict. Anything invalid rejects the whole thing — the command answers ok:false, a
    //     bad saved file is ignored in favour of res/ambilight.json, with a warning.
    //
    // Same keys, ranges and defaults in both. The strict form takes no unknown keys and needs
    // every section and key except hyperhdr.priority. It holds only what set-outputs sets
    // (esp32.enabled, hyperhdr.enabled / host / port / priority); everything else, like the
    // HyperHDR image size, always comes from res/ambilight.json, so a newer package's values
    // apply even after set-outputs. Saved files from older versions also carry
    // hyperhdr.imageWidth / imageHeight: those keys are accepted and ignored.
    // Tizen-independent, so it is unit-tested in CI.
    sealed class OutputConfig
    {
        public bool Esp32Enabled { get; private set; } = true;
        public HyperHdrSettings HyperHdr { get; private set; } = new HyperHdrSettings();
        public List<string> Warnings { get; } = new List<string>();
        public OutputSource Source { get; private set; } = OutputSource.Package;

        // The problems of res/ambilight.json that affect the HyperHDR image size, worded for
        // LoadEffective's merged result: the image size is taken from the package even when
        // everything else comes from the saved file.
        private readonly List<string> _imageSizeWarnings = new List<string>();

        public static OutputConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var defaults = new OutputConfig();
                defaults.Warnings.Add($"{path} not found — using defaults (ESP32 only)");
                defaults._imageSizeWarnings.Add($"{path} not found — using the default HyperHDR image size");
                return defaults;
            }
            return Parse(File.ReadAllText(path));
        }

        // The settings to start with: res/ambilight.json (packagePath) with the saved ones
        // (savedPath, written by Save) over it if that file exists and is valid. The saved file
        // sets esp32.enabled and the HyperHDR target (Source = Saved); the image size always
        // comes from the package, and the package's problems with it are the warnings. A
        // saved file that can't be read or is invalid is ignored and reported as the first
        // warning; the result is then res/ambilight.json alone, with its warnings.
        public static OutputConfig LoadEffective(string packagePath, string savedPath)
        {
            var package = Load(packagePath);
            if (!File.Exists(savedPath)) return package;

            string problem;
            try
            {
                string error = ParseStrict(File.ReadAllText(savedPath), true, package.HyperHdr, out var saved);
                if (error == null)
                {
                    saved.Warnings.AddRange(package._imageSizeWarnings);
                    return saved;
                }
                problem = $"saved outputs {savedPath} are invalid ({error})";
            }
            catch (Exception ex)
            {
                problem = $"saved outputs {savedPath} could not be read ({ex.Message})";
            }
            package.Warnings.Insert(0, $"{problem} — using {Path.GetFileName(packagePath)} from the package");
            return package;
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
                cfg._imageSizeWarnings.Add($"config is not valid JSON ({ex.Message}) — using the default HyperHDR image size");
                return cfg;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    cfg.Warnings.Add("config root must be an object — using defaults (ESP32 only)");
                    cfg._imageSizeWarnings.Add("config root must be an object — using the default HyperHDR image size");
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
                    cfg.HyperHdr = HyperHdrSettings.Parse(hyper, cfg.Warnings, cfg._imageSizeWarnings);
            }
            return cfg;
        }

        // The "outputs" value of the set-outputs command (protocol/schemas/service/outputs.schema.json):
        //   {"esp32": {"enabled": bool}, "hyperhdr": {"enabled": bool, "host": string, "port": int, "priority"?: int}}
        // Returns null and the new settings (Source = Saved), or the errors, "; "-separated.
        // The HyperHDR image size isn't part of the command: it is kept from imageSizeOf (the
        // settings in effect), or the default without one.
        public static string ParseCommand(string json, HyperHdrSettings imageSizeOf, out OutputConfig cfg) =>
            ParseStrict(json, false, imageSizeOf, out cfg);

        // Strict parse (see the class comment); the image size is taken from imageSizeOf, or the
        // default without one. savedFile also accepts, and ignores, the image size keys that
        // older versions saved.
        private static string ParseStrict(string json, bool savedFile, HyperHdrSettings imageSizeOf, out OutputConfig cfg)
        {
            cfg = null;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json ?? ""); }
            catch (JsonException ex) { return $"not valid JSON: {ex.Message}"; }

            var errors = new List<string>();
            var result = new OutputConfig { Source = OutputSource.Saved };
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return "must be a JSON object with \"esp32\" and \"hyperhdr\"";
                OnlyKeys(root, "", errors, "esp32", "hyperhdr");

                if (Section(root, "esp32", errors, out var esp32))
                {
                    OnlyKeys(esp32, "esp32.", errors, "enabled");
                    result.Esp32Enabled = StrictBool(esp32, "enabled", "esp32", errors);
                }

                if (Section(root, "hyperhdr", errors, out var hyper))
                {
                    if (savedFile) OnlyKeys(hyper, "hyperhdr.", errors, "enabled", "host", "port", "priority", "imageWidth", "imageHeight");
                    else OnlyKeys(hyper, "hyperhdr.", errors, "enabled", "host", "port", "priority");

                    var s = new HyperHdrSettings();
                    bool enabled = StrictBool(hyper, "enabled", "hyperhdr", errors);
                    bool hostOk = false;
                    if (!hyper.TryGetProperty("host", out var host)) errors.Add("hyperhdr.host is missing");
                    else if (host.ValueKind != JsonValueKind.String) errors.Add("hyperhdr.host must be a string");
                    else { s.Host = host.GetString().Trim(); hostOk = true; }
                    s.Port = StrictInt(hyper, "port", null, 1, 65535, errors);
                    s.Priority = StrictInt(hyper, "priority", s.Priority, HyperHdrProtocol.MinPriority, HyperHdrProtocol.MaxPriority, errors);
                    if (imageSizeOf != null)
                    {
                        s.ImageWidth = imageSizeOf.ImageWidth;
                        s.ImageHeight = imageSizeOf.ImageHeight;
                    }
                    if (enabled && hostOk && s.Host.Length == 0)
                        errors.Add("hyperhdr.host is required when hyperhdr.enabled is true");
                    s.Enabled = enabled;
                    result.HyperHdr = s;
                }
            }
            if (errors.Count > 0) return string.Join("; ", errors);
            cfg = result;
            return null;
        }

        // Writes the settings for LoadEffective through a temp file: its data is flushed to the
        // disk (fsync) before it is renamed over path, and on Linux the rename replaces path
        // atomically. So after a power cut path holds either the previous settings or the new
        // ones, never half a file (if the rename itself didn't reach the disk, the previous
        // settings come back at the next start).
        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(StatusJson.Build(WriteSaved));
            using (var file = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes, 0, bytes.Length);
                file.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, true);
        }

        // The saved file: only the set-outputs keys. The rest (the HyperHDR image size) is read
        // from res/ambilight.json at every start.
        private void WriteSaved(Utf8JsonWriter w)
        {
            w.WriteStartObject();
            w.WriteStartObject("esp32");
            w.WriteBoolean("enabled", Esp32Enabled);
            w.WriteEndObject();
            w.WriteStartObject("hyperhdr");
            w.WriteBoolean("enabled", HyperHdr.Enabled);
            w.WriteString("host", HyperHdr.Host);
            w.WriteNumber("port", HyperHdr.Port);
            w.WriteNumber("priority", HyperHdr.Priority);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        // The status "outputs" section: the settings in effect and where they came from.
        public void WriteJson(Utf8JsonWriter w)
        {
            w.WriteStartObject();
            w.WriteString("source", Source == OutputSource.Saved ? "saved" : "package");
            w.WriteStartObject("esp32");
            w.WriteBoolean("enabled", Esp32Enabled);
            w.WriteEndObject();
            w.WriteStartObject("hyperhdr");
            w.WriteBoolean("enabled", HyperHdr.Enabled);
            w.WriteString("host", HyperHdr.Host);
            w.WriteNumber("port", HyperHdr.Port);
            w.WriteNumber("priority", HyperHdr.Priority);
            w.WriteEndObject();
            w.WriteEndObject();
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

        // ------------------------------------------------------------------ strict helpers

        private static bool Section(JsonElement root, string name, List<string> errors, out JsonElement section)
        {
            if (!root.TryGetProperty(name, out section)) { errors.Add($"{name} is missing"); return false; }
            if (section.ValueKind != JsonValueKind.Object) { errors.Add($"{name} must be an object"); return false; }
            return true;
        }

        private static void OnlyKeys(JsonElement obj, string prefix, List<string> errors, params string[] allowed)
        {
            foreach (var p in obj.EnumerateObject())
                if (Array.IndexOf(allowed, p.Name) < 0) errors.Add($"unknown key {prefix}{p.Name}");
        }

        private static bool StrictBool(JsonElement obj, string name, string section, List<string> errors)
        {
            if (!obj.TryGetProperty(name, out var v)) { errors.Add($"{section}.{name} is missing"); return false; }
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            errors.Add($"{section}.{name} must be true or false");
            return false;
        }

        // A hyperhdr integer; fallback null means the key is required.
        private static int StrictInt(JsonElement obj, string name, int? fallback, int min, int max, List<string> errors)
        {
            if (!obj.TryGetProperty(name, out var v))
            {
                if (fallback == null) errors.Add($"hyperhdr.{name} is missing");
                return fallback ?? 0;
            }
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= min && n <= max) return n;
            errors.Add(Invariant($"hyperhdr.{name} must be an integer {min}..{max}"));
            return fallback ?? 0;
        }
    }

    sealed class HyperHdrSettings
    {
        public bool Enabled { get; internal set; }
        public string Host { get; internal set; } = "";
        public int Port { get; internal set; } = 19400;
        public int Priority { get; internal set; } = 150;
        public int ImageWidth { get; internal set; } = 64;
        public int ImageHeight { get; internal set; } = 36;
        public string Origin => "Tizen Ambilight";

        // Same target and image: an output running with other's settings can be kept as it is.
        public bool SameAs(HyperHdrSettings other) =>
            other != null && Enabled == other.Enabled && Host == other.Host && Port == other.Port
            && Priority == other.Priority && ImageWidth == other.ImageWidth && ImageHeight == other.ImageHeight;

        // Lenient parse of res/ambilight.json's hyperhdr section. The problems that affect the
        // image size also go to imageSizeWarnings (see OutputConfig.LoadEffective).
        internal static HyperHdrSettings Parse(JsonElement obj, List<string> warnings, List<string> imageSizeWarnings)
        {
            var s = new HyperHdrSettings();
            if (obj.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("hyperhdr must be an object — HyperHDR output disabled");
                imageSizeWarnings.Add("hyperhdr must be an object — using the default HyperHDR image size");
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
            var sizeWarnings = new List<string>();
            s.ImageWidth  = OutputConfig.GetInt(obj, "imageWidth",  s.ImageWidth,  EdgeImage.MinSize, EdgeImage.MaxSize, "hyperhdr", sizeWarnings, ref valid);
            s.ImageHeight = OutputConfig.GetInt(obj, "imageHeight", s.ImageHeight, EdgeImage.MinSize, EdgeImage.MaxSize, "hyperhdr", sizeWarnings, ref valid);
            warnings.AddRange(sizeWarnings);
            foreach (var w in sizeWarnings) imageSizeWarnings.Add($"{w} — using the default");

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
