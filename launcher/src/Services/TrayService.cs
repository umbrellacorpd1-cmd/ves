using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using UmbraLauncher.Core;

namespace UmbraLauncher.Services
{
    public sealed class TrayApp
    {
        public string Id;
        public string Name;
        public bool Running;
        public BitmapSource Icon;
    }

    /// <summary>Notification-area icon with a dark menu listing library apps (launch straight from the tray).</summary>
    public sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly Dictionary<BitmapSource, Image> _iconCache = new Dictionary<BitmapSource, Image>();

        public TrayService()
        {
            _menu = new ContextMenuStrip
            {
                Renderer = new DarkRenderer(),
                ShowImageMargin = true,
                ImageScalingSize = new Size(16, 16),
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(4),
            };
            _menu.Opening += (s, e) => Rebuild();

            _icon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Visible = true,
                ContextMenuStrip = _menu,
                Text = "Umbra Launcher",
            };
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenRequested?.Invoke();
            };
            _icon.BalloonTipClicked += (s, e) => OpenRequested?.Invoke();
        }

        public event Action OpenRequested;
        public event Action ExitRequested;
        public event Action<string> LaunchRequested;

        public Func<IList<TrayApp>> GetApps { get; set; }
        public Func<string> GetTitle { get; set; }

        public void SetTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "Umbra Launcher";
            _icon.Text = text.Length > 63 ? text.Substring(0, 62) + "…" : text;
        }

        public void ShowBalloon(string title, string text) => _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.None);

        private static Icon LoadIcon()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            if (info == null) return SystemIcons.Application;
            using (var s = info.Stream) return new Icon(s, SystemInformation.SmallIconSize);
        }

        private void Rebuild()
        {
            _menu.Items.Clear();
            var title = GetTitle?.Invoke() ?? "Umbra";
            var open = new ToolStripMenuItem(Loc.F("TrayOpen", title), null, (s, e) => OpenRequested?.Invoke());
            open.Font = new Font(_menu.Font, FontStyle.Bold);
            _menu.Items.Add(open);
            _menu.Items.Add(new ToolStripSeparator());

            var apps = GetApps?.Invoke() ?? new List<TrayApp>();
            if (apps.Count == 0)
            {
                _menu.Items.Add(new ToolStripMenuItem(Loc.T("TrayNoApps")) { Enabled = false });
            }
            else
            {
                foreach (var a in apps)
                {
                    var id = a.Id;
                    var text = a.Running ? $"{a.Name}   ●" : a.Name;
                    _menu.Items.Add(new ToolStripMenuItem(text, ToImage(a.Icon), (s, e) => LaunchRequested?.Invoke(id)));
                }
            }

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem(Loc.T("TrayExit"), null, (s, e) => ExitRequested?.Invoke()));
        }

        private Image ToImage(BitmapSource src)
        {
            if (src == null) return null;
            if (_iconCache.TryGetValue(src, out var cached)) return cached;
            try
            {
                var small = new TransformedBitmap(src, new System.Windows.Media.ScaleTransform(32.0 / src.PixelWidth, 32.0 / src.PixelHeight));
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(small));
                using (var ms = new MemoryStream())
                {
                    enc.Save(ms);
                    ms.Position = 0;
                    using (var tmp = new Bitmap(ms))
                    {
                        var img = new Bitmap(tmp);
                        _iconCache[src] = img;
                        return img;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            foreach (var img in _iconCache.Values) img.Dispose();
            _iconCache.Clear();
        }

        private sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Color.FromArgb(242, 242, 245) : Color.FromArgb(110, 114, 123);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var r = e.Item.ContentRectangle;
                using (var pen = new Pen(Color.FromArgb(44, 44, 50)))
                    e.Graphics.DrawLine(pen, r.Left + 4, r.Height / 2, r.Right - 4, r.Height / 2);
            }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            private static readonly Color Bg = Color.FromArgb(24, 24, 29);
            private static readonly Color Hover = Color.FromArgb(40, 40, 47);
            private static readonly Color Border = Color.FromArgb(50, 50, 57);

            public override Color ToolStripDropDownBackground => Bg;
            public override Color ImageMarginGradientBegin => Bg;
            public override Color ImageMarginGradientMiddle => Bg;
            public override Color ImageMarginGradientEnd => Bg;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Hover;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
        }
    }
}
