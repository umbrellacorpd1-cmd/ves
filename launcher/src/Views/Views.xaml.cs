using System.Windows.Controls;

namespace UmbraLauncher.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView() => InitializeComponent();

        public void FocusSearch()
        {
            if (!SearchBox.IsVisible) return;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    public partial class StatsView : UserControl
    {
        public StatsView() => InitializeComponent();
    }

    public partial class SettingsView : UserControl
    {
        public SettingsView() => InitializeComponent();
    }

    public partial class AppDrawer : UserControl
    {
        public AppDrawer() => InitializeComponent();
    }

    public partial class EditorDialog : UserControl
    {
        public EditorDialog() => InitializeComponent();
    }

    public partial class ConfirmDialog : UserControl
    {
        public ConfirmDialog() => InitializeComponent();
    }
}
