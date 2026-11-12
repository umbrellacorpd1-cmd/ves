using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UmbraLauncher.Core;

namespace UmbraLauncher.Services
{
    public sealed class IconResult
    {
        public BitmapSource Icon;
        public Color? Dominant;
    }

    /// <summary>Extracts large (256 px) icons through the shell on a dedicated STA thread.</summary>
    public static class IconService
    {
        private static readonly BlockingCollection<Action> Queue = new BlockingCollection<Action>();
        private static readonly ConcurrentDictionary<string, IconResult> Cache = new ConcurrentDictionary<string, IconResult>(StringComparer.OrdinalIgnoreCase);
        private static Thread _worker;

        public static Task<IconResult> LoadAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(new IconResult());
            if (Cache.TryGetValue(path, out var cached)) return Task.FromResult(cached);

            EnsureWorker();
            var tcs = new TaskCompletionSource<IconResult>();
            Queue.Add(() =>
            {
                try
                {
                    var icon = File.Exists(path) ? Extract(path) : null;
                    var result = new IconResult { Icon = icon, Dominant = icon != null ? ColorUtil.Dominant(icon) : null };
                    if (icon != null) Cache[path] = result;
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    Log.Error("Icon extraction failed for " + path, ex);
                    tcs.TrySetResult(new IconResult());
                }
            });
            return tcs.Task;
        }

        public static Task<BitmapSource> LoadImageAsync(string path, int decodeWidth)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    img.DecodePixelWidth = decodeWidth;
                    img.UriSource = new Uri(path, UriKind.Absolute);
                    img.EndInit();
                    img.Freeze();
                    return (BitmapSource)img;
                }
                catch (Exception ex)
                {
                    Log.Error("Cover image could not be loaded: " + path, ex);
                    return null;
                }
            });
        }

        private static void EnsureWorker()
        {
            if (_worker != null) return;
            lock (Queue)
            {
                if (_worker != null) return;
                _worker = new Thread(() =>
                {
                    foreach (var job in Queue.GetConsumingEnumerable()) job();
                })
                {
                    IsBackground = true,
                    Name = "Icon extraction",
                };
                _worker.SetApartmentState(ApartmentState.STA);
                _worker.Start();
            }
        }

        private static BitmapSource Extract(string path)
        {
            try
            {
                var bmp = FromShell(path, 256);
                if (bmp != null) return bmp;
            }
            catch (Exception ex)
            {
                Log.Info("Shell icon unavailable for " + path + ": " + ex.Message);
            }

            using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(path))
            {
                if (ico == null) return null;
                var src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
        }

        private static BitmapSource FromShell(string path, int size)
        {
            var iid = typeof(Native.IShellItemImageFactory).GUID;
            Native.SHCreateItemFromParsingName(path, IntPtr.Zero, iid, out var factory);
            try
            {
                var hr = factory.GetImage(new Native.SIZE { cx = size, cy = size }, Native.SIIGBF_ICONONLY | Native.SIIGBF_BIGGERSIZEOK, out var hbm);
                if (hr != 0 || hbm == IntPtr.Zero) return null;
                try
                {
                    return FromDib(hbm);
                }
                finally
                {
                    Native.DeleteObject(hbm);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>Copies a 32-bit DIB section keeping its alpha channel (CreateBitmapSourceFromHBitmap drops it).</summary>
        private static BitmapSource FromDib(IntPtr hbm)
        {
            var ds = new Native.DIBSECTION();
            int got = Native.GetObject(hbm, Marshal.SizeOf(typeof(Native.DIBSECTION)), ref ds);
            if (got == 0 || ds.dsBm.bmBits == IntPtr.Zero || ds.dsBm.bmBitsPixel != 32) return null;

            int w = ds.dsBm.bmWidth, h = ds.dsBm.bmHeight, stride = ds.dsBm.bmWidthBytes;
            var px = new byte[stride * h];
            Marshal.Copy(ds.dsBm.bmBits, px, 0, px.Length);

            if (ds.dsBmih.biHeight > 0)
            {
                var row = new byte[stride];
                for (int y = 0; y < h / 2; y++)
                {
                    Buffer.BlockCopy(px, y * stride, row, 0, stride);
                    Buffer.BlockCopy(px, (h - 1 - y) * stride, px, y * stride, stride);
                    Buffer.BlockCopy(row, 0, px, (h - 1 - y) * stride, stride);
                }
            }

            bool anyAlpha = false, straight = false;
            for (int i = 0; i < px.Length; i += 4)
            {
                byte a = px[i + 3];
                if (a != 0) anyAlpha = true;
                if (px[i] > a || px[i + 1] > a || px[i + 2] > a) straight = true;
            }

            if (!anyAlpha)
            {
                for (int i = 3; i < px.Length; i += 4) px[i] = 255;
                straight = false;
            }

            var format = straight ? PixelFormats.Bgra32 : PixelFormats.Pbgra32;
            var bmp = BitmapSource.Create(w, h, 96, 96, format, null, px, stride);
            bmp.Freeze();
            return bmp;
        }
    }
}
