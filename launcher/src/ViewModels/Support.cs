using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using UmbraLauncher.Core;
using UmbraLauncher.Models;

namespace UmbraLauncher.ViewModels
{
    /// <summary>A stretch of tracked time in local time; counted seconds may be less than the wall-clock span.</summary>
    public struct TimeSpanRecord
    {
        public DateTime Start;
        public DateTime End;
        public double Seconds;

        public TimeSpanRecord(DateTime start, DateTime end, double seconds)
        {
            Start = start;
            End = end;
            Seconds = seconds;
        }

        public static TimeSpanRecord From(SessionRecord s) => new TimeSpanRecord(Fmt.FromUnix(s.Start), Fmt.FromUnix(s.End), s.Seconds);
    }

    public static class StatsMath
    {
        /// <summary>Spreads each session's counted time over the calendar days it spans.</summary>
        public static double[] PerDay(IEnumerable<TimeSpanRecord> spans, DateTime firstDay, int days)
        {
            var result = new double[days];
            var lastDayEnd = firstDay.AddDays(days);
            foreach (var s in spans)
            {
                if (s.Seconds <= 0) continue;
                var start = s.Start;
                var end = s.End > s.Start ? s.End : s.Start;
                if (end < firstDay || start >= lastDayEnd) continue;

                double wall = (end - start).TotalSeconds;
                if (wall < 1)
                {
                    int i = (start.Date - firstDay).Days;
                    if (i >= 0 && i < days) result[i] += s.Seconds;
                    continue;
                }

                double ratio = s.Seconds / wall;
                var cursor = start;
                while (cursor < end)
                {
                    var next = cursor.Date.AddDays(1);
                    if (next > end) next = end;
                    int i = (cursor.Date - firstDay).Days;
                    if (i >= 0 && i < days) result[i] += (next - cursor).TotalSeconds * ratio;
                    cursor = next;
                }
            }

            return result;
        }

        public static double Between(IEnumerable<TimeSpanRecord> spans, DateTime from, DateTime to)
        {
            double total = 0;
            foreach (var s in spans)
            {
                var start = s.Start;
                var end = s.End > s.Start ? s.End : s.Start;
                double wall = (end - start).TotalSeconds;
                if (wall < 1)
                {
                    if (start >= from && start < to) total += s.Seconds;
                    continue;
                }

                var a = start > from ? start : from;
                var b = end < to ? end : to;
                if (b > a) total += (b - a).TotalSeconds * (s.Seconds / wall);
            }

            return total;
        }

        /// <summary>A rounded axis maximum (seconds) at or above the given value.</summary>
        public static double NiceMax(double seconds)
        {
            double[] steps = { 600, 900, 1800, 3600, 7200, 10800, 14400, 21600, 28800, 36000, 43200, 57600, 72000, 86400 };
            foreach (var s in steps)
                if (seconds <= s) return s;
            return Math.Ceiling(seconds / 86400) * 86400;
        }

        public static string AxisLabel(double seconds)
        {
            if (seconds <= 0) return "0";
            if (seconds < 3600) return Loc.I.IsRu ? $"{seconds / 60:0} мин" : $"{seconds / 60:0}m";
            var h = seconds / 3600;
            return Loc.I.IsRu ? $"{h.ToString("0.#", Loc.I.Culture)} ч" : $"{h:0.#}h";
        }
    }

    public sealed class DayBarViewModel
    {
        public double Height { get; set; }
        public string Label { get; set; }
        public string SubLabel { get; set; }
        public string Tooltip { get; set; }
        public bool IsToday { get; set; }
        public bool HasValue { get; set; }
        public bool ShowLabel { get; set; } = true;
    }

    public sealed class SessionRowViewModel
    {
        public string When { get; set; }
        public string Duration { get; set; }
        public string Source { get; set; }
        public bool IsLive { get; set; }
    }

    public sealed class RankRowViewModel
    {
        public int Rank { get; set; }
        public AppItemViewModel App { get; set; }
        public string TimeText { get; set; }
        public string SubText { get; set; }
        public System.Windows.GridLength BarWidth { get; set; }
        public System.Windows.GridLength RestWidth { get; set; }
    }

    public sealed class AddTileItem
    {
        public AddTileItem(ICommand addCommand) => AddCommand = addCommand;

        public ICommand AddCommand { get; }
    }

    public sealed class AccentOption : ObservableObject
    {
        private bool _isSelected;

        public AccentOption(string hex, ICommand select)
        {
            Hex = hex;
            Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            Brush.Freeze();
            SelectCommand = select;
        }

        public string Hex { get; }
        public SolidColorBrush Brush { get; }
        public ICommand SelectCommand { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }

    public sealed class ToastViewModel
    {
        public ToastViewModel(string kind, string title, string message, string actionText, Action action, Action<ToastViewModel> close)
        {
            Kind = kind;
            Title = title;
            Message = message;
            ActionText = actionText;
            CloseCommand = new RelayCommand(() => close(this));
            ActionCommand = new RelayCommand(() =>
            {
                close(this);
                action?.Invoke();
            });
        }

        public string Kind { get; }
        public string Title { get; }
        public string Message { get; }
        public bool HasMessage => !string.IsNullOrEmpty(Message);
        public string ActionText { get; }
        public bool HasAction => !string.IsNullOrEmpty(ActionText);
        public ICommand CloseCommand { get; }
        public ICommand ActionCommand { get; }
    }

    public sealed class ConfirmViewModel
    {
        public ConfirmViewModel(string title, string message, string confirmText, Action onConfirm, Action onClose)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            ConfirmCommand = new RelayCommand(() =>
            {
                onClose();
                onConfirm();
            });
            CancelCommand = new RelayCommand(onClose);
        }

        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
    }
}
