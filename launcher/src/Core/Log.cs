using System;
using System.IO;
using System.Text;

namespace UmbraLauncher.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _file;

        public static void Init(string dataDir)
        {
            try
            {
                var dir = Path.Combine(dataDir, "logs");
                Directory.CreateDirectory(dir);
                _file = Path.Combine(dir, "launcher.log");
                var fi = new FileInfo(_file);
                if (fi.Exists && fi.Length > 1024 * 1024)
                    File.Copy(_file, Path.Combine(dir, "launcher.old.log"), true);
                if (fi.Exists && fi.Length > 1024 * 1024) File.Delete(_file);
            }
            catch
            {
                _file = null;
            }
        }

        public static void Info(string message) => Write("INFO ", message);

        public static void Error(string message, Exception ex = null)
            => Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);

        private static void Write(string level, string message)
        {
            if (_file == null) return;
            try
            {
                lock (Gate)
                    File.AppendAllText(_file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch
            {
                // Logging must never take the launcher down.
            }
        }
    }
}
