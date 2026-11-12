using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UmbraLauncher.Core;
using UmbraLauncher.Services;
using UmbraLauncher.ViewModels;
using UmbraLauncher.Views;

namespace UmbraLauncher
{
    public partial class App : Application
    {
        private Mutex _mutex;
        private bool _ownsMutex;
        private EventWaitHandle _showEvent;
        private TrayService _tray;
        private MainViewModel _vm;
        private MainWindow _window;
        private bool _exiting;
        private DateTime _lastErrorToast;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = e.Args ?? new string[0];
            bool startInTray = args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
            var dataDir = Storage.ResolveDataDir(args);

            // One instance per data folder; a second launch just brings the window back.
            var key = InstanceKey(dataDir);
            _mutex = new Mutex(true, @"Local\UmbraLauncher." + key, out _ownsMutex);
            if (!_ownsMutex)
            {
                try { EventWaitHandle.OpenExisting(@"Local\UmbraLauncher.Show." + key).Set(); }
                catch (Exception) { /* the other instance is shutting down */ }
                Shutdown();
                return;
            }

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\UmbraLauncher.Show." + key);
            ThreadPool.RegisterWaitForSingleObject(_showEvent, (s, timedOut) => Dispatcher.BeginInvoke(new Action(ShowMain)), null, -1, false);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => Log.Error("Unhandled exception", ev.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                Log.Error("Unobserved task exception", ev.Exception);
                ev.SetObserved();
            };

            Log.Init(dataDir);
            Log.Info($"Starting {typeof(App).Assembly.GetName().Version} with data in {dataDir}");
            var storage = new Storage(dataDir);
            var data = storage.Load();

            Loc.I.Language = string.IsNullOrEmpty(data.Settings.Language) ? Loc.DetectSystemLanguage() : data.Settings.Language;
            ThemeService.ApplyAccent(data.Settings.Accent);

            _vm = new MainViewModel(storage, data);
            _window = new MainWindow(_vm);
            MainWindow = _window;

            _tray = new TrayService
            {
                GetTitle = () => _vm.BrandTitle,
                GetApps = () => _vm.Apps
                    .OrderByDescending(a => a.IsRunning)
                    .ThenByDescending(a => Math.Max(a.Entry.LastPlayedAt, a.Entry.AddedAt))
                    .Take(15)
                    .Select(a => new TrayApp { Id = a.Id, Name = a.Name, Running = a.IsRunning, Icon = a.Icon })
                    .ToList(),
            };
            _tray.OpenRequested += ShowMain;
            _tray.ExitRequested += ExitApp;
            _tray.LaunchRequested += id => _vm.LaunchById(id);
            _tray.SetTooltip(_vm.WindowTitle);
            _vm.TitleChanged += () => _tray.SetTooltip(_vm.WindowTitle);
            _vm.StatusChanged += () => _tray.SetTooltip(_vm.RunningCount > 0 ? $"{_vm.WindowTitle} — {_vm.RunningChipText}" : _vm.WindowTitle);
            _vm.HiddenToTrayFirstTime += () => _tray.ShowBalloon(Loc.T("ToastTray"), Loc.T("ToastTrayText"));

            if (!startInTray) _window.Show();
            _vm.Start();
            StartupService.Refresh();
        }

        private static string InstanceKey(string dataDir)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(dataDir.ToLowerInvariant()));
                return BitConverter.ToString(hash, 0, 6).Replace("-", string.Empty);
            }
        }

        public void ShowMain()
        {
            if (_window == null || _exiting) return;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
            _window.Focus();
        }

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            _vm?.Shutdown();
            _tray?.Dispose();
            _tray = null;
            if (_window != null && !_window.IsClosed)
            {
                _window.AllowClose = true;
                _window.Close();
            }

            Shutdown();
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            // Windows is logging off / shutting down: close sessions so no time is lost.
            _vm?.Shutdown();
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _vm?.Shutdown();
            _tray?.Dispose();
            if (_ownsMutex)
            {
                try { _mutex.ReleaseMutex(); }
                catch (ApplicationException) { /* not owned by this thread */ }
            }

            _mutex?.Dispose();
            _showEvent?.Dispose();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("UI exception", e.Exception);
            e.Handled = true;
            if (_vm != null && (DateTime.UtcNow - _lastErrorToast).TotalSeconds > 5)
            {
                _lastErrorToast = DateTime.UtcNow;
                _vm.Toast("error", Loc.T("ToastError"), e.Exception.Message);
            }
        }
    }
}
