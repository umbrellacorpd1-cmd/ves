using System;
using System.Collections.Generic;
using System.IO;

namespace UmbraLauncher.Services
{
    /// <summary>Identifies a library app's executable, install folder and extra process names for the tracker.</summary>
    public sealed class AppMatcher
    {
        private static HashSet<string> _sharedDirs;

        private AppMatcher()
        {
        }

        public string ExePathLower { get; private set; }
        public string ExeNameLower { get; private set; }
        /// <summary>The app's install folder (with trailing slash), or null when the folder is shared by unrelated programs.</summary>
        public string DirLower { get; private set; }
        public HashSet<string> Extra { get; private set; }

        public static AppMatcher Create(string exePath, string extraProcesses)
        {
            var m = new AppMatcher { Extra = new HashSet<string>(StringComparer.Ordinal) };
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                string full;
                try { full = Path.GetFullPath(exePath); }
                catch { full = exePath; }
                m.ExePathLower = full.ToLowerInvariant();
                m.ExeNameLower = Path.GetFileName(m.ExePathLower);

                var dir = Path.GetDirectoryName(m.ExePathLower);
                if (!string.IsNullOrEmpty(dir))
                {
                    dir = dir.TrimEnd('\\') + "\\";
                    if (!IsShared(dir)) m.DirLower = dir;
                }
            }

            if (!string.IsNullOrWhiteSpace(extraProcesses))
            {
                foreach (var raw in extraProcesses.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = raw.Trim().Trim('"').ToLowerInvariant();
                    if (name.Length == 0) continue;
                    name = Path.GetFileName(name);
                    if (!name.EndsWith(".exe", StringComparison.Ordinal)) name += ".exe";
                    m.Extra.Add(name);
                }
            }

            return m;
        }

        /// <summary>Folders that hold many unrelated programs, where "same folder" says nothing about ownership.</summary>
        private static bool IsShared(string dirLower)
        {
            if (_sharedDirs == null)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                void Add(string p)
                {
                    if (!string.IsNullOrEmpty(p)) set.Add(p.TrimEnd('\\').ToLowerInvariant() + "\\");
                }

                foreach (Environment.SpecialFolder f in new[]
                {
                    Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                    Environment.SpecialFolder.CommonProgramFiles, Environment.SpecialFolder.CommonProgramFilesX86,
                    Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolder.UserProfile,
                    Environment.SpecialFolder.Desktop, Environment.SpecialFolder.DesktopDirectory,
                    Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.MyDocuments,
                    Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.Programs,
                    Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.CommonPrograms,
                })
                {
                    Add(Environment.GetFolderPath(f));
                }

                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile)) Add(Path.Combine(profile, "Downloads"));
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local)) Add(Path.Combine(local, "Programs"));
                Add(Path.GetTempPath());
                _sharedDirs = set;
            }

            if (_sharedDirs.Contains(dirLower)) return true;

            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows) && dirLower.StartsWith(windows.TrimEnd('\\').ToLowerInvariant() + "\\", StringComparison.Ordinal))
                return true;

            // Drive roots like "c:\".
            return dirLower.Length <= 3;
        }
    }
}
