using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UmbraLauncher.Core
{
    /// <summary>Attached properties used by the control templates.</summary>
    public static class Ui
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
            "Icon", typeof(Geometry), typeof(Ui), new PropertyMetadata(null));

        public static Geometry GetIcon(DependencyObject o) => (Geometry)o.GetValue(IconProperty);
        public static void SetIcon(DependencyObject o, Geometry v) => o.SetValue(IconProperty, v);

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
            "Placeholder", typeof(string), typeof(Ui), new PropertyMetadata(null));

        public static string GetPlaceholder(DependencyObject o) => (string)o.GetValue(PlaceholderProperty);
        public static void SetPlaceholder(DependencyObject o, string v) => o.SetValue(PlaceholderProperty, v);

        /// <summary>Clips an element to a rounded rectangle that follows its size.</summary>
        public static readonly DependencyProperty ClipRadiusProperty = DependencyProperty.RegisterAttached(
            "ClipRadius", typeof(double), typeof(Ui), new PropertyMetadata(0.0, OnClipRadiusChanged));

        public static double GetClipRadius(DependencyObject o) => (double)o.GetValue(ClipRadiusProperty);
        public static void SetClipRadius(DependencyObject o, double v) => o.SetValue(ClipRadiusProperty, v);

        private static void OnClipRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement fe)) return;
            fe.SizeChanged -= UpdateClip;
            fe.SizeChanged += UpdateClip;
            ApplyClip(fe);
        }

        private static void UpdateClip(object sender, SizeChangedEventArgs e) => ApplyClip((FrameworkElement)sender);

        private static void ApplyClip(FrameworkElement fe)
        {
            double r = GetClipRadius(fe);
            if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0) return;
            var g = new RectangleGeometry(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight), r, r);
            g.Freeze();
            fe.Clip = g;
        }
    }

    public static class Behaviors
    {
        public static readonly DependencyProperty DoubleClickCommandProperty = DependencyProperty.RegisterAttached(
            "DoubleClickCommand", typeof(ICommand), typeof(Behaviors), new PropertyMetadata(null, OnDoubleClickChanged));

        public static ICommand GetDoubleClickCommand(DependencyObject o) => (ICommand)o.GetValue(DoubleClickCommandProperty);
        public static void SetDoubleClickCommand(DependencyObject o, ICommand v) => o.SetValue(DoubleClickCommandProperty, v);

        private static void OnDoubleClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UIElement el)) return;
            el.PreviewMouseLeftButtonDown -= OnPreviewDown;
            if (e.NewValue != null) el.PreviewMouseLeftButtonDown += OnPreviewDown;
        }

        private static void OnPreviewDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var cmd = GetDoubleClickCommand((DependencyObject)sender);
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>Click command for non-button elements (e.g. a dimmed backdrop).</summary>
        public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.RegisterAttached(
            "ClickCommand", typeof(ICommand), typeof(Behaviors), new PropertyMetadata(null, OnClickChanged));

        public static ICommand GetClickCommand(DependencyObject o) => (ICommand)o.GetValue(ClickCommandProperty);
        public static void SetClickCommand(DependencyObject o, ICommand v) => o.SetValue(ClickCommandProperty, v);

        private static void OnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UIElement el)) return;
            el.MouseLeftButtonUp -= OnClickUp;
            if (e.NewValue != null) el.MouseLeftButtonUp += OnClickUp;
        }

        private static void OnClickUp(object sender, MouseButtonEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, sender)) return;
            var cmd = GetClickCommand((DependencyObject)sender);
            if (cmd != null && cmd.CanExecute(null)) cmd.Execute(null);
        }

        /// <summary>Moves keyboard focus into the element whenever it becomes visible.</summary>
        public static readonly DependencyProperty FocusOnShowProperty = DependencyProperty.RegisterAttached(
            "FocusOnShow", typeof(bool), typeof(Behaviors), new PropertyMetadata(false, OnFocusOnShowChanged));

        public static bool GetFocusOnShow(DependencyObject o) => (bool)o.GetValue(FocusOnShowProperty);
        public static void SetFocusOnShow(DependencyObject o, bool v) => o.SetValue(FocusOnShowProperty, v);

        private static void OnFocusOnShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is UIElement el)) return;
            el.IsVisibleChanged -= FocusWhenVisible;
            if ((bool)e.NewValue) el.IsVisibleChanged += FocusWhenVisible;
        }

        private static void FocusWhenVisible(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;
            var el = (UIElement)sender;
            el.Dispatcher.BeginInvoke(new Action(() =>
            {
                el.Focus();
                if (el is TextBox tb) tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    /// <summary>Entrance animations that run whenever an element becomes visible.</summary>
    public static class Anim
    {
        public static readonly DependencyProperty ShowEffectProperty = DependencyProperty.RegisterAttached(
            "ShowEffect", typeof(string), typeof(Anim), new PropertyMetadata(null, OnShowEffectChanged));

        public static string GetShowEffect(DependencyObject o) => (string)o.GetValue(ShowEffectProperty);
        public static void SetShowEffect(DependencyObject o, string v) => o.SetValue(ShowEffectProperty, v);

        private static void OnShowEffectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement fe)) return;
            fe.IsVisibleChanged -= OnVisibleChanged;
            if (e.NewValue != null) fe.IsVisibleChanged += OnVisibleChanged;
        }

        private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;
            var fe = (FrameworkElement)sender;
            Play(fe, GetShowEffect(fe));
        }

        public static void Play(FrameworkElement fe, string effect)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(effect == "Fade" ? 180 : 260);
            fe.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

            var group = new TransformGroup();
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform();
            group.Children.Add(scale);
            group.Children.Add(translate);
            fe.RenderTransform = group;
            fe.RenderTransformOrigin = new Point(0.5, 0.5);

            switch (effect)
            {
                case "SlideUp":
                    translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, duration) { EasingFunction = ease });
                    break;
                case "SlideLeft":
                    translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(Math.Max(120, fe.ActualWidth), 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                    break;
                case "Pop":
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
                    translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
                    break;
            }
        }
    }
}
