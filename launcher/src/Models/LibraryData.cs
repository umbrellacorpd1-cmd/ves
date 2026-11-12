using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace UmbraLauncher.Models
{
    [DataContract]
    public sealed class LibraryData
    {
        [DataMember(Name = "version", Order = 0)] public int Version { get; set; }
        [DataMember(Name = "settings", Order = 1)] public AppSettings Settings { get; set; }
        [DataMember(Name = "apps", Order = 2)] public List<AppEntry> Apps { get; set; }

        public LibraryData() => Init();

        [OnDeserializing] private void OnDeserializing(StreamingContext c) => Init();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext c)
        {
            if (Settings == null) Settings = new AppSettings();
            if (Apps == null) Apps = new List<AppEntry>();
            Apps.RemoveAll(a => a == null || string.IsNullOrWhiteSpace(a.Path));
        }

        private void Init()
        {
            Version = 1;
            Settings = new AppSettings();
            Apps = new List<AppEntry>();
        }
    }

    [DataContract]
    public sealed class AppSettings
    {
        public const string DefaultAccent = "#E81C5A";
        public const string DefaultTitle = "Umbra";

        [DataMember(Name = "language")] public string Language { get; set; }
        [DataMember(Name = "accent")] public string Accent { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "minimizeOnLaunch")] public bool MinimizeOnLaunch { get; set; }
        [DataMember(Name = "closeToTray")] public bool CloseToTray { get; set; }
        [DataMember(Name = "trackExternal")] public bool TrackExternal { get; set; }
        [DataMember(Name = "focusOnly")] public bool FocusOnly { get; set; }
        [DataMember(Name = "sort")] public string Sort { get; set; }
        [DataMember(Name = "view")] public string View { get; set; }
        [DataMember(Name = "statsRange")] public int StatsRange { get; set; }
        [DataMember(Name = "trayHintShown")] public bool TrayHintShown { get; set; }
        [DataMember(Name = "window")] public WindowPlacement Window { get; set; }

        public AppSettings() => Init();

        [OnDeserializing] private void OnDeserializing(StreamingContext c) => Init();

        private void Init()
        {
            Language = null;
            Accent = DefaultAccent;
            Title = DefaultTitle;
            MinimizeOnLaunch = true;
            CloseToTray = true;
            TrackExternal = true;
            FocusOnly = false;
            Sort = "recent";
            View = "grid";
            StatsRange = 14;
            Window = null;
        }
    }

    [DataContract]
    public sealed class WindowPlacement
    {
        [DataMember(Name = "left")] public double Left { get; set; }
        [DataMember(Name = "top")] public double Top { get; set; }
        [DataMember(Name = "width")] public double Width { get; set; }
        [DataMember(Name = "height")] public double Height { get; set; }
        [DataMember(Name = "maximized")] public bool Maximized { get; set; }
    }

    [DataContract]
    public sealed class AppEntry
    {
        [DataMember(Name = "id", Order = 0)] public string Id { get; set; }
        [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
        [DataMember(Name = "path", Order = 2)] public string Path { get; set; }
        [DataMember(Name = "args", Order = 3)] public string Arguments { get; set; }
        [DataMember(Name = "workdir", Order = 4)] public string WorkingDirectory { get; set; }
        [DataMember(Name = "admin", Order = 5)] public bool RunAsAdmin { get; set; }
        [DataMember(Name = "extra", Order = 6)] public string ExtraProcesses { get; set; }
        [DataMember(Name = "cover", Order = 7)] public string CoverPath { get; set; }
        [DataMember(Name = "added", Order = 8)] public long AddedAt { get; set; }
        [DataMember(Name = "lastPlayed", Order = 9)] public long LastPlayedAt { get; set; }
        [DataMember(Name = "launches", Order = 10)] public int LaunchCount { get; set; }
        [DataMember(Name = "sessions", Order = 11)] public List<SessionRecord> Sessions { get; set; }
        /// <summary>Snapshot of a session in progress, so time survives a crash or power loss.</summary>
        [DataMember(Name = "active", Order = 12, EmitDefaultValue = false)] public ActiveSession Active { get; set; }

        public AppEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            Sessions = new List<SessionRecord>();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext c)
        {
            if (string.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString("N");
            if (Sessions == null) Sessions = new List<SessionRecord>();
            Sessions.RemoveAll(s => s == null);
        }
    }

    [DataContract]
    public sealed class SessionRecord
    {
        /// <summary>Unix seconds (UTC) when the app was first seen running.</summary>
        [DataMember(Name = "s", Order = 0)] public long Start { get; set; }
        /// <summary>Unix seconds (UTC) when the app was last seen running.</summary>
        [DataMember(Name = "e", Order = 1)] public long End { get; set; }
        /// <summary>Counted seconds (excludes sleep, and background time in focus-only mode).</summary>
        [DataMember(Name = "d", Order = 2)] public int Seconds { get; set; }
        /// <summary>True when the app was started outside the launcher and picked up by detection.</summary>
        [DataMember(Name = "x", Order = 3)] public bool Detected { get; set; }
    }

    [DataContract]
    public sealed class ActiveSession
    {
        [DataMember(Name = "s")] public long Start { get; set; }
        [DataMember(Name = "e")] public long LastSeen { get; set; }
        [DataMember(Name = "d")] public double Seconds { get; set; }
        [DataMember(Name = "x")] public bool Detected { get; set; }
    }
}
