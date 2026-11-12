using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AceGLauncher
{
    public sealed class AppEntry
    {
        public string Id;
        public string Name;
        public string Path;
        public string Arguments = "";
        public double TotalSeconds;
        public int Sessions;
        public double LongestSessionSeconds;
        public DateTime AddedUtc;
        public DateTime LastPlayedUtc; // DateTime.MinValue = never

        // Runtime state (not persisted)
        public bool IsRunning;
        public double CurrentSessionSeconds;
        public DateTime LaunchingUntilUtc;
        public Bitmap Icon;
        public Color Tint = Theme.Accent;

        public bool Exists { get { return File.Exists(Path); } }
    }

    /// <summary>Stores the library in %APPDATA% and keeps playtime counters up to date.</summary>
    public sealed class Library
    {
        public readonly List<AppEntry> Apps = new List<AppEntry>();
        public event Action Changed;

        private readonly string _file;
        private DateTime _lastTickUtc = DateTime.UtcNow;
        private DateTime _lastSaveUtc = DateTime.UtcNow;
        private bool _dirty;

        public Library()
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AceG Launcher");
            Directory.CreateDirectory(dir);
            _file = System.IO.Path.Combine(dir, "library.tsv");
        }

        public string DataFile { get { return _file; } }

        public double TotalSeconds { get { return Apps.Sum(a => a.TotalSeconds); } }
        public int TotalSessions { get { return Apps.Sum(a => a.Sessions); } }
        public int RunningCount { get { return Apps.Count(a => a.IsRunning); } }

        public AppEntry Add(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            AppEntry existing = Apps.FirstOrDefault(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var app = new AppEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = GuessName(path),
                Path = path,
                AddedUtc = DateTime.UtcNow,
                LastPlayedUtc = DateTime.MinValue
            };
            IconLoader.Load(app);
            Apps.Add(app);
            Save();
            OnChanged();
            return app;
        }

        public void Remove(AppEntry app)
        {
            Apps.Remove(app);
            if (app.Icon != null) app.Icon.Dispose();
            Save();
            OnChanged();
        }

        public void Rename(AppEntry app, string name)
        {
            name = Clean(name);
            if (name.Length == 0) return;
            app.Name = name;
            Save();
            OnChanged();
        }

        public void SetPath(AppEntry app, string path)
        {
            app.Path = System.IO.Path.GetFullPath(path);
            if (app.Icon != null) app.Icon.Dispose();
            IconLoader.Load(app);
            Save();
            OnChanged();
        }

        public void ResetStats(AppEntry app)
        {
            app.TotalSeconds = 0;
            app.Sessions = 0;
            app.LongestSessionSeconds = 0;
            app.LastPlayedUtc = DateTime.MinValue;
            Save();
            OnChanged();
        }

        public void Launch(AppEntry app)
        {
            if (!app.Exists) throw new FileNotFoundException("File not found: " + app.Path);
            var psi = new ProcessStartInfo(app.Path, app.Arguments ?? "")
            {
                UseShellExecute = true, // lets Windows show the UAC prompt for apps that need admin
                WorkingDirectory = System.IO.Path.GetDirectoryName(app.Path)
            };
            using (Process.Start(psi)) { }
            app.LaunchingUntilUtc = DateTime.UtcNow.AddSeconds(5);
        }

        /// <summary>Called once per second. Detects running apps (also ones started outside the launcher).</summary>
        public void Tick()
        {
            DateTime now = DateTime.UtcNow;
            double delta = (now - _lastTickUtc).TotalSeconds;
            _lastTickUtc = now;
            // After sleep/hibernate the gap is huge; don't count it as playtime.
            if (delta < 0 || delta > 10) delta = 1;

            bool changed = false;
            HashSet<string> running = ProcessScanner.RunningPaths(Apps.Select(a => a.Path));

            foreach (AppEntry app in Apps)
            {
                bool isRunning = running.Contains(app.Path.ToLowerInvariant());
                if (isRunning)
                {
                    if (!app.IsRunning)
                    {
                        app.LaunchingUntilUtc = DateTime.MinValue;
                        app.IsRunning = true;
                        app.CurrentSessionSeconds = 0;
                        app.Sessions++;
                    }
                    else
                    {
                        app.CurrentSessionSeconds += delta;
                        app.TotalSeconds += delta;
                    }
                    app.LastPlayedUtc = now;
                    if (app.CurrentSessionSeconds > app.LongestSessionSeconds)
                        app.LongestSessionSeconds = app.CurrentSessionSeconds;
                    _dirty = true;
                    changed = true;
                }
                else if (app.IsRunning)
                {
                    app.IsRunning = false;
                    app.CurrentSessionSeconds = 0;
                    _dirty = true;
                    changed = true;
                }
            }

            if (_dirty && (now - _lastSaveUtc).TotalSeconds >= 30) Save();
            if (changed) OnChanged();
        }

        public void Load()
        {
            Apps.Clear();
            if (!File.Exists(_file)) return;
            foreach (string line in File.ReadAllLines(_file, Encoding.UTF8))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length < 9) continue;
                try
                {
                    var app = new AppEntry
                    {
                        Id = f[0],
                        Name = f[1],
                        Path = f[2],
                        Arguments = f[3],
                        TotalSeconds = double.Parse(f[4], CultureInfo.InvariantCulture),
                        Sessions = int.Parse(f[5], CultureInfo.InvariantCulture),
                        LongestSessionSeconds = double.Parse(f[6], CultureInfo.InvariantCulture),
                        AddedUtc = new DateTime(long.Parse(f[7], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                        LastPlayedUtc = new DateTime(long.Parse(f[8], CultureInfo.InvariantCulture), DateTimeKind.Utc)
                    };
                    IconLoader.Load(app);
                    Apps.Add(app);
                }
                catch (FormatException) { }
                catch (OverflowException) { }
                catch (ArgumentException) { }
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# AceG Launcher library v1: id, name, path, args, seconds, sessions, longest, added ticks, last played ticks");
            foreach (AppEntry a in Apps)
            {
                sb.Append(a.Id).Append('\t')
                  .Append(Clean(a.Name)).Append('\t')
                  .Append(a.Path).Append('\t')
                  .Append(Clean(a.Arguments ?? "")).Append('\t')
                  .Append(a.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(a.Sessions.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(a.LongestSessionSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(a.AddedUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(a.LastPlayedUtc.Ticks.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }
            // Write to a temp file first so a crash mid-write never corrupts the library.
            string tmp = _file + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(_file)) File.Delete(_file);
            File.Move(tmp, _file);
            _dirty = false;
            _lastSaveUtc = DateTime.UtcNow;
        }

        private void OnChanged()
        {
            var h = Changed;
            if (h != null) h();
        }

        private static string Clean(string s)
        {
            return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string GuessName(string path)
        {
            try
            {
                FileVersionInfo v = FileVersionInfo.GetVersionInfo(path);
                string n = !string.IsNullOrWhiteSpace(v.ProductName) ? v.ProductName : v.FileDescription;
                if (!string.IsNullOrWhiteSpace(n) && n.Length <= 40) return Clean(n);
            }
            catch (Exception) { }
            return Clean(System.IO.Path.GetFileNameWithoutExtension(path));
        }
    }

    public static class ProcessScanner
    {
        /// <summary>Returns lower-cased full paths of the given executables that currently have a live process.</summary>
        public static HashSet<string> RunningPaths(IEnumerable<string> paths)
        {
            var result = new HashSet<string>();
            var byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in paths)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(p);
                List<string> list;
                if (!byName.TryGetValue(name, out list)) byName[name] = list = new List<string>();
                list.Add(p.ToLowerInvariant());
            }

            foreach (var kv in byName)
            {
                Process[] procs;
                try { procs = Process.GetProcessesByName(kv.Key); }
                catch (Exception) { continue; }
                foreach (Process proc in procs)
                {
                    using (proc)
                    {
                        string full = Native.GetProcessPath(proc);
                        if (full != null)
                        {
                            full = full.ToLowerInvariant();
                            if (kv.Value.Contains(full)) result.Add(full);
                        }
                        else if (kv.Value.Count == 1)
                        {
                            // Path not readable (e.g. protected process): fall back to matching by file name.
                            result.Add(kv.Value[0]);
                        }
                    }
                }
            }
            return result;
        }
    }

    public static class IconLoader
    {
        public static void Load(AppEntry app)
        {
            Bitmap bmp = null;
            if (File.Exists(app.Path))
            {
                bmp = Native.ExtractLargeIcon(app.Path, 128);
                if (bmp == null)
                {
                    try
                    {
                        using (Icon ico = System.Drawing.Icon.ExtractAssociatedIcon(app.Path))
                            if (ico != null) bmp = ico.ToBitmap();
                    }
                    catch (Exception) { }
                }
            }
            app.Icon = bmp;
            app.Tint = bmp != null ? DominantColor(bmp) : Theme.Accent;
        }

        /// <summary>Average of the saturated pixels, used to tint each card's artwork.</summary>
        private static Color DominantColor(Bitmap bmp)
        {
            long r = 0, g = 0, b = 0, n = 0;
            int step = Math.Max(1, bmp.Width / 32);
            for (int y = 0; y < bmp.Height; y += step)
                for (int x = 0; x < bmp.Width; x += step)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A < 128) continue;
                    int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B));
                    if (max - min < 40 || max < 60) continue;
                    r += c.R; g += c.G; b += c.B; n++;
                }
            if (n < 4) return Theme.Accent;
            return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
        }
    }
}
