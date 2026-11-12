using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UmbraLauncher.Core;
using UmbraLauncher.Models;
using UmbraLauncher.Services;

namespace UmbraLauncher.ViewModels
{
    public sealed class AppItemViewModel : ObservableObject, ITrackedApp
    {
        private readonly MainViewModel _main;
        private TrackedSession _session;
        private double _baseSeconds;
        private double _weekBase;
        private double _longest;
        private int _count;
        private BitmapSource _icon;
        private BitmapSource _cover;
        private Brush _cardBrush;
        private Brush _heroGlowBrush;
        private Brush _drawerGlowBrush;
        private Brush _colorBrush;
        private Brush _softBrush;
        private Brush _badgeTextBrush;
        private Color _glowColor;
        private bool _isMissing;
        private int _loadToken;

        public AppItemViewModel(MainViewModel main, AppEntry entry)
        {
            _main = main;
            Entry = entry;
            LaunchCommand = new RelayCommand(() => _main.Launch(this));
            LaunchAdminCommand = new RelayCommand(() => _main.Launch(this, true));
            DetailsCommand = new RelayCommand(() => _main.OpenDetails(this));
            FocusCommand = new RelayCommand(() => _main.FocusApp(this));
            CloseAppCommand = new RelayCommand(() => _main.CloseApp(this));
            EditCommand = new RelayCommand(() => _main.Edit(this));
            RemoveCommand = new RelayCommand(() => _main.AskRemove(this));
            OpenFolderCommand = new RelayCommand(() => ShellHelpers.RevealInExplorer(Entry.Path));
            Refresh();
        }

        public AppEntry Entry { get; }
        public string Id => Entry.Id;
        public AppMatcher Matcher { get; private set; }

        public ICommand LaunchCommand { get; }
        public ICommand LaunchAdminCommand { get; }
        public ICommand DetailsCommand { get; }
        public ICommand FocusCommand { get; }
        public ICommand CloseAppCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand OpenFolderCommand { get; }

        public string Name => Entry.Name;
        public string Path => Entry.Path;
        public string AutomationId => "Card_" + Safe(Entry.Name);
        public string PlayAutomationId => "Play_" + Safe(Entry.Name);
        public bool RunAsAdmin => Entry.RunAsAdmin;

        public BitmapSource Icon
        {
            get => _icon;
            private set
            {
                if (Set(ref _icon, value)) Raise(nameof(HasIcon), nameof(ShowFallbackGlyph), nameof(ShowCenterIcon));
            }
        }

        public bool HasIcon => _icon != null;
        public bool ShowFallbackGlyph => _icon == null && _cover == null;

        public BitmapSource Cover
        {
            get => _cover;
            private set
            {
                if (Set(ref _cover, value)) Raise(nameof(HasCover), nameof(ShowCenterIcon), nameof(ShowFallbackGlyph));
            }
        }

        public bool HasCover => _cover != null;
        public bool ShowCenterIcon => _cover == null && _icon != null;

        public Brush CardBrush { get => _cardBrush; private set => Set(ref _cardBrush, value); }
        public Brush HeroGlowBrush { get => _heroGlowBrush; private set => Set(ref _heroGlowBrush, value); }
        public Brush DrawerGlowBrush { get => _drawerGlowBrush; private set => Set(ref _drawerGlowBrush, value); }
        public Brush ColorBrush { get => _colorBrush; private set => Set(ref _colorBrush, value); }
        public Brush SoftBrush { get => _softBrush; private set => Set(ref _softBrush, value); }
        public Brush BadgeTextBrush { get => _badgeTextBrush; private set => Set(ref _badgeTextBrush, value); }
        public Color GlowColor { get => _glowColor; private set => Set(ref _glowColor, value); }

        public bool IsMissing
        {
            get => _isMissing;
            private set => Set(ref _isMissing, value);
        }

        public bool IsRunning => _session != null;
        public double LiveSeconds => _session?.Seconds ?? 0;
        public string LiveClock => Fmt.Clock(LiveSeconds);
        public string RunningLabel => $"{Loc.T("Running")} · {LiveClock}";
        public double TotalSeconds => _baseSeconds + LiveSeconds;
        public string TotalText => Fmt.Duration(TotalSeconds);
        public string HoursBadge => $"{Fmt.Hours(TotalSeconds)} {Loc.T("HoursUnit")}";
        public bool HasPlaytime => TotalSeconds >= 60;
        public int SessionCount => _count + (IsRunning ? 1 : 0);
        public string SessionCountText => SessionCount.ToString("N0", Loc.I.Culture);
        public string LastPlayedText => IsRunning ? Loc.T("LiveNow") : Fmt.Relative(Entry.LastPlayedAt);
        public string AvgText => SessionCount == 0 ? "—" : Fmt.Duration(TotalSeconds / SessionCount);
        public string WeekText => Fmt.Duration(_weekBase + LiveSeconds);
        public string LongestText => Fmt.Duration(Math.Max(_longest, LiveSeconds));

        public string CardSubtitle => IsRunning
            ? Loc.F("RunningFor", LiveClock)
            : Entry.LastPlayedAt > 0 ? Fmt.Relative(Entry.LastPlayedAt) : Loc.T("NeverLaunched");

        public string HeroMeta => Entry.LastPlayedAt > 0
            ? $"{Loc.F("LastLaunched", Fmt.RelativeLower(Entry.LastPlayedAt))}   ·   {Fmt.Sessions(SessionCount)}"
            : Loc.T("NeverLaunched");

        public string HeroBadge => Entry.LastPlayedAt > 0 ? Loc.T("HeroContinue") : Loc.T("HeroReady");

        public bool IsNew => Entry.LaunchCount == 0 && _count == 0 && !IsRunning && Fmt.NowUnix() - Entry.AddedAt < 3 * 86400;

        public ObservableCollection<DayBarViewModel> Days { get; } = new ObservableCollection<DayBarViewModel>();
        public ObservableCollection<SessionRowViewModel> RecentSessions { get; } = new ObservableCollection<SessionRowViewModel>();
        public bool HasSessions => RecentSessions.Count > 0;

        /// <summary>Re-reads the entry after it was added or edited.</summary>
        public void Refresh()
        {
            Matcher = AppMatcher.Create(Entry.Path, Entry.ExtraProcesses);
            IsMissing = !File.Exists(Entry.Path);
            ApplyColor(null);
            RecalcStats();
            Raise(nameof(Name), nameof(Path), nameof(AutomationId), nameof(PlayAutomationId), nameof(RunAsAdmin));
            LoadVisualsAsync();
        }

        private async void LoadVisualsAsync()
        {
            int token = ++_loadToken;
            var iconTask = IconService.LoadAsync(Entry.Path);
            var coverTask = IconService.LoadImageAsync(Entry.CoverPath, 900);
            var result = await iconTask;
            if (token != _loadToken) return;
            Icon = result.Icon;
            ApplyColor(result.Dominant);
            var cover = await coverTask;
            if (token != _loadToken) return;
            Cover = cover;
        }

        public void CheckMissing() => IsMissing = !File.Exists(Entry.Path);

        private void ApplyColor(Color? dominant)
        {
            double hue, sat;
            if (dominant.HasValue)
            {
                ColorUtil.ToHsl(dominant.Value, out hue, out sat, out _);
                sat = Math.Max(0.45, Math.Min(0.8, sat));
            }
            else
            {
                hue = ColorUtil.FallbackHue(Entry.Name);
                sat = 0.62;
            }

            var main = ColorUtil.FromHsl(hue, sat, 0.56);
            GlowColor = main;
            CardBrush = ColorUtil.CardBrush(hue, sat);
            HeroGlowBrush = ColorUtil.GlowBrush(main, 0.80, 0.32, 0x9E, 0.55, 1.05);
            DrawerGlowBrush = ColorUtil.GlowBrush(main, 0.5, 0.0, 0x70, 0.95, 0.95);
            ColorBrush = ColorUtil.Frozen(new SolidColorBrush(main));
            SoftBrush = ColorUtil.Frozen(new SolidColorBrush(ColorUtil.WithAlpha(main, 0x2A)));
            BadgeTextBrush = ColorUtil.Frozen(new SolidColorBrush(ColorUtil.FromHsl(hue, Math.Min(0.9, sat + 0.1), 0.30)));
        }

        public IEnumerable<TimeSpanRecord> Spans()
        {
            foreach (var s in Entry.Sessions) yield return TimeSpanRecord.From(s);
            if (_session != null) yield return new TimeSpanRecord(_session.StartUtc.ToLocalTime(), DateTime.Now, _session.Seconds);
        }

        public void RecalcStats()
        {
            _baseSeconds = Entry.Sessions.Sum(s => (double)s.Seconds);
            _count = Entry.Sessions.Count;
            _longest = _count == 0 ? 0 : Entry.Sessions.Max(s => (double)s.Seconds);
            var now = DateTime.Now;
            _weekBase = StatsMath.Between(Entry.Sessions.Select(TimeSpanRecord.From), now.Date.AddDays(-6), now.AddMinutes(1));
            RaiseStats();
        }

        public void SetSession(TrackedSession session)
        {
            if (ReferenceEquals(_session, session)) return;
            _session = session;
            OnPropertyChanged(nameof(IsRunning));
            RaiseStats();
        }

        /// <summary>Called every tracker tick while running so timers on screen move.</summary>
        public void UpdateLive()
        {
            Raise(nameof(LiveSeconds), nameof(LiveClock), nameof(RunningLabel), nameof(TotalSeconds), nameof(TotalText),
                nameof(HoursBadge), nameof(CardSubtitle), nameof(WeekText), nameof(LongestText), nameof(AvgText), nameof(HasPlaytime));
        }

        public void RaiseStats()
        {
            Raise(nameof(LiveSeconds), nameof(LiveClock), nameof(RunningLabel), nameof(TotalSeconds), nameof(TotalText),
                nameof(HoursBadge), nameof(HasPlaytime), nameof(SessionCount), nameof(SessionCountText), nameof(LastPlayedText),
                nameof(AvgText), nameof(WeekText), nameof(LongestText), nameof(CardSubtitle), nameof(HeroMeta), nameof(HeroBadge), nameof(IsNew));
        }

        /// <summary>Builds the drawer's 14-day chart and session history.</summary>
        public void LoadDetails()
        {
            const int days = 14;
            var first = DateTime.Today.AddDays(-(days - 1));
            var values = StatsMath.PerDay(Spans(), first, days);
            double max = Math.Max(values.Max(), 1);
            Days.Clear();
            for (int i = 0; i < days; i++)
            {
                var day = first.AddDays(i);
                Days.Add(new DayBarViewModel
                {
                    Height = values[i] <= 0 ? 3 : Math.Max(5, values[i] / max * 86),
                    HasValue = values[i] > 0,
                    IsToday = i == days - 1,
                    Label = Fmt.DayShort(day).Substring(0, Math.Min(2, Fmt.DayShort(day).Length)),
                    Tooltip = $"{Fmt.DayLong(day)} — {Fmt.Duration(values[i])}",
                });
            }

            RecentSessions.Clear();
            if (_session != null)
            {
                RecentSessions.Add(new SessionRowViewModel
                {
                    When = Fmt.SessionDate(_session.StartUtc.ToLocalTime()),
                    Duration = LiveClock,
                    Source = Loc.T("LiveNow"),
                    IsLive = true,
                });
            }

            foreach (var s in Entry.Sessions.OrderByDescending(x => x.Start).Take(8))
            {
                RecentSessions.Add(new SessionRowViewModel
                {
                    When = Fmt.SessionDate(Fmt.FromUnix(s.Start)),
                    Duration = Fmt.Duration(s.Seconds),
                    Source = s.Detected ? Loc.T("SourceDetected") : Loc.T("SourceLauncher"),
                });
            }

            OnPropertyChanged(nameof(HasSessions));
        }

        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "App";
            var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            return new string(chars);
        }
    }
}
