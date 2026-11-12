using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using UmbraLauncher.Core;
using UmbraLauncher.Models;

namespace UmbraLauncher.Services
{
    public interface ITrackedApp
    {
        string Id { get; }
        AppMatcher Matcher { get; }
    }

    public sealed class TrackedSession
    {
        public string AppId;
        public DateTime StartUtc;
        public DateTime LastTickUtc;
        public DateTime LastAliveUtc;
        public double Seconds;
        public bool Detected;
        /// <summary>Process ids that belong to the session, with their creation FILETIME (0 = not yet known).</summary>
        public readonly Dictionary<int, long> Pids = new Dictionary<int, long>();

        // Launch hand-off: stubs such as Windows 11's notepad.exe start the real app under the same name and exit.
        public string LaunchExeNameLower;
        public long LaunchFileTime;
        public DateTime LaunchUtc;
    }

    /// <summary>
    /// Polls running processes every couple of seconds and turns "app is running" into sessions.
    /// A process counts for an app when it runs the app's executable or a listed extra process, or descends from the
    /// app's session. Shortly after a launch, once the launched program has exited, a new program from the app's own
    /// folder joins too, so launchers that hand off to the real game are counted. A process running another library
    /// app's executable is only ever credited to that app.
    /// </summary>
    public sealed class PlaytimeTracker
    {
        private const double MinSessionSeconds = 3;
        /// <summary>Gaps longer than this between ticks mean the PC slept; that time is not counted.</summary>
        private const double MaxTickGap = 20;
        /// <summary>How long after a launch a new program in the app's folder is treated as its hand-off.</summary>
        private const double HandoffSeconds = 120;

        private readonly DispatcherTimer _timer;
        private readonly ProcessScanner _scanner = new ProcessScanner();
        private readonly Dictionary<string, TrackedSession> _sessions = new Dictionary<string, TrackedSession>();
        private readonly Dictionary<string, DateTime> _pending = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, Handoff> _handoffs = new Dictionary<string, Handoff>();
        private bool _busy;

        public PlaytimeTracker()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += OnTick;
        }

        public Func<IList<ITrackedApp>> Apps { get; set; }
        public bool TrackExternal { get; set; } = true;
        public bool FocusOnly { get; set; }

        public event Action<string, TrackedSession> SessionStarted;
        /// <summary>Raised when a session closes; the record is null when it was too short to keep.</summary>
        public event Action<string, TrackedSession, SessionRecord> SessionEnded;
        public event Action Ticked;

        public IEnumerable<TrackedSession> ActiveSessions => _sessions.Values;

        public void Start()
        {
            _timer.Start();
            OnTick(null, EventArgs.Empty);
        }

        public void Stop() => _timer.Stop();

        public TrackedSession Get(string appId)
        {
            _sessions.TryGetValue(appId, out var s);
            return s;
        }

        public void NotifyLaunched(string appId, int? pid, string exeNameLower)
        {
            var now = DateTime.UtcNow;
            if (!_sessions.TryGetValue(appId, out var s)) s = Begin(appId, now, false);
            s.Detected = false;
            if (pid.HasValue) s.Pids[pid.Value] = 0;
            s.LaunchExeNameLower = exeNameLower;
            s.LaunchUtc = now;
            s.LaunchFileTime = now.AddSeconds(-2).ToFileTimeUtc();
            s.LastAliveUtc = now;
            _pending[appId] = now.AddSeconds(60);
            _handoffs[appId] = new Handoff { Until = now.AddSeconds(HandoffSeconds), AfterFileTime = s.LaunchFileTime };
        }

        public void Forget(string appId)
        {
            _sessions.Remove(appId);
            _pending.Remove(appId);
            _handoffs.Remove(appId);
        }

        /// <summary>Closes every open session (used on exit); running apps are picked up again on next start.</summary>
        public void EndAll()
        {
            foreach (var id in new List<string>(_sessions.Keys)) Finish(id, _sessions[id]);
        }

        private async void OnTick(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var snap = await Task.Run(() => _scanner.Scan());
                Apply(snap, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Log.Error("Tracker tick failed", ex);
            }
            finally
            {
                _busy = false;
            }
        }

        private void Apply(ProcessSnapshot snap, DateTime now)
        {
            var apps = Apps?.Invoke() ?? Array.Empty<ITrackedApp>();
            int foreground = FocusOnly ? Native.GetForegroundProcessId() : 0;
            var owners = Owners(apps, snap);
            var present = new HashSet<string>();

            foreach (var app in apps)
            {
                var m = app.Matcher;
                if (m == null) continue;
                present.Add(app.Id);

                _sessions.TryGetValue(app.Id, out var s);
                bool pending = _pending.TryGetValue(app.Id, out var until) && until > now;
                if (!pending) _pending.Remove(app.Id);
                Handoff handoff = null;
                if (_handoffs.TryGetValue(app.Id, out var h))
                {
                    if (h.Until > now) handoff = h;
                    else _handoffs.Remove(app.Id);
                }

                List<ProcInfo> matches = null;
                foreach (var p in snap.All)
                {
                    owners.TryGetValue(p.Pid, out var owner);
                    bool hit = owner == app.Id || (m.Extra.Count > 0 && m.Extra.Contains(p.ExeNameLower));
                    if (!hit && owner == null && s != null && s.LaunchExeNameLower != null && (now - s.LaunchUtc).TotalSeconds < 90 &&
                        p.ExeNameLower == s.LaunchExeNameLower && p.CreateTime >= s.LaunchFileTime)
                        hit = true;
                    if (hit) (matches ?? (matches = new List<ProcInfo>())).Add(p);
                }

                if (s == null)
                {
                    bool viaHandoff = false;
                    if (matches == null && handoff != null)
                    {
                        matches = HandoffMatches(app.Id, m, handoff, snap, owners);
                        viaHandoff = matches != null;
                    }

                    if (matches == null) continue;
                    bool launched = pending || (viaHandoff && !handoff.Detected);
                    if (!TrackExternal && !launched) continue;
                    s = Begin(app.Id, now, !launched);
                }

                AddAll(s, matches);
                AddDescendants(s, snap, owners);
                PruneExited(s, snap);

                if (s.Pids.Count == 0 && handoff != null)
                {
                    // The launched program is gone: a new program from the app's folder is the real app it handed off to.
                    var next = HandoffMatches(app.Id, m, handoff, snap, owners);
                    if (next != null)
                    {
                        AddAll(s, next);
                        AddDescendants(s, snap, owners);
                    }
                }

                if (s.Pids.Count > 0)
                {
                    double dt = (now - s.LastTickUtc).TotalSeconds;
                    if (dt < 0) dt = 0;
                    else if (dt > MaxTickGap) dt = _timer.Interval.TotalSeconds;
                    bool counting = !FocusOnly || (foreground != 0 && s.Pids.ContainsKey(foreground));
                    if (counting) s.Seconds += dt;
                    s.LastAliveUtc = now;
                }
                else
                {
                    // Give freshly launched apps time to show up (UAC prompt, slow start).
                    double grace = pending ? 45 : 6;
                    if ((now - s.LastAliveUtc).TotalSeconds >= grace)
                    {
                        Finish(app.Id, s);
                        continue;
                    }
                }

                s.LastTickUtc = now;
            }

            foreach (var id in new List<string>(_sessions.Keys))
                if (!present.Contains(id)) _sessions.Remove(id);

            Ticked?.Invoke();
        }

        /// <summary>Maps each process that runs a library app's executable to that app's id.</summary>
        private static Dictionary<int, string> Owners(IList<ITrackedApp> apps, ProcessSnapshot snap)
        {
            var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
            var byName = new Dictionary<string, string>(StringComparer.Ordinal);
            var sharedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var app in apps)
            {
                var m = app.Matcher;
                if (m?.ExePathLower == null) continue;
                byPath[m.ExePathLower] = app.Id;
                if (byName.ContainsKey(m.ExeNameLower)) sharedNames.Add(m.ExeNameLower);
                else byName[m.ExeNameLower] = app.Id;
            }

            foreach (var name in sharedNames) byName.Remove(name);

            var owners = new Dictionary<int, string>();
            foreach (var p in snap.All)
            {
                // Protected processes hide their path; fall back to a file name that only one library app uses.
                string id;
                if (p.PathLower != null ? byPath.TryGetValue(p.PathLower, out id) : byName.TryGetValue(p.ExeNameLower, out id))
                    owners[p.Pid] = id;
            }

            return owners;
        }

        private static List<ProcInfo> HandoffMatches(string appId, AppMatcher m, Handoff handoff, ProcessSnapshot snap, Dictionary<int, string> owners)
        {
            if (m.DirLower == null) return null;
            List<ProcInfo> result = null;
            foreach (var p in snap.All)
            {
                if (p.PathLower == null || p.CreateTime < handoff.AfterFileTime || !p.PathLower.StartsWith(m.DirLower, StringComparison.Ordinal)) continue;
                if (owners.TryGetValue(p.Pid, out var owner) && owner != appId) continue;
                (result ?? (result = new List<ProcInfo>())).Add(p);
            }

            return result;
        }

        private static void AddAll(TrackedSession s, List<ProcInfo> procs)
        {
            if (procs == null) return;
            foreach (var p in procs)
                if (!s.Pids.ContainsKey(p.Pid)) s.Pids[p.Pid] = p.CreateTime;
        }

        private static void AddDescendants(TrackedSession s, ProcessSnapshot snap, Dictionary<int, string> owners)
        {
            // Walk from every member, including ones that already exited this tick, so a launcher
            // that spawns the game and quits immediately still hands the session over.
            var queue = new Queue<KeyValuePair<int, long>>(s.Pids);
            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (!snap.Children.TryGetValue(parent.Key, out var kids)) continue;
                foreach (var kid in kids)
                {
                    if (s.Pids.ContainsKey(kid.Pid)) continue;
                    if (parent.Value != 0 && kid.CreateTime != 0 && kid.CreateTime < parent.Value) continue; // PID was reused
                    if (owners.TryGetValue(kid.Pid, out var owner) && owner != s.AppId) continue; // counted for that app instead
                    s.Pids[kid.Pid] = kid.CreateTime;
                    queue.Enqueue(new KeyValuePair<int, long>(kid.Pid, kid.CreateTime));
                }
            }
        }

        private static void PruneExited(TrackedSession s, ProcessSnapshot snap)
        {
            List<int> dead = null;
            List<int> learn = null;
            foreach (var kv in s.Pids)
            {
                if (!snap.ByPid.TryGetValue(kv.Key, out var p) ||
                    (kv.Value != 0 && p.CreateTime != 0 && p.CreateTime != kv.Value))
                    (dead ?? (dead = new List<int>())).Add(kv.Key);
                else if (kv.Value == 0 && p.CreateTime != 0)
                    (learn ?? (learn = new List<int>())).Add(kv.Key);
            }

            if (dead != null) foreach (var pid in dead) s.Pids.Remove(pid);
            if (learn != null) foreach (var pid in learn) s.Pids[pid] = snap.ByPid[pid].CreateTime;
        }

        private TrackedSession Begin(string appId, DateTime now, bool detected)
        {
            var s = new TrackedSession
            {
                AppId = appId,
                StartUtc = now,
                LastTickUtc = now,
                LastAliveUtc = now,
                Detected = detected,
            };
            _sessions[appId] = s;
            if (detected && !_handoffs.ContainsKey(appId))
                _handoffs[appId] = new Handoff { Until = now.AddSeconds(HandoffSeconds), AfterFileTime = now.AddSeconds(-5).ToFileTimeUtc(), Detected = true };
            SessionStarted?.Invoke(appId, s);
            return s;
        }

        private void Finish(string appId, TrackedSession s)
        {
            _sessions.Remove(appId);
            _pending.Remove(appId);
            SessionRecord record = null;
            if (s.Seconds >= MinSessionSeconds)
            {
                record = new SessionRecord
                {
                    Start = Fmt.ToUnix(s.StartUtc),
                    End = Fmt.ToUnix(s.LastAliveUtc),
                    Seconds = (int)Math.Round(s.Seconds),
                    Detected = s.Detected,
                };
            }

            SessionEnded?.Invoke(appId, s, record);
        }

        private sealed class Handoff
        {
            public DateTime Until;
            /// <summary>Only processes created after this FILETIME qualify.</summary>
            public long AfterFileTime;
            /// <summary>Opened by a session detected outside the launcher rather than by a launch.</summary>
            public bool Detected;
        }
    }
}
