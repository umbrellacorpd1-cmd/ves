using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace AceGLauncher
{
    /// <summary>Win32 helpers. Every call is guarded so the launcher degrades gracefully if one is unavailable.</summary>
    internal static class Native
    {
        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int PrivateExtractIcons(string file, int index, int cx, int cy,
            IntPtr[] icons, int[] ids, int count, int flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr h);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Full image path of a process; works for elevated and 32/64-bit processes. Null if denied.</summary>
        public static string GetProcessPath(Process p)
        {
            try
            {
                IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                if (h == IntPtr.Zero) return null;
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
                }
                finally { CloseHandle(h); }
            }
            catch (Exception)
            {
                try { return p.MainModule.FileName; } catch (Exception) { return null; }
            }
        }

        /// <summary>Extracts the exe's icon at a large size (the stock API only gives 32px).</summary>
        public static Bitmap ExtractLargeIcon(string file, int size)
        {
            try
            {
                var handles = new IntPtr[1];
                var ids = new int[1];
                int n = PrivateExtractIcons(file, 0, size, size, handles, ids, 1, 0);
                if (n <= 0 || handles[0] == IntPtr.Zero) return null;
                try
                {
                    using (Icon ico = Icon.FromHandle(handles[0]))
                        return ico.ToBitmap();
                }
                finally { DestroyIcon(handles[0]); }
            }
            catch (Exception) { return null; }
        }
    }
}
