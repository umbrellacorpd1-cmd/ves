using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;
using UmbraLauncher.Core;

namespace UmbraLauncher.Services
{
    public sealed class ShortcutInfo
    {
        public string Target;
        public string Arguments;
        public string WorkingDirectory;
    }

    public static class ShellHelpers
    {
        /// <summary>Resolves a .lnk to its target without external libraries (IShellLinkW).</summary>
        public static ShortcutInfo ResolveShortcut(string lnkPath)
        {
            object link = null;
            try
            {
                link = new Native.ShellLink();
                ((IPersistFile)link).Load(lnkPath, 0);
                var shell = (Native.IShellLinkW)link;
                var sb = new StringBuilder(1024);
                shell.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                var target = sb.ToString();
                sb.Clear();
                shell.GetArguments(sb, sb.Capacity);
                var args = sb.ToString();
                sb.Clear();
                shell.GetWorkingDirectory(sb, sb.Capacity);
                return new ShortcutInfo { Target = target, Arguments = args, WorkingDirectory = sb.ToString() };
            }
            catch (Exception ex)
            {
                Log.Error("Could not resolve shortcut " + lnkPath, ex);
                return null;
            }
            finally
            {
                if (link != null) Marshal.ReleaseComObject(link);
            }
        }

        /// <summary>Best display name for an executable: file description, product name, then file name.</summary>
        public static string SuggestName(string exePath)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                foreach (var candidate in new[] { info.FileDescription, info.ProductName })
                {
                    var c = candidate?.Trim();
                    if (!string.IsNullOrEmpty(c) && c.Length <= 48 && !c.Equals("launcher", StringComparison.OrdinalIgnoreCase)) return c;
                }
            }
            catch
            {
                // Some files have no version resource; fall through.
            }

            return Path.GetFileNameWithoutExtension(exePath);
        }

        public static void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
                else if (Directory.Exists(Path.GetDirectoryName(path) ?? string.Empty)) Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(path)}\"")?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Could not open Explorer", ex);
            }
        }

        public static void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", $"\"{dir}\"")?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Could not open folder", ex);
            }
        }
    }

    /// <summary>Per-user autostart through HKCU\...\Run (no admin rights needed).</summary>
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "UmbraLauncher";

        private static string Command => $"\"{Process.GetCurrentProcess().MainModule?.FileName}\" --tray";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return key?.GetValue(ValueName) is string;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(ValueName, Command);
                    else key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not update autostart", ex);
            }
        }

        /// <summary>Keeps the stored path current if the exe was moved.</summary>
        public static void Refresh()
        {
            if (IsEnabled) SetEnabled(true);
        }
    }
}
