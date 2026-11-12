using System;
using System.Globalization;

namespace UmbraLauncher.Core
{
    /// <summary>Human-friendly formatting of durations, dates and counts in the current UI language.</summary>
    public static class Fmt
    {
        private static bool Ru => Loc.I.IsRu;
        private static CultureInfo C => Loc.I.Culture;

        public static string Duration(double seconds)
        {
            long s = (long)Math.Round(Math.Max(0, seconds));
            long h = s / 3600, m = s % 3600 / 60;
            if (s == 0) return Ru ? "0 мин" : "0 min";
            if (h == 0 && m == 0) return Ru ? "< 1 мин" : "< 1 min";
            if (h == 0) return Ru ? $"{m} мин" : $"{m} min";
            var hours = h.ToString("N0", C);
            if (m == 0) return Ru ? $"{hours} ч" : $"{hours} h";
            return Ru ? $"{hours} ч {m} мин" : $"{hours} h {m} min";
        }

        public static string Clock(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes:00}:{t.Seconds:00}";
        }

        public static string Hours(double seconds)
        {
            double h = seconds / 3600.0;
            return h >= 100 ? h.ToString("N0", C) : h.ToString("0.0", C);
        }

        public static DateTime FromUnix(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;

        public static long ToUnix(DateTime utc) => new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeSeconds();

        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static string Relative(long unix)
        {
            if (unix <= 0) return Loc.T("Never");
            var t = FromUnix(unix);
            var days = (DateTime.Today - t.Date).Days;
            var time = t.ToString("HH:mm", C);
            if (days <= 0) return $"{Loc.T("Today")}, {time}";
            if (days == 1) return $"{Loc.T("Yesterday")}, {time}";
            if (days < 7) return Ru ? $"{days} {Plural(days, "день", "дня", "дней")} назад" : $"{days} days ago";
            return t.ToString(t.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy", C).Replace(".", "");
        }

        public static string RelativeLower(long unix)
        {
            var s = Relative(unix);
            return s.Length == 0 ? s : char.ToLower(s[0], C) + s.Substring(1);
        }

        public static string SessionDate(DateTime local)
        {
            var days = (DateTime.Today - local.Date).Days;
            if (days == 0) return $"{Loc.T("Today")}, {local:HH:mm}";
            if (days == 1) return $"{Loc.T("Yesterday")}, {local:HH:mm}";
            return local.ToString("d MMM, HH:mm", C).Replace(".", "");
        }

        public static string DayShort(DateTime d)
        {
            var s = C.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek);
            return s.Length > 0 ? char.ToUpper(s[0], C) + s.Substring(1) : s;
        }

        public static string DayLong(DateTime d) => d.ToString(Ru ? "dddd, d MMMM" : "dddd, MMMM d", C);

        public static string Plural(long n, string one, string few, string many)
        {
            n = Math.Abs(n);
            long mod100 = n % 100, mod10 = n % 10;
            if (mod100 >= 11 && mod100 <= 14) return many;
            if (mod10 == 1) return one;
            if (mod10 >= 2 && mod10 <= 4) return few;
            return many;
        }

        public static string Sessions(long n) => Ru
            ? $"{n} {Plural(n, "сессия", "сессии", "сессий")}"
            : $"{n} {(n == 1 ? "session" : "sessions")}";

        public static string Apps(long n) => Ru
            ? $"{n} {Plural(n, "приложение", "приложения", "приложений")}"
            : $"{n} {(n == 1 ? "app" : "apps")}";
    }
}
