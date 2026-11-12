using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UmbraLauncher.Services
{
    public static class ColorUtil
    {
        /// <summary>Fallback hues for monochrome icons, echoing the reference sites' card colors.</summary>
        private static readonly double[] FallbackHues = { 340, 0, 262, 217, 160, 35, 190, 45 };

        public static Color Parse(string hex, Color fallback)
        {
            try
            {
                var o = ColorConverter.ConvertFromString(hex);
                return o is Color c ? c : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

        public static Color Lighten(Color c, double amount)
        {
            ToHsl(c, out var h, out var s, out var l);
            return FromHsl(h, s, Math.Min(1, l + amount));
        }

        public static Color Darken(Color c, double amount)
        {
            ToHsl(c, out var h, out var s, out var l);
            return FromHsl(h, s, Math.Max(0, l - amount));
        }

        public static double HueOf(Color c)
        {
            ToHsl(c, out var h, out _, out _);
            return h;
        }

        public static double FallbackHue(string seed)
        {
            int hash = 17;
            foreach (var ch in seed ?? string.Empty) hash = unchecked(hash * 31 + char.ToLowerInvariant(ch));
            return FallbackHues[(hash & 0x7fffffff) % FallbackHues.Length];
        }

        /// <summary>Weighted average hue of the icon's saturated pixels; null for grey/monochrome icons.</summary>
        public static Color? Dominant(BitmapSource src)
        {
            if (src == null || src.PixelWidth == 0) return null;
            BitmapSource small = new TransformedBitmap(src, new ScaleTransform(32.0 / src.PixelWidth, 32.0 / src.PixelHeight));
            small = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            int w = small.PixelWidth, h = small.PixelHeight;
            var px = new byte[w * h * 4];
            small.CopyPixels(px, w * 4, 0);

            double x = 0, y = 0, total = 0, satSum = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                double a = px[i + 3] / 255.0;
                if (a < 0.5) continue;
                var c = Color.FromRgb(px[i + 2], px[i + 1], px[i]);
                ToHsl(c, out var hue, out var sat, out var lum);
                if (lum < 0.12 || lum > 0.94 || sat < 0.18) continue;
                double weight = a * sat * sat * (1.0 - Math.Abs(lum - 0.5));
                x += Math.Cos(hue * Math.PI / 180) * weight;
                y += Math.Sin(hue * Math.PI / 180) * weight;
                satSum += sat * weight;
                total += weight;
            }

            if (total < 4) return null;
            double hDeg = Math.Atan2(y, x) * 180 / Math.PI;
            if (hDeg < 0) hDeg += 360;
            double s = Math.Max(0.5, Math.Min(0.85, satSum / total));
            return FromHsl(hDeg, s, 0.52);
        }

        public static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (Math.Abs(max - min) < 1e-9)
            {
                h = 0;
                s = 0;
                return;
            }

            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        public static Color FromHsl(double h, double s, double l, byte a = 255)
        {
            h = ((h % 360) + 360) % 360 / 360.0;
            double r, g, b;
            if (s <= 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3);
            }

            return Color.FromArgb(a, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        public static T Frozen<T>(T f) where T : Freezable
        {
            f.Freeze();
            return f;
        }

        /// <summary>uc.zone-style product card: a saturated diagonal gradient in the app's own color.</summary>
        public static Brush CardBrush(double hue, double sat)
        {
            // Darkened yellows turn olive; lean their shadow toward orange instead.
            double h = ((hue % 360) + 360) % 360;
            double endHue = h > 35 && h < 80 ? h - 22 : h + 14;
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            b.GradientStops.Add(new GradientStop(FromHsl(h - 6, sat * 0.85, 0.60), 0));
            b.GradientStops.Add(new GradientStop(FromHsl(h, sat * 0.9, 0.45), 0.45));
            b.GradientStops.Add(new GradientStop(FromHsl(endHue, Math.Min(1, sat * 1.05), 0.26), 1));
            return Frozen(b);
        }

        public static Brush GlowBrush(Color c, double cx, double cy, byte alpha, double rx, double ry)
        {
            var b = new RadialGradientBrush
            {
                Center = new Point(cx, cy),
                GradientOrigin = new Point(cx, cy),
                RadiusX = rx,
                RadiusY = ry,
            };
            b.GradientStops.Add(new GradientStop(WithAlpha(c, alpha), 0));
            b.GradientStops.Add(new GradientStop(WithAlpha(c, (byte)(alpha * 0.35)), 0.45));
            b.GradientStops.Add(new GradientStop(WithAlpha(c, 0), 1));
            return Frozen(b);
        }
    }
}
