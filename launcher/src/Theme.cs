using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AceGLauncher
{
    /// <summary>Palette taken from majestic-rp.ru (crimson accent, near-black) and uc.zone (zinc surfaces, pill badges).</summary>
    public static class Theme
    {
        public static readonly Color Bg = ColorTranslator.FromHtml("#0e0e0e");
        public static readonly Color Sidebar = ColorTranslator.FromHtml("#0b0b0d");
        public static readonly Color Surface = ColorTranslator.FromHtml("#151517");
        public static readonly Color Surface2 = ColorTranslator.FromHtml("#1a1a1e");
        public static readonly Color SurfaceHover = ColorTranslator.FromHtml("#222227");
        public static readonly Color Line = ColorTranslator.FromHtml("#232327");
        public static readonly Color LineStrong = ColorTranslator.FromHtml("#3d3c3e");
        public static readonly Color Text = ColorTranslator.FromHtml("#fffcfc");
        public static readonly Color TextSoft = ColorTranslator.FromHtml("#e4e4e7");
        public static readonly Color Muted = ColorTranslator.FromHtml("#afb3ba");
        public static readonly Color Dim = ColorTranslator.FromHtml("#6b737b");
        public static readonly Color Accent = ColorTranslator.FromHtml("#e81c5a");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#ff2f6d");
        public static readonly Color Accent2 = ColorTranslator.FromHtml("#ff6cbd");
        public static readonly Color Red = ColorTranslator.FromHtml("#ce3535");
        public static readonly Color Green = ColorTranslator.FromHtml("#2fd27a");

        public static float Scale = 1f;
        public static int S(float px) { return (int)Math.Round(px * Scale); }

        public static Font Display(float pt) { return MakeFont(pt, FontStyle.Bold, "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Black", "Segoe UI"); }
        public static Font Body(float pt) { return MakeFont(pt, FontStyle.Regular, "Segoe UI Variable Text", "Segoe UI", "Inter"); }
        public static Font Semi(float pt) { return MakeFont(pt, FontStyle.Bold, "Segoe UI Semibold", "Segoe UI", "Inter"); }

        private static Font MakeFont(float pt, FontStyle style, params string[] families)
        {
            using (var installed = new InstalledFontCollection())
            {
                foreach (string name in families)
                    foreach (FontFamily f in installed.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            // Semibold/SemiBold families are already heavy; don't double-bold them.
                            FontStyle st = name.IndexOf("Semi", StringComparison.OrdinalIgnoreCase) >= 0 ? FontStyle.Regular : style;
                            if (!f.IsStyleAvailable(st)) st = FontStyle.Regular;
                            return new Font(f, pt, st, GraphicsUnit.Point);
                        }
            }
            return new Font(FontFamily.GenericSansSerif, pt, style, GraphicsUnit.Point);
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c); }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Brush b)
        {
            using (var path = Round(r, radius)) g.FillPath(b, path);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color c, float width)
        {
            using (var path = Round(r, radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, path);
        }

        /// <summary>Soft radial glow, the signature background effect on both reference sites.</summary>
        public static void Glow(Graphics g, Rectangle r, Color c, int alpha)
        {
            if (r.Width < 2 || r.Height < 2) return;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using (var b = new PathGradientBrush(path))
                {
                    b.CenterColor = Alpha(c, alpha);
                    b.SurroundColors = new[] { Alpha(c, 0) };
                    g.FillEllipse(b, r);
                }
            }
        }

        public static LinearGradientBrush AccentBrush(Rectangle r, bool hover)
        {
            Rectangle rr = new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
            return new LinearGradientBrush(rr,
                hover ? AccentHover : Accent,
                hover ? Mix(AccentHover, Accent2, 0.45f) : Mix(Accent, Accent2, 0.3f),
                LinearGradientMode.ForwardDiagonal);
        }

        public static void Draw(Graphics g, string s, Font f, Color c, Rectangle r, StringAlignment h, StringAlignment v)
        {
            using (var fmt = new StringFormat(StringFormatFlags.NoWrap))
            using (var b = new SolidBrush(c))
            {
                fmt.Alignment = h;
                fmt.LineAlignment = v;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(s, f, b, r, fmt);
            }
        }

        public static void Draw(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Draw(g, s, f, c, r, StringAlignment.Near, StringAlignment.Center);
        }

        public static string Hours(double seconds)
        {
            double h = seconds / 3600.0;
            if (h >= 100) return h.ToString("0") + " h";
            if (h >= 1) return h.ToString("0.0") + " h";
            int m = (int)(seconds / 60);
            return m + " min";
        }

        public static string Clock(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        public static string Ago(DateTime utc)
        {
            if (utc == DateTime.MinValue) return "Never played";
            TimeSpan d = DateTime.UtcNow - utc;
            if (d.TotalMinutes < 2) return "Just now";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + " min ago";
            if (d.TotalDays < 1) return (int)d.TotalHours + " h ago";
            if (d.TotalDays < 2) return "Yesterday";
            if (d.TotalDays < 30) return (int)d.TotalDays + " days ago";
            return utc.ToLocalTime().ToString("dd MMM yyyy");
        }
    }
}
