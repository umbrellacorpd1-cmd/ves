using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UmbraLauncher.Core;

namespace UmbraLauncher.Services
{
    public sealed class ProcInfo
    {
        public int Pid;
        public int ParentPid;
        public string ExeName;
        public string ExeNameLower;
        /// <summary>Full image path, or null when the process denies even limited query access.</summary>
        public string PathLower;
        /// <summary>FILETIME of process creation (0 when unknown); guards against PID reuse.</summary>
        public long CreateTime;
    }

    public sealed class ProcessSnapshot
    {
        public readonly List<ProcInfo> All = new List<ProcInfo>(512);
        public readonly Dictionary<int, ProcInfo> ByPid = new Dictionary<int, ProcInfo>(512);
        public readonly Dictionary<int, List<ProcInfo>> Children = new Dictionary<int, List<ProcInfo>>(512);
    }

    /// <summary>Lists running processes via a Toolhelp snapshot; paths are resolved once per process and cached.</summary>
    public sealed class ProcessScanner
    {
        private readonly Dictionary<int, ProcInfo> _cache = new Dictionary<int, ProcInfo>();
        private readonly int _selfPid;

        public ProcessScanner()
        {
            using (var self = Process.GetCurrentProcess()) _selfPid = self.Id;
        }

        public ProcessSnapshot Scan()
        {
            var snap = new ProcessSnapshot();
            var handle = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0);
            if (handle == IntPtr.Zero || handle == Native.INVALID_HANDLE_VALUE) return snap;

            try
            {
                var entry = new Native.PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(Native.PROCESSENTRY32W)) };
                if (!Native.Process32FirstW(handle, ref entry)) return snap;

                var seen = new HashSet<int>();
                do
                {
                    int pid = (int)entry.th32ProcessID;
                    if (pid == 0 || pid == 4 || pid == _selfPid) continue;
                    seen.Add(pid);

                    int parent = (int)entry.th32ParentProcessID;
                    string exe = entry.szExeFile ?? string.Empty;
                    if (!_cache.TryGetValue(pid, out var info) || info.ParentPid != parent ||
                        !string.Equals(info.ExeName, exe, StringComparison.OrdinalIgnoreCase))
                    {
                        info = new ProcInfo { Pid = pid, ParentPid = parent, ExeName = exe, ExeNameLower = exe.ToLowerInvariant() };
                        QueryDetails(info);
                        _cache[pid] = info;
                    }

                    snap.All.Add(info);
                    snap.ByPid[pid] = info;
                    if (!snap.Children.TryGetValue(parent, out var kids)) snap.Children[parent] = kids = new List<ProcInfo>();
                    kids.Add(info);
                }
                while (Native.Process32NextW(handle, ref entry));

                if (_cache.Count > seen.Count)
                {
                    var dead = new List<int>();
                    foreach (var pid in _cache.Keys)
                        if (!seen.Contains(pid)) dead.Add(pid);
                    foreach (var pid in dead) _cache.Remove(pid);
                }
            }
            finally
            {
                Native.CloseHandle(handle);
            }

            return snap;
        }

        private static void QueryDetails(ProcInfo info)
        {
            var h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)info.Pid);
            if (h == IntPtr.Zero) return;
            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (Native.QueryFullProcessImageNameW(h, 0, sb, ref size))
                    info.PathLower = sb.ToString().ToLowerInvariant();
                if (Native.GetProcessTimes(h, out var created, out _, out _, out _))
                    info.CreateTime = created;
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }
    }
}
