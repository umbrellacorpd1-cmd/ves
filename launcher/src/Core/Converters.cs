using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UmbraLauncher.Core
{
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && (v == Visibility.Visible) != Invert;
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value != null && !(value is string s && s.Length == 0);
            if (Invert) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    /// <summary>Two-way "value == parameter" for radio-button style selectors.</summary>
    public sealed class EqualsToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool b) || !b) return Binding.DoNothing;
            if (targetType == typeof(int) && int.TryParse(parameter?.ToString(), out var i)) return i;
            return parameter?.ToString();
        }
    }

    public sealed class EqualsToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool eq = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
            if (Invert) eq = !eq;
            return eq ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    /// <summary>Responsive card grid: how many columns of at least N px fit.</summary>
    public sealed class WidthToColumnsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double width = value is double d ? d : 0;
            double min = 220;
            if (parameter != null) double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out min);
            if (width <= 0) return 4;
            return Math.Max(2, (int)Math.Floor(width / min));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long n = 0;
            if (value is int i) n = i;
            else if (value is long l) n = l;
            else if (value is double dd) n = (long)dd;
            bool visible = n > 0;
            if (Invert) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
