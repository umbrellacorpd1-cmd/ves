using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using UmbraLauncher.Core;

namespace UmbraLauncher.Controls
{
    /// <summary>Renders a 24x24 icon geometry (stroke or fill), scaled like an SVG.</summary>
    public sealed class IconView : FrameworkElement
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(IconView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
            typeof(IconView), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(IconView), new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(IconView), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsFilledProperty = DependencyProperty.Register(
            nameof(IsFilled), typeof(bool), typeof(IconView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public Geometry Data { get => (Geometry)GetValue(DataProperty); set => SetValue(DataProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public bool IsFilled { get => (bool)GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

        protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) => new System.Windows.Size(Size, Size);

        protected override void OnRender(DrawingContext dc)
        {
            var g = Data;
            if (g == null) return;
            double s = Size / 24.0;
            dc.PushTransform(new ScaleTransform(s, s));
            if (IsFilled)
            {
                dc.DrawGeometry(Foreground, null, g);
            }
            else
            {
                var pen = new Pen(Foreground, StrokeThickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round,
                };
                dc.DrawGeometry(null, pen, g);
            }
            dc.Pop();
        }
    }

    /// <summary>Hollow display lettering, like the outlined "Majestic" wordmark over the hero cards.</summary>
    public sealed class OutlinedText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(OutlinedText), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
            typeof(OutlinedText), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
            typeof(OutlinedText), new FrameworkPropertyMetadata(120.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
            typeof(OutlinedText), new FrameworkPropertyMetadata(FontWeights.Black, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

        public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
            typeof(OutlinedText), new FrameworkPropertyMetadata(FontStyles.Italic, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, Invalidate));

        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            nameof(Stroke), typeof(Brush), typeof(OutlinedText), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(OutlinedText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(OutlinedText), new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        private Geometry _geometry;

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

        private static void Invalidate(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OutlinedText)d)._geometry = null;

        private Geometry Build()
        {
            if (_geometry != null) return _geometry;
            var ft = new FormattedText(
                Text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal),
                FontSize,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            _geometry = ft.BuildGeometry(new Point(0, 0));
            _geometry.Freeze();
            return _geometry;
        }

        protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
        {
            var b = Build().Bounds;
            if (b.IsEmpty) return new System.Windows.Size(0, 0);
            return new System.Windows.Size(Math.Max(0, b.Right) + StrokeThickness, Math.Max(0, b.Bottom) + StrokeThickness);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var g = Build();
            var pen = Stroke == null ? null : new Pen(Stroke, StrokeThickness) { LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(Fill, pen, g);
        }
    }

    /// <summary>Live indicator: a dot with an expanding ring (Majestic "В ЭФИРЕ" style).</summary>
    public sealed class PulseDot : Grid
    {
        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(PulseDot), new PropertyMetadata(Brushes.LimeGreen, (d, e) => ((PulseDot)d).Apply()));

        private readonly Ellipse _dot = new Ellipse();
        private readonly Ellipse _ring = new Ellipse();

        public PulseDot()
        {
            Width = 8;
            Height = 8;
            IsHitTestVisible = false;
            _ring.RenderTransformOrigin = new Point(0.5, 0.5);
            _ring.RenderTransform = new ScaleTransform(1, 1);
            Children.Add(_ring);
            Children.Add(_dot);
            Apply();
            Loaded += (s, e) => Start();
            Unloaded += (s, e) => Stop();
        }

        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

        private void Apply()
        {
            _dot.Fill = Fill;
            _ring.Fill = Fill;
        }

        private void Start()
        {
            var duration = TimeSpan.FromSeconds(1.6);
            var grow = new DoubleAnimation(1, 2.8, duration) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Timeline.SetDesiredFrameRate(grow, 24);
            var fade = new DoubleAnimation(0.55, 0, duration) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(fade, 24);
            var st = (ScaleTransform)_ring.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            _ring.BeginAnimation(OpacityProperty, fade);
        }

        private void Stop()
        {
            var st = (ScaleTransform)_ring.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _ring.BeginAnimation(OpacityProperty, null);
        }
    }
}

namespace UmbraLauncher.Core
{
    /// <summary>{core:L Key} — a live-updating localized string.</summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class LExtension : MarkupExtension
    {
        public LExtension()
        {
        }

        public LExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("[" + Key + "]") { Source = Loc.I, Mode = BindingMode.OneWay };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
