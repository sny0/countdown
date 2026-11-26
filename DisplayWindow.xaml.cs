using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Forms = System.Windows.Forms;

namespace Countdown
{
    public partial class DisplayWindow : Window
    {
        private readonly DropShadowEffect digitShadow = new DropShadowEffect
        {
            Color = Color.FromRgb(7, 10, 16),
            ShadowDepth = 2
        };

        public DisplayWindow()
        {
            InitializeComponent();
        }

        internal void SetTime(string text, bool showHours)
        {
            TimeText.Text = text;
            TimeText.FontSize = showHours ? 60 : 78;
        }

        internal void SetDigitScale(double scale, Rect? controlsBounds)
        {
            Rect area = GetWorkArea();
            double fittedScale = Math.Min(scale, Math.Min(area.Width / DisplayContent.Width, area.Height / DisplayContent.Height));
            Width = DisplayContent.Width * fittedScale;
            Height = DisplayContent.Height * fittedScale;
            if (!IsVisible)
            {
                return;
            }

            var bounds = new Rect(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)), Width, Height);
            if (controlsBounds is Rect controls && bounds.IntersectsWith(controls))
            {
                const double gap = 10;
                var positions = new[]
                {
                    new Rect(controls.Left - gap - Width, bounds.Top, Width, Height),
                    new Rect(controls.Right + gap, bounds.Top, Width, Height),
                    new Rect(bounds.Left, controls.Top - gap - Height, Width, Height),
                    new Rect(bounds.Left, controls.Bottom + gap, Width, Height)
                };
                bounds = positions.Where(position => area.Contains(position))
                    .OrderBy(position => Math.Abs(position.Left - bounds.Left) + Math.Abs(position.Top - bounds.Top))
                    .DefaultIfEmpty(bounds).First();
            }

            Left = bounds.Left;
            Top = bounds.Top;
        }

        private Rect GetWorkArea()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null)
            {
                return SystemParameters.WorkArea;
            }

            var area = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var fromPixels = source.CompositionTarget.TransformFromDevice;
            return new Rect(fromPixels.Transform(new Point(area.Left, area.Top)),
                fromPixels.Transform(new Point(area.Right, area.Bottom)));
        }

        internal void SetBackgroundOpacity(double opacity)
        {
            Backdrop.Opacity = opacity;
        }

        internal void SetDigitColor(Brush color)
        {
            TimeText.Foreground = color;
        }

        internal FontWeight DigitWeight
        {
            get => TimeText.FontWeight;
            set => TimeText.FontWeight = value;
        }

        internal void SetDigitShadow(double opacity, double blurRadius)
        {
            digitShadow.Opacity = opacity;
            digitShadow.BlurRadius = blurRadius;
            TimeText.Effect = opacity > 0 ? digitShadow : null;
        }

        private void DragDisplay(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
