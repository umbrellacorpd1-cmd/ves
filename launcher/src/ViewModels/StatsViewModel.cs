using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UmbraLauncher.Core;

namespace UmbraLauncher.ViewModels
{
    public sealed class StatsViewModel : ObservableObject
    {
        private const double ChartHeight = 190;
        private readonly MainViewModel _main;

        public StatsViewModel(MainViewModel main) => _main = main;

        public ObservableCollection<DayBarViewModel> Bars { get; } = new ObservableCollection<DayBarViewModel>();
        public ObservableCollection<RankRowViewModel> Ranking { get; } = new ObservableCollection<RankRowViewModel>();

        public string TotalText { get; private set; }
        public string TotalSub { get; private set; }
        public string WeekText { get; private set; }
        public string WeekSub { get; private set; }
        public bool WeekUp { get; private set; }
        public string TodayText { get; private set; }
        public string TodaySub { get; private set; }
        public string TopName { get; private set; }
        public string TopText { get; private set; }
        public BitmapSource TopIcon { get; private set; }
        public Brush TopBrush { get; private set; }
        public string AxisTop { get; private set; }
        public string AxisMid { get; private set; }
        public bool HasData { get; private set; }
        public bool HasChartData { get; private set; }
        public int Columns { get; private set; } = 14;

        public string ChartTitle => $"{Loc.T("ChartTitle")} · {Loc.T("Range" + _main.StatsRange)}".ToUpper(Loc.I.Culture);

        public void Recompute()
        {
            var apps = _main.Apps.ToList();
            var today = DateTime.Today;
            var now = DateTime.Now;

            double total = apps.Sum(a => a.TotalSeconds);
            int sessions = apps.Sum(a => a.SessionCount);
            TotalText = Fmt.Duration(total);
            TotalSub = $"{Fmt.Sessions(sessions)} · {Fmt.Apps(apps.Count)}";

            var allSpans = apps.SelectMany(a => a.Spans()).ToList();
            double week = StatsMath.Between(allSpans, today.AddDays(-6), now.AddMinutes(1));
            double prevWeek = StatsMath.Between(allSpans, today.AddDays(-13), today.AddDays(-6));
            WeekText = Fmt.Duration(week);
            if (prevWeek < 60)
            {
                WeekSub = week >= 60 ? Loc.T("KpiWeekNew") : "—";
                WeekUp = week >= 60;
            }
            else
            {
                double delta = (week - prevWeek) / prevWeek * 100;
                WeekUp = delta >= 0;
                WeekSub = Math.Abs(delta) < 1
                    ? Loc.T("KpiWeekSame")
                    : Loc.F("KpiWeekDelta", (delta > 0 ? "+" : "−") + Math.Abs(delta).ToString("0", Loc.I.Culture) + "%");
            }

            double todaySeconds = StatsMath.Between(allSpans, today, now.AddMinutes(1));
            int todaySessions = allSpans.Count(s => s.End >= today && s.Seconds > 0);
            TodayText = Fmt.Duration(todaySeconds);
            TodaySub = Fmt.Sessions(todaySessions);

            var top = apps.Where(a => a.TotalSeconds >= 1).OrderByDescending(a => a.TotalSeconds).FirstOrDefault();
            TopName = top?.Name ?? Loc.T("KpiTopNone");
            TopText = top == null ? "—" : top.TotalText;
            TopIcon = top?.Icon;
            TopBrush = top?.SoftBrush;

            // Activity chart
            int range = _main.StatsRange;
            Columns = range;
            var first = today.AddDays(-(range - 1));
            var values = StatsMath.PerDay(allSpans, first, range);
            double peak = values.Length == 0 ? 0 : values.Max();
            double axis = StatsMath.NiceMax(Math.Max(peak, 1));
            AxisTop = StatsMath.AxisLabel(axis);
            AxisMid = StatsMath.AxisLabel(axis / 2);
            HasChartData = peak > 0;
            Bars.Clear();
            for (int i = 0; i < range; i++)
            {
                var day = first.AddDays(i);
                Bars.Add(new DayBarViewModel
                {
                    Height = values[i] <= 0 ? 3 : Math.Max(6, values[i] / axis * ChartHeight),
                    HasValue = values[i] > 0,
                    IsToday = i == range - 1,
                    Label = range > 14 ? day.Day.ToString(Loc.I.Culture) : Fmt.DayShort(day),
                    SubLabel = range > 14 ? string.Empty : day.Day.ToString(Loc.I.Culture),
                    ShowLabel = range <= 14 || i % 2 == (range - 1) % 2,
                    Tooltip = $"{Fmt.DayLong(day)} — {Fmt.Duration(values[i])}",
                });
            }

            // Ranking
            Ranking.Clear();
            var ranked = apps.Where(a => a.TotalSeconds >= 1).OrderByDescending(a => a.TotalSeconds).Take(10).ToList();
            double best = ranked.Count == 0 ? 1 : Math.Max(1, ranked[0].TotalSeconds);
            int rank = 1;
            foreach (var a in ranked)
            {
                double f = Math.Max(0.02, a.TotalSeconds / best);
                Ranking.Add(new RankRowViewModel
                {
                    Rank = rank++,
                    App = a,
                    TimeText = a.TotalText,
                    SubText = $"{Fmt.Sessions(a.SessionCount)} · {a.LastPlayedText}",
                    BarWidth = new GridLength(f, GridUnitType.Star),
                    RestWidth = new GridLength(1 - f + 0.0001, GridUnitType.Star),
                });
            }

            HasData = ranked.Count > 0;
            Raise(nameof(TotalText), nameof(TotalSub), nameof(WeekText), nameof(WeekSub), nameof(WeekUp), nameof(TodayText),
                nameof(TodaySub), nameof(TopName), nameof(TopText), nameof(TopIcon), nameof(TopBrush), nameof(AxisTop), nameof(AxisMid),
                nameof(HasData), nameof(HasChartData), nameof(Columns), nameof(ChartTitle));
        }
    }
}
