using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using UmbraLauncher.Core;

namespace UmbraLauncher.Services
{
    /// <summary>Swaps the accent-dependent brushes at runtime; XAML references them with DynamicResource.</summary>
    public static class ThemeService
    {
        public static readonly string[] Accents =
        {
            "#E81C5A", // Majestic pink
            "#CE3535", // Umbrella red
            "#FF6A2B", // sunset orange
            "#8B5CF6", // violet
            "#2F7BFF", // electric blue
            "#12B886", // mint
        };

        public static Color Accent { get; private set; } = Color.FromRgb(0xE8, 0x1C, 0x5A);

        public static void ApplyAccent(string hex)
        {
            var c = ColorUtil.Parse(hex, Color.FromRgb(0xE8, 0x1C, 0x5A));
            Accent = c;
            var r = Application.Current.Resources;
            r["C.Accent"] = c;
            r["B.Accent"] = Solid(c);
            r["B.AccentHover"] = Solid(ColorUtil.Lighten(c, 0.07));
            r["B.AccentText"] = Solid(ColorUtil.Lighten(c, 0.12));
            r["B.AccentSoft"] = Solid(ColorUtil.WithAlpha(c, 0x24));
            r["B.AccentSoftStrong"] = Solid(ColorUtil.WithAlpha(c, 0x40));
            r["B.AccentRing"] = Solid(ColorUtil.WithAlpha(c, 0x30));
            r["B.AccentLine"] = Solid(ColorUtil.WithAlpha(c, 0x8C));

            var diag = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            diag.GradientStops.Add(new GradientStop(ColorUtil.Lighten(c, 0.10), 0));
            diag.GradientStops.Add(new GradientStop(ColorUtil.Darken(c, 0.14), 1));
            r["B.AccentGradient"] = ColorUtil.Frozen(diag);

            // Majestic's highlighted hero card: deep magenta at the top fading into the bright accent.
            var vert = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
            vert.GradientStops.Add(new GradientStop(ColorUtil.Darken(c, 0.30), 0));
            vert.GradientStops.Add(new GradientStop(ColorUtil.Darken(c, 0.08), 0.45));
            vert.GradientStops.Add(new GradientStop(ColorUtil.Lighten(c, 0.04), 1));
            r["B.AccentCard"] = ColorUtil.Frozen(vert);

            r["B.AmbientGlow"] = ColorUtil.GlowBrush(c, 0.5, 0.5, 0x2E, 0.5, 0.5);
            r["B.ButtonGlow"] = ColorUtil.GlowBrush(c, 0.5, 0.5, 0x99, 0.5, 0.5);
        }

        private static SolidColorBrush Solid(Color c) => ColorUtil.Frozen(new SolidColorBrush(c));
    }

    /// <summary>Dark title-bar integration for the custom chrome (Windows 10 20H1+ / Windows 11).</summary>
    public static class WindowEffects
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;

        public static void Apply(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int on = 1;
                Native.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
                int round = 2;
                Native.DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
                int border = 0x00241F1F; // COLORREF 0x00BBGGRR -> #1F1F24
                Native.DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
                int caption = 0x00100D0D;
                Native.DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            }
            catch (Exception ex)
            {
                Log.Info("DWM attributes unavailable: " + ex.Message);
            }
        }

        /// <summary>A maximized custom-chrome window overhangs the screen by the frame size; this is that size in DIPs.</summary>
        public static Thickness MaximizedOverhang(Window window)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                int x = Native.GetSystemMetrics(Native.SM_CXSIZEFRAME) + Native.GetSystemMetrics(Native.SM_CXPADDEDBORDER);
                int y = Native.GetSystemMetrics(Native.SM_CYSIZEFRAME) + Native.GetSystemMetrics(Native.SM_CXPADDEDBORDER);
                return new Thickness(x / dpi.DpiScaleX, y / dpi.DpiScaleY, x / dpi.DpiScaleX, y / dpi.DpiScaleY);
            }
            catch
            {
                return new Thickness(7);
            }
        }
    }
}
