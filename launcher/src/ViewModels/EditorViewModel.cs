using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using UmbraLauncher.Core;
using UmbraLauncher.Services;

namespace UmbraLauncher.ViewModels
{
    /// <summary>Add / edit dialog state.</summary>
    public sealed class EditorViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        private readonly DispatcherTimer _previewTimer;
        private string _path = string.Empty;
        private string _name = string.Empty;
        private string _arguments = string.Empty;
        private string _workingDirectory = string.Empty;
        private string _extra = string.Empty;
        private string _coverPath = string.Empty;
        private bool _runAsAdmin;
        private bool _showAdvanced;
        private bool _autoName = true;
        private string _error;
        private BitmapSource _previewIcon;
        private Brush _previewBrush;
        private int _previewToken;

        public EditorViewModel(MainViewModel main, AppItemViewModel target)
        {
            _main = main;
            Target = target;
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _previewTimer.Tick += (s, e) =>
            {
                _previewTimer.Stop();
                UpdatePreview();
            };

            BrowseCommand = new RelayCommand(Browse);
            BrowseCoverCommand = new RelayCommand(BrowseCover);
            ClearCoverCommand = new RelayCommand(() => CoverPath = string.Empty);
            ToggleAdvancedCommand = new RelayCommand(() => ShowAdvanced = !ShowAdvanced);
            SaveCommand = new RelayCommand(() => _main.CommitEditor(this));
            CancelCommand = new RelayCommand(() => _main.CloseEditor());

            if (target != null)
            {
                var e = target.Entry;
                _path = e.Path ?? string.Empty;
                _name = e.Name ?? string.Empty;
                _arguments = e.Arguments ?? string.Empty;
                _workingDirectory = e.WorkingDirectory ?? string.Empty;
                _extra = e.ExtraProcesses ?? string.Empty;
                _coverPath = e.CoverPath ?? string.Empty;
                _runAsAdmin = e.RunAsAdmin;
                _autoName = false;
                _showAdvanced = !string.IsNullOrEmpty(_workingDirectory) || !string.IsNullOrEmpty(_extra) || !string.IsNullOrEmpty(_coverPath);
                UpdatePreview();
            }
        }

        public AppItemViewModel Target { get; }
        public bool IsNew => Target == null;
        public string Title => IsNew ? Loc.T("EditorAddTitle") : Loc.T("EditorEditTitle");
        public string SaveText => IsNew ? Loc.T("AddConfirm") : Loc.T("Save");

        public ICommand BrowseCommand { get; }
        public ICommand BrowseCoverCommand { get; }
        public ICommand ClearCoverCommand { get; }
        public ICommand ToggleAdvancedCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public string Path
        {
            get => _path;
            set
            {
                if (!Set(ref _path, value ?? string.Empty)) return;
                Error = null;
                _previewTimer.Stop();
                _previewTimer.Start();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    _autoName = string.IsNullOrWhiteSpace(_name);
                    Error = null;
                    OnPropertyChanged(nameof(PreviewTitle));
                }
            }
        }

        public string Arguments { get => _arguments; set => Set(ref _arguments, value ?? string.Empty); }
        public string WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, value ?? string.Empty); }
        public string ExtraProcesses { get => _extra; set => Set(ref _extra, value ?? string.Empty); }
        public bool RunAsAdmin { get => _runAsAdmin; set => Set(ref _runAsAdmin, value); }
        public bool ShowAdvanced { get => _showAdvanced; set => Set(ref _showAdvanced, value); }

        public string CoverPath
        {
            get => _coverPath;
            set
            {
                if (Set(ref _coverPath, value ?? string.Empty)) Raise(nameof(HasCover), nameof(CoverName));
            }
        }

        public bool HasCover => !string.IsNullOrWhiteSpace(_coverPath);
        public string CoverName => HasCover ? System.IO.Path.GetFileName(_coverPath) : Loc.T("NoCover");

        public string Error
        {
            get => _error;
            set => Set(ref _error, value);
        }

        public BitmapSource PreviewIcon { get => _previewIcon; private set => Set(ref _previewIcon, value); }
        public Brush PreviewBrush { get => _previewBrush; private set => Set(ref _previewBrush, value); }
        public bool HasFile => File.Exists(CleanPath);
        public string PreviewTitle => string.IsNullOrWhiteSpace(_name) ? Loc.T("PreviewEmpty") : _name;
        public string PreviewSubtitle => HasFile ? CleanPath : Loc.T("PreviewHint");

        public string CleanPath => (_path ?? string.Empty).Trim().Trim('"');

        public void ApplyFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var link = ShellHelpers.ResolveShortcut(file);
                if (link != null && !string.IsNullOrWhiteSpace(link.Target) && File.Exists(link.Target))
                {
                    if (_autoName) SetAutoName(System.IO.Path.GetFileNameWithoutExtension(file));
                    _autoName = false;
                    Arguments = link.Arguments ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(link.WorkingDirectory) &&
                        !string.Equals(link.WorkingDirectory.TrimEnd('\\'), System.IO.Path.GetDirectoryName(link.Target)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        WorkingDirectory = link.WorkingDirectory;
                    }

                    file = link.Target;
                }
            }

            _path = file;
            OnPropertyChanged(nameof(Path));
            Error = null;
            UpdatePreview();
        }

        private void SetAutoName(string name)
        {
            _name = name ?? string.Empty;
            Raise(nameof(Name), nameof(PreviewTitle));
        }

        private async void UpdatePreview()
        {
            var path = CleanPath;
            Raise(nameof(HasFile), nameof(PreviewSubtitle));
            int token = ++_previewToken;
            if (!File.Exists(path))
            {
                PreviewIcon = null;
                PreviewBrush = null;
                return;
            }

            if (_autoName && !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) SetAutoName(ShellHelpers.SuggestName(path));
            var result = await IconService.LoadAsync(path);
            if (token != _previewToken) return;
            PreviewIcon = result.Icon;
            double hue = result.Dominant.HasValue ? ColorUtil.HueOf(result.Dominant.Value) : ColorUtil.FallbackHue(_name);
            PreviewBrush = ColorUtil.CardBrush(hue, 0.65);
        }

        private void Browse()
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.T("FieldPath"),
                Filter = $"{Loc.T("FilterApps")} (*.exe, *.lnk)|*.exe;*.lnk|{Loc.T("FilterAll")} (*.*)|*.*",
                CheckFileExists = true,
                DereferenceLinks = false,
            };
            var current = CleanPath;
            try
            {
                var dir = File.Exists(current) ? System.IO.Path.GetDirectoryName(current) : null;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            catch
            {
                // Invalid path text; let the dialog pick its default folder.
            }

            if (dlg.ShowDialog(Application.Current.MainWindow) == true) ApplyFile(dlg.FileName);
        }

        private void BrowseCover()
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.T("FieldCover"),
                Filter = $"{Loc.T("FilterImages")} (*.png, *.jpg, *.jpeg, *.bmp, *.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|{Loc.T("FilterAll")} (*.*)|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(Application.Current.MainWindow) == true) CoverPath = dlg.FileName;
        }

        public bool Validate()
        {
            var path = CleanPath;
            if (path.Length == 0)
            {
                Error = Loc.T("ErrPathEmpty");
                return false;
            }

            if (!File.Exists(path))
            {
                Error = Loc.T("ErrPathMissing");
                return false;
            }

            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                ApplyFile(path);
                path = CleanPath;
            }

            if (string.IsNullOrWhiteSpace(_name)) SetAutoName(ShellHelpers.SuggestName(path));
            if (string.IsNullOrWhiteSpace(_name))
            {
                Error = Loc.T("ErrNameEmpty");
                return false;
            }

            if (_main.IsDuplicate(path, Target))
            {
                Error = Loc.T("ErrDuplicate");
                return false;
            }

            return true;
        }
    }
}
