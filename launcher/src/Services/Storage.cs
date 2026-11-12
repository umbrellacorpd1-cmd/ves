using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using UmbraLauncher.Core;
using UmbraLauncher.Models;

namespace UmbraLauncher.Services
{
    /// <summary>Reads and writes library.json with atomic replace + one rolling backup.</summary>
    public sealed class Storage
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(LibraryData));

        public Storage(string dataDir)
        {
            DataDir = dataDir;
            Directory.CreateDirectory(dataDir);
        }

        public string DataDir { get; }

        public string LibraryPath => Path.Combine(DataDir, "library.json");

        private string BackupPath => Path.Combine(DataDir, "library.bak.json");

        /// <summary>--data &lt;dir&gt; wins, then a portable "UmbraData" folder next to the exe, then %APPDATA%.</summary>
        public static string ResolveDataDir(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--data", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[i + 1]));
            }

            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var portable = Path.Combine(exeDir, "UmbraData");
            if (Directory.Exists(portable)) return portable;

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UmbraLauncher");
        }

        public LibraryData Load()
        {
            if (!File.Exists(LibraryPath) && !File.Exists(BackupPath)) return new LibraryData();
            try
            {
                return Read(LibraryPath);
            }
            catch (Exception ex)
            {
                Log.Error("library.json is unreadable, trying the backup", ex);
                try
                {
                    var corrupt = Path.Combine(DataDir, $"library.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                    if (File.Exists(LibraryPath)) File.Copy(LibraryPath, corrupt, true);
                }
                catch
                {
                    // best effort
                }
            }

            try
            {
                return Read(BackupPath);
            }
            catch (Exception ex)
            {
                Log.Error("Backup is unreadable too, starting with an empty library", ex);
                return new LibraryData();
            }
        }

        private static LibraryData Read(string path)
        {
            // ReadAllText strips any BOM; the serializer then gets clean UTF-8.
            var text = File.ReadAllText(path);
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                return (LibraryData)Serializer.ReadObject(ms) ?? new LibraryData();
        }

        public void Save(LibraryData data)
        {
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, new UTF8Encoding(false), false, true, "  "))
                {
                    Serializer.WriteObject(writer, data);
                    writer.Flush();
                }
                bytes = ms.ToArray();
            }

            var tmp = LibraryPath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(LibraryPath))
            {
                try
                {
                    File.Replace(tmp, LibraryPath, BackupPath, true);
                    return;
                }
                catch (IOException)
                {
                    // File.Replace is unsupported on some volumes; fall back to copy + move.
                }
                catch (UnauthorizedAccessException)
                {
                }

                File.Copy(LibraryPath, BackupPath, true);
                File.Delete(LibraryPath);
            }
            File.Move(tmp, LibraryPath);
        }
    }
}
