using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using UmbraLauncher.Core;
using UmbraLauncher.Models;
using UmbraLauncher.Services;
using UmbraLauncher.ViewModels;

namespace UmbraLauncher.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _restoreMaximized;

        public MainWindow(MainViewModel vm)
        {
            _vm = vm;
            DataContext = vm;
            InitializeComponent();
            RestorePlacement(vm.Settings.Window);

            SourceInitialized += (s, e) =>
            {
                WindowEffects.Apply(this);
                if (_restoreMaximized) WindowState = WindowState.Maximized;
            };
            StateChanged += (s, e) => UpdateChrome();
            vm.RequestMinimize += () => WindowState = WindowState.Minimized;
            PreviewKeyDown += OnPreviewKeyDown;
            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
            UpdateChrome();
        }

        public bool AllowClose { get; set; }
        public bool IsClosed { get; private set; }

        private void UpdateChrome()
        {
            bool max = WindowState == WindowState.Maximized;
            Root.Margin = max ? WindowEffects.MaximizedOverhang(this) : new Thickness(0);
            Ui.SetIcon(MaxButton, (Geometry)FindResource(max ? "I.Restore" : "I.Maximize"));
            MaxButton.SetBinding(ToolTipProperty, new Binding(max ? "[Restore]" : "[Maximize]") { Source = Loc.I });
        }

        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnMaximize(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && _vm.Editor == null && _vm.Confirm == null)
            {
                _vm.Page = "Library";
                Library.FocusSearch();
                e.Handled = true;
            }
        }

        // ---------- Drag & drop of .exe / .lnk files ----------

        private static bool HasFiles(DragEventArgs e) => e.Data.GetDataPresent(DataFormats.FileDrop);

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (_vm.Editor != null || _vm.Confirm != null) return;
            _vm.IsDropTarget = HasFiles(e);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = HasFiles(e) && _vm.Editor == null && _vm.Confirm == null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            // DragLeave also bubbles up from child elements; only hide once the cursor really left the window.
            var p = e.GetPosition(this);
            if (p.X > 0 && p.Y > 0 && p.X < ActualWidth && p.Y < ActualHeight) return;
            _vm.IsDropTarget = false;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _vm.IsDropTarget = false;
            if (!HasFiles(e) || _vm.Editor != null || _vm.Confirm != null) return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                _vm.Page = "Library";
                _vm.AddFiles(files);
            }
        }

        // ---------- Placement & closing ----------

        private void RestorePlacement(WindowPlacement p)
        {
            if (p == null || p.Width < 800 || p.Height < 560) return;
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (!screen.Contains(new Point(p.Left + 80, p.Top + 30))) return;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = p.Left;
            Top = p.Top;
            Width = p.Width;
            Height = p.Height;
            _restoreMaximized = p.Maximized;
        }

        private void SavePlacement()
        {
            var b = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            if (b.IsEmpty || b.Width < 400 || double.IsNaN(b.Left)) return;
            _vm.Settings.Window = new WindowPlacement
            {
                Left = b.Left,
                Top = b.Top,
                Width = b.Width,
                Height = b.Height,
                Maximized = WindowState == WindowState.Maximized,
            };
            _vm.RequestSave();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (IsVisible) SavePlacement();
            if (!AllowClose && _vm.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                _vm.OnHiddenToTray();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
            if (!AllowClose) ((App)Application.Current).ExitApp();
        }
    }
}
