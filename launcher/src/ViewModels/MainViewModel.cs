using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UmbraLauncher.Core;
using UmbraLauncher.Models;
using UmbraLauncher.Services;

namespace UmbraLauncher.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private static readonly string[] SupportedExtensions = { ".exe", ".lnk" };

        private readonly Storage _storage;
        private readonly PlaytimeTracker _tracker = new PlaytimeTracker();
        private readonly DispatcherTimer _saveTimer;
        private readonly DispatcherTimer _persistTimer;
        private string _page = "Library";
        private string _search = string.Empty;
        private AppItemViewModel _hero;
        private AppItemViewModel _drawerApp;
        private bool _isDrawerOpen;
        private EditorViewModel _editor;
        private ConfirmViewModel _confirm;
        private bool _isDropTarget;
        private bool _shutdown;
        private bool _startWithWindows;
        private int _tick;

        public MainViewModel(Storage storage, LibraryData data)
        {
            _storage = storage;
            Data = data;
            Settings = data.Settings;
            _startWithWindows = StartupService.IsEnabled;

            RecoverInterruptedSessions();
            foreach (var entry in data.Apps) Apps.Add(new AppItemViewModel(this, entry));

            AddCommand = new RelayCommand(() => OpenAddEditor(null));
            AddTile = new AddTileItem(AddCommand);
            Stats = new StatsViewModel(this);
            ToggleLanguageCommand = new RelayCommand(() => Language = Loc.I.IsRu ? "en" : "ru");
            CloseDrawerCommand = new RelayCommand(() => IsDrawerOpen = false);
            EscapeCommand = new RelayCommand(OnEscape);
            OpenDataFolderCommand = new RelayCommand(() => ShellHelpers.OpenFolder(_storage.DataDir));
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ResetStatsCommand = new RelayCommand(AskResetStats);
            foreach (var hex in ThemeService.Accents)
            {
                var h = hex;
                AccentOptions.Add(new AccentOption(hex, new RelayCommand(() => AccentHex = h)) { IsSelected = string.Equals(hex, Settings.Accent, StringComparison.OrdinalIgnoreCase) });
            }

            _tracker.Apps = () => Apps.Cast<ITrackedApp>().ToList();
            _tracker.TrackExternal = Settings.TrackExternal;
            _tracker.FocusOnly = Settings.FocusOnly;
            _tracker.SessionStarted += OnSessionStarted;
            _tracker.SessionEnded += OnSessionEnded;
            _tracker.Ticked += OnTick;

            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                SaveNow();
            };
            _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _persistTimer.Tick += (s, e) => PersistActiveSessions();

            Loc.I.LanguageChanged += (s, e) => OnLanguageChanged();
            RebuildVisible();
            UpdateHero();
            UpdateStatus();
            Stats.Recompute();
        }

        public LibraryData Data { get; }
        public AppSettings Settings { get; }
        public string DataDir => _storage.DataDir;

        public ObservableCollection<AppItemViewModel> Apps { get; } = new ObservableCollection<AppItemViewModel>();
        public ObservableCollection<object> VisibleItems { get; } = new ObservableCollection<object>();
        public ObservableCollection<AppItemViewModel> VisibleApps { get; } = new ObservableCollection<AppItemViewModel>();
        public ObservableCollection<ToastViewModel> Toasts { get; } = new ObservableCollection<ToastViewModel>();
        public ObservableCollection<AccentOption> AccentOptions { get; } = new ObservableCollection<AccentOption>();
        public AddTileItem AddTile { get; }
        public StatsViewModel Stats { get; }

        public ICommand AddCommand { get; }
        public ICommand ToggleLanguageCommand { get; }
        public ICommand CloseDrawerCommand { get; }
        public ICommand EscapeCommand { get; }
        public ICommand OpenDataFolderCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ResetStatsCommand { get; }

        public event Action RequestMinimize;

        // ---------- Navigation & state ----------

        public string Page
        {
            get => _page;
            set
            {
                if (!Set(ref _page, value)) return;
                IsDrawerOpen = false;
                if (value == "Stats") Stats.Recompute();
            }
        }

        public string SearchText
        {
            get => _search;
            set
            {
                if (Set(ref _search, value ?? string.Empty))
                {
                    RebuildVisible();
                    OnPropertyChanged(nameof(HasSearch));
                }
            }
        }

        public bool HasSearch => !string.IsNullOrWhiteSpace(_search);

        public string SortMode
        {
            get => Settings.Sort ?? "recent";
            set
            {
                if (value == null || Settings.Sort == value) return;
                Settings.Sort = value;
                OnPropertyChanged();
                RebuildVisible();
                RequestSave();
            }
        }

        public string ViewMode
        {
            get => Settings.View ?? "grid";
            set
            {
                if (value == null || Settings.View == value) return;
                Settings.View = value;
                OnPropertyChanged();
                RequestSave();
            }
        }

        public int StatsRange
        {
            get => Settings.StatsRange == 7 || Settings.StatsRange == 30 ? Settings.StatsRange : 14;
            set
            {
                if (Settings.StatsRange == value) return;
                Settings.StatsRange = value;
                OnPropertyChanged();
                Stats.Recompute();
                RequestSave();
            }
        }

        public AppItemViewModel HeroApp { get => _hero; private set => Set(ref _hero, value); }
        public AppItemViewModel DrawerApp { get => _drawerApp; private set => Set(ref _drawerApp, value); }

        public bool IsDrawerOpen
        {
            get => _isDrawerOpen;
            set => Set(ref _isDrawerOpen, value);
        }

        public EditorViewModel Editor { get => _editor; private set => Set(ref _editor, value); }
        public ConfirmViewModel Confirm { get => _confirm; private set => Set(ref _confirm, value); }
        public bool IsDropTarget { get => _isDropTarget; set => Set(ref _isDropTarget, value); }

        public bool HasApps => Apps.Count > 0;
        public bool HasResults => VisibleApps.Count > 0 || !HasSearch;
        public string AppCountText => Apps.Count.ToString(Loc.I.Culture);
        public int RunningCount => Apps.Count(a => a.IsRunning);

        public string RunningChipText
        {
            get
            {
                var running = Apps.Where(a => a.IsRunning).ToList();
                if (running.Count == 0) return string.Empty;
                if (running.Count == 1) return $"{running[0].Name.ToUpper(Loc.I.Culture)} · {running[0].LiveClock}";
                return $"{running.Count} · {Loc.T("Running")}";
            }
        }

        public string StatusText
        {
            get
            {
                var running = Apps.Where(a => a.IsRunning).Select(a => a.Name).ToList();
                return running.Count == 0 ? Loc.T("StatusIdle") : Loc.F("StatusRunning", string.Join(", ", running));
            }
        }

        public string StatusTotalText => $"{Loc.F("StatusTotal", Fmt.Duration(Apps.Sum(a => a.TotalSeconds)))}  ·  {Fmt.Apps(Apps.Count)}";

        public string BrandTitle => string.IsNullOrWhiteSpace(Settings.Title) ? AppSettings.DefaultTitle : Settings.Title.Trim();
        public string WindowTitle => BrandTitle + " Launcher";
        public string VersionText => Loc.F("Version", typeof(MainViewModel).Assembly.GetName().Version.ToString(3));

        // ---------- Settings ----------

        public string Language
        {
            get => Loc.I.Language;
            set
            {
                Loc.I.Language = value;
                Settings.Language = Loc.I.Language;
                OnPropertyChanged();
                RequestSave();
            }
        }

        public string AccentHex
        {
            get => Settings.Accent;
            set
            {
                Settings.Accent = value;
                ThemeService.ApplyAccent(value);
                foreach (var o in AccentOptions) o.IsSelected = string.Equals(o.Hex, value, StringComparison.OrdinalIgnoreCase);
                OnPropertyChanged();
                RequestSave();
            }
        }

        public string LauncherTitle
        {
            get => Settings.Title;
            set
            {
                Settings.Title = value;
                Raise(nameof(LauncherTitle), nameof(BrandTitle), nameof(WindowTitle));
                TitleChanged?.Invoke();
                RequestSave();
            }
        }

        public event Action TitleChanged;

        public bool MinimizeOnLaunch
        {
            get => Settings.MinimizeOnLaunch;
            set { Settings.MinimizeOnLaunch = value; OnPropertyChanged(); RequestSave(); }
        }

        public bool CloseToTray
        {
            get => Settings.CloseToTray;
            set { Settings.CloseToTray = value; OnPropertyChanged(); RequestSave(); }
        }

        public bool TrackExternal
        {
            get => Settings.TrackExternal;
            set { Settings.TrackExternal = value; _tracker.TrackExternal = value; OnPropertyChanged(); RequestSave(); }
        }

        public bool FocusOnly
        {
            get => Settings.FocusOnly;
            set { Settings.FocusOnly = value; _tracker.FocusOnly = value; OnPropertyChanged(); RequestSave(); }
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                StartupService.SetEnabled(value);
                _startWithWindows = StartupService.IsEnabled;
                OnPropertyChanged();
            }
        }

        // ---------- Lifecycle ----------

        public void Start()
        {
            _tracker.Start();
            _persistTimer.Start();
        }

        public void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            _tracker.Stop();
            _persistTimer.Stop();
            _tracker.EndAll();
            SaveNow();
        }

        public void RequestSave()
        {
            if (_shutdown)
            {
                SaveNow();
                return;
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void SaveNow()
        {
            try
            {
                _storage.Save(Data);
            }
            catch (Exception ex)
            {
                Log.Error("Saving the library failed", ex);
                if (!_shutdown) Toast("error", Loc.T("ToastError"), ex.Message);
            }
        }

        /// <summary>Turns sessions that were in progress when the launcher last died into finished ones.</summary>
        private void RecoverInterruptedSessions()
        {
            bool changed = false;
            foreach (var e in Data.Apps)
            {
                if (e.Active == null) continue;
                if (e.Active.Seconds >= 3)
                {
                    e.Sessions.Add(new SessionRecord
                    {
                        Start = e.Active.Start,
                        End = Math.Max(e.Active.Start, e.Active.LastSeen),
                        Seconds = (int)Math.Round(e.Active.Seconds),
                        Detected = e.Active.Detected,
                    });
                }

                e.Active = null;
                changed = true;
            }

            if (changed)
            {
                try { _storage.Save(Data); }
                catch (Exception ex) { Log.Error("Could not save recovered sessions", ex); }
            }
        }

        private void PersistActiveSessions()
        {
            bool any = false;
            foreach (var s in _tracker.ActiveSessions)
            {
                var app = Find(s.AppId);
                if (app == null) continue;
                app.Entry.Active = new ActiveSession
                {
                    Start = Fmt.ToUnix(s.StartUtc),
                    LastSeen = Fmt.ToUnix(s.LastAliveUtc),
                    Seconds = s.Seconds,
                    Detected = s.Detected,
                };
                any = true;
            }

            if (any) SaveNow();
            if (_tick % 2 == 0) foreach (var a in Apps) a.CheckMissing();
        }

        // ---------- Tracker events ----------

        private void OnSessionStarted(string appId, TrackedSession session)
        {
            var app = Find(appId);
            if (app == null) return;
            if (session.Detected)
            {
                app.Entry.LastPlayedAt = Fmt.NowUnix();
                RequestSave();
            }

            app.SetSession(session);
            AfterActivityChange();
        }

        private void OnSessionEnded(string appId, TrackedSession session, SessionRecord record)
        {
            var app = Find(appId);
            if (app == null) return;
            app.Entry.Active = null;
            if (record != null)
            {
                app.Entry.Sessions.Add(record);
                if (!_shutdown && record.Seconds >= 60)
                    Toast("success", Loc.T("ToastSession"), $"{app.Name} · {Fmt.Duration(record.Seconds)}");
            }

            app.SetSession(null);
            app.RecalcStats();
            if (ReferenceEquals(app, DrawerApp) && IsDrawerOpen) app.LoadDetails();
            RequestSave();
            AfterActivityChange();
        }

        private void OnTick()
        {
            _tick++;
            foreach (var a in Apps)
            {
                var s = _tracker.Get(a.Id);
                if (s != null)
                {
                    a.SetSession(s);
                    a.UpdateLive();
                }
                else if (a.IsRunning)
                {
                    a.SetSession(null);
                }
            }

            if (RunningCount > 0)
            {
                Raise(nameof(RunningChipText), nameof(StatusTotalText));
                if (_tick % 5 == 0)
                {
                    if (Page == "Stats") Stats.Recompute();
                    if (IsDrawerOpen && DrawerApp != null && DrawerApp.IsRunning) DrawerApp.LoadDetails();
                }
            }
        }

        private void AfterActivityChange()
        {
            UpdateHero();
            if (SortMode != "name") RebuildVisible();
            UpdateStatus();
            if (Page == "Stats") Stats.Recompute();
        }

        // ---------- Library ----------

        public AppItemViewModel Find(string id) => Apps.FirstOrDefault(a => a.Id == id);

        public bool IsDuplicate(string path, AppItemViewModel except)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch { full = path; }
            return Apps.Any(a => !ReferenceEquals(a, except) && string.Equals(SafeFull(a.Path), full, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeFull(string p)
        {
            try { return Path.GetFullPath(p); }
            catch { return p; }
        }

        private void RebuildVisible()
        {
            IEnumerable<AppItemViewModel> q = Apps;
            var term = _search.Trim();
            if (term.Length > 0)
            {
                q = q.Where(a => (a.Name ?? string.Empty).IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                                 (a.Path ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            switch (SortMode)
            {
                case "most":
                    q = q.OrderByDescending(a => a.TotalSeconds).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                case "name":
                    q = q.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
                default:
                    q = q.OrderByDescending(a => a.IsRunning)
                        .ThenByDescending(a => Math.Max(a.Entry.LastPlayedAt, a.Entry.AddedAt))
                        .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;
            }

            var list = q.ToList();
            Sync(VisibleApps, list);
            var items = new List<object>(list);
            if (term.Length == 0) items.Add(AddTile);
            Sync(VisibleItems, items);
            Raise(nameof(HasApps), nameof(HasResults), nameof(AppCountText));
        }

        private static void Sync<T>(ObservableCollection<T> target, IList<T> desired)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                if (i < target.Count && Equals(target[i], item)) continue;
                int idx = target.IndexOf(item);
                if (idx >= 0) target.Move(idx, i);
                else target.Insert(i, item);
            }

            while (target.Count > desired.Count) target.RemoveAt(target.Count - 1);
        }

        private void UpdateHero()
        {
            HeroApp = Apps.Where(a => a.IsRunning).OrderByDescending(a => a.Entry.LastPlayedAt).FirstOrDefault()
                      ?? Apps.OrderByDescending(a => a.Entry.LastPlayedAt).ThenByDescending(a => a.Entry.AddedAt).FirstOrDefault();
        }

        private void UpdateStatus()
        {
            Raise(nameof(RunningCount), nameof(RunningChipText), nameof(StatusText), nameof(StatusTotalText), nameof(HasApps), nameof(AppCountText));
            StatusChanged?.Invoke();
        }

        public event Action StatusChanged;

        // ---------- Launching ----------

        public void Launch(AppItemViewModel app, bool? asAdmin = null)
        {
            if (app == null) return;
            if (!File.Exists(app.Path))
            {
                app.CheckMissing();
                Toast("error", Loc.T("ToastFileMissing"), Loc.T("ToastFileMissingText"), Loc.T("Edit"), () => Edit(app));
                return;
            }

            var entry = app.Entry;
            var workDir = !string.IsNullOrWhiteSpace(entry.WorkingDirectory) && Directory.Exists(entry.WorkingDirectory)
                ? entry.WorkingDirectory
                : Path.GetDirectoryName(entry.Path);
            var psi = new ProcessStartInfo
            {
                FileName = entry.Path,
                Arguments = entry.Arguments ?? string.Empty,
                WorkingDirectory = workDir ?? string.Empty,
                UseShellExecute = true,
            };
            if (asAdmin ?? entry.RunAsAdmin) psi.Verb = "runas";

            int? pid = null;
            try
            {
                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        try { pid = p.Id; }
                        catch (InvalidOperationException) { pid = null; }
                    }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Toast("info", Loc.T("ToastLaunchCancelled"), Loc.T("ToastLaunchCancelledText"));
                return;
            }
            catch (Exception ex)
            {
                Log.Error("Launch failed: " + entry.Path, ex);
                Toast("error", Loc.T("ToastLaunchFailed"), ex.Message);
                return;
            }

            Log.Info($"Launched {entry.Name} (pid {pid?.ToString() ?? "?"})");
            _tracker.NotifyLaunched(app.Id, pid, Path.GetFileName(entry.Path).ToLowerInvariant());
            entry.LastPlayedAt = Fmt.NowUnix();
            entry.LaunchCount++;
            app.SetSession(_tracker.Get(app.Id));
            app.RecalcStats();
            AfterActivityChange();
            RequestSave();
            if (Settings.MinimizeOnLaunch) RequestMinimize?.Invoke();
        }

        public void LaunchById(string id)
        {
            var app = Find(id);
            if (app != null) Launch(app);
        }

        public void FocusApp(AppItemViewModel app)
        {
            var s = _tracker.Get(app.Id);
            if (s == null) return;
            var pids = new HashSet<int>(s.Pids.Keys);
            IntPtr found = IntPtr.Zero;
            Native.EnumWindows((h, l) =>
            {
                if (!Native.IsWindowVisible(h) || Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero || Native.GetWindowTextLength(h) == 0) return true;
                Native.GetWindowThreadProcessId(h, out var pid);
                if (!pids.Contains((int)pid)) return true;
                found = h;
                return false;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
            {
                Toast("info", Loc.T("ToastNoWindow"), Loc.T("ToastNoWindowText"));
                return;
            }

            if (Native.IsIconic(found)) Native.ShowWindow(found, Native.SW_RESTORE);
            Native.SetForegroundWindow(found);
        }

        /// <summary>Asks the app to close the way its own close button would (no force-kill, no lost work).</summary>
        public void CloseApp(AppItemViewModel app)
        {
            var s = _tracker.Get(app.Id);
            if (s == null) return;
            int asked = 0;
            foreach (var pid in s.Pids.Keys.ToList())
            {
                try
                {
                    using (var p = Process.GetProcessById(pid))
                        if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow()) asked++;
                }
                catch (Exception)
                {
                    // Already gone or access denied.
                }
            }

            if (asked == 0) Toast("info", Loc.T("ToastNoWindow"), Loc.T("ToastNoWindowText"));
        }

        // ---------- Drawer / editor / confirm ----------

        public void OpenDetails(AppItemViewModel app)
        {
            app.LoadDetails();
            DrawerApp = app;
            IsDrawerOpen = true;
        }

        public void OpenAddEditor(string prefillPath)
        {
            IsDrawerOpen = false;
            var ed = new EditorViewModel(this, null);
            if (!string.IsNullOrEmpty(prefillPath)) ed.ApplyFile(prefillPath);
            Editor = ed;
        }

        public void Edit(AppItemViewModel app)
        {
            IsDrawerOpen = false;
            Editor = new EditorViewModel(this, app);
        }

        public void CloseEditor() => Editor = null;

        public void CommitEditor(EditorViewModel ed)
        {
            if (!ed.Validate()) return;
            if (ed.IsNew)
            {
                var entry = new AppEntry
                {
                    Name = ed.Name.Trim(),
                    Path = ed.CleanPath,
                    Arguments = ed.Arguments.Trim(),
                    WorkingDirectory = ed.WorkingDirectory.Trim(),
                    RunAsAdmin = ed.RunAsAdmin,
                    ExtraProcesses = ed.ExtraProcesses.Trim(),
                    CoverPath = ed.CoverPath.Trim(),
                    AddedAt = Fmt.NowUnix(),
                };
                Data.Apps.Add(entry);
                Apps.Add(new AppItemViewModel(this, entry));
                Toast("success", Loc.T("ToastAdded"), entry.Name);
            }
            else
            {
                var e = ed.Target.Entry;
                e.Name = ed.Name.Trim();
                e.Path = ed.CleanPath;
                e.Arguments = ed.Arguments.Trim();
                e.WorkingDirectory = ed.WorkingDirectory.Trim();
                e.RunAsAdmin = ed.RunAsAdmin;
                e.ExtraProcesses = ed.ExtraProcesses.Trim();
                e.CoverPath = ed.CoverPath.Trim();
                ed.Target.Refresh();
                Toast("success", Loc.T("ToastSaved"), e.Name);
            }

            Editor = null;
            RebuildVisible();
            UpdateHero();
            UpdateStatus();
            Stats.Recompute();
            RequestSave();
        }

        /// <summary>Drag &amp; drop: one file opens the editor, several are added straight away.</summary>
        public void AddFiles(IEnumerable<string> files)
        {
            var all = files?.ToList() ?? new List<string>();
            var valid = all.Where(f => File.Exists(f) && SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
            int skipped = all.Count - valid.Count;

            if (valid.Count == 1 && skipped == 0)
            {
                OpenAddEditor(valid[0]);
                return;
            }

            int added = 0;
            foreach (var f in valid)
            {
                var ed = new EditorViewModel(this, null);
                ed.ApplyFile(f);
                if (!ed.Validate())
                {
                    skipped++;
                    continue;
                }

                var entry = new AppEntry
                {
                    Name = ed.Name.Trim(),
                    Path = ed.CleanPath,
                    Arguments = ed.Arguments.Trim(),
                    WorkingDirectory = ed.WorkingDirectory.Trim(),
                    AddedAt = Fmt.NowUnix(),
                };
                Data.Apps.Add(entry);
                Apps.Add(new AppItemViewModel(this, entry));
                added++;
            }

            if (added > 0)
            {
                Toast("success", added == 1 ? Loc.T("ToastAdded") : Loc.F("ToastAddedMany", added));
                RebuildVisible();
                UpdateHero();
                UpdateStatus();
                RequestSave();
            }

            if (skipped > 0) Toast("info", Loc.F("ToastSkipped", skipped));
        }

        public void AskRemove(AppItemViewModel app)
        {
            Confirm = new ConfirmViewModel(
                Loc.T("ConfirmRemoveTitle"),
                Loc.F("ConfirmRemoveText", app.Name, app.TotalText),
                Loc.T("Remove"),
                () => Remove(app),
                () => Confirm = null);
        }

        private void Remove(AppItemViewModel app)
        {
            _tracker.Forget(app.Id);
            Data.Apps.Remove(app.Entry);
            Apps.Remove(app);
            if (ReferenceEquals(DrawerApp, app))
            {
                IsDrawerOpen = false;
                DrawerApp = null;
            }

            RebuildVisible();
            UpdateHero();
            UpdateStatus();
            Stats.Recompute();
            RequestSave();
            Toast("info", Loc.T("ToastRemoved"), app.Name);
        }

        private void AskResetStats()
        {
            Confirm = new ConfirmViewModel(
                Loc.T("ConfirmResetTitle"),
                Loc.T("ConfirmResetText"),
                Loc.T("Reset"),
                ResetStats,
                () => Confirm = null);
        }

        private void ResetStats()
        {
            foreach (var a in Apps)
            {
                a.Entry.Sessions.Clear();
                a.Entry.Active = null;
                a.Entry.LaunchCount = 0;
                a.Entry.LastPlayedAt = 0;
                a.RecalcStats();
            }

            AfterActivityChange();
            Stats.Recompute();
            RequestSave();
            Toast("success", Loc.T("ToastReset"));
        }

        private void OnEscape()
        {
            if (Confirm != null) Confirm = null;
            else if (Editor != null) Editor = null;
            else if (IsDrawerOpen) IsDrawerOpen = false;
            else if (HasSearch) SearchText = string.Empty;
        }

        public void OnHiddenToTray()
        {
            if (Settings.TrayHintShown) return;
            Settings.TrayHintShown = true;
            RequestSave();
            HiddenToTrayFirstTime?.Invoke();
        }

        public event Action HiddenToTrayFirstTime;

        // ---------- Export ----------

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{BrandTitle.ToLowerInvariant()}-playtime-{DateTime.Now:yyyy-MM-dd}.csv",
                Filter = "CSV (*.csv)|*.csv",
                DefaultExt = ".csv",
            };
            if (dlg.ShowDialog(Application.Current.MainWindow) != true) return;

            try
            {
                var sep = Loc.I.Culture.TextInfo.ListSeparator;
                var sb = new StringBuilder();
                string Q(string v) => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"";
                sb.AppendLine(string.Join(sep, Loc.I.IsRu
                    ? new[] { "Приложение", "Путь", "Начало", "Конец", "Минуты", "Источник" }
                    : new[] { "App", "Path", "Start", "End", "Minutes", "Source" }));
                foreach (var a in Apps)
                {
                    foreach (var s in a.Entry.Sessions.OrderBy(x => x.Start))
                    {
                        sb.AppendLine(string.Join(sep,
                            Q(a.Name),
                            Q(a.Path),
                            Q(Fmt.FromUnix(s.Start).ToString("yyyy-MM-dd HH:mm:ss")),
                            Q(Fmt.FromUnix(s.End).ToString("yyyy-MM-dd HH:mm:ss")),
                            (s.Seconds / 60.0).ToString("0.0", Loc.I.Culture),
                            Q(s.Detected ? Loc.T("SourceDetected") : Loc.T("SourceLauncher"))));
                    }
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                Toast("success", Loc.T("ToastExported"), Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                Log.Error("CSV export failed", ex);
                Toast("error", Loc.T("ToastError"), ex.Message);
            }
        }

        // ---------- Toasts ----------

        public void Toast(string kind, string title, string message = null, string actionText = null, Action action = null)
        {
            var toast = new ToastViewModel(kind, title, message, actionText, action, t => Toasts.Remove(t));
            Toasts.Add(toast);
            while (Toasts.Count > 4) Toasts.RemoveAt(0);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == "error" ? 7 : 4.5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Toasts.Remove(toast);
            };
            timer.Start();
        }

        private void OnLanguageChanged()
        {
            foreach (var a in Apps) a.RaiseStats();
            if (IsDrawerOpen) DrawerApp?.LoadDetails();
            Stats.Recompute();
            UpdateStatus();
            Raise(nameof(VersionText), nameof(Language));
        }
    }
}
