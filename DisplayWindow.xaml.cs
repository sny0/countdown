using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private Point resizeGripOffset;
        private Point resizeStartScreenPoint;
        private Matrix resizePixelsToDips;
        private Rect resizeStartBounds;
        private Rect resizeWorkArea;
        private Vector resizeEdge;

        internal int DigitScalePercent { get; private set; }
        internal event Action<int> DigitScaleChanged;

        public DisplayWindow()
        {
            InitializeComponent();
        }

        internal void SetTime(string text, bool showHours)
        {
            TimeText.Text = text;
            TimeText.FontSize = showHours ? 60 : 78;
        }

        internal void SetDigitScale(int percent, Rect? controlsBounds)
        {
            Rect area = GetWorkArea();
            ApplyDigitScale(percent, area);
            if (!IsVisible)
            {
                return;
            }

            Rect bounds = FitPosition(Left, Top, area);
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

        private void ApplyDigitScale(int percent, Rect area)
        {
            DigitScalePercent = percent;
            double fittedScale = Math.Min(percent / 100.0, GetMaxScale(area));
            Width = DisplayContent.Width * fittedScale;
            Height = DisplayContent.Height * fittedScale;
        }

        private double GetMaxScale(Rect area)
        {
            return Math.Min(area.Width / DisplayContent.Width, area.Height / DisplayContent.Height);
        }

        private Rect FitPosition(double left, double top, Rect area)
        {
            return new Rect(Math.Max(area.Left, Math.Min(left, area.Right - Width)),
                Math.Max(area.Top, Math.Min(top, area.Bottom - Height)), Width, Height);
        }

        private void StartResize(object sender, DragStartedEventArgs e)
        {
            var grip = (Thumb)sender;
            resizeEdge = new Vector(Grid.GetColumn(grip) - 1, Grid.GetRow(grip) - 1);
            resizeGripOffset = new Point(e.HorizontalOffset, e.VerticalOffset);
            resizeStartScreenPoint = grip.PointToScreen(resizeGripOffset);
            resizePixelsToDips = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
            resizeStartBounds = new Rect(Left, Top, Width, Height);
            resizeWorkArea = GetWorkArea();
        }

        private void ResizeDisplay(object sender, DragDeltaEventArgs e)
        {
            var grip = (Thumb)sender;
            var offset = new Vector(e.HorizontalChange, e.VerticalChange);
            Point pointer = grip.PointToScreen(resizeGripOffset + offset);
            ResizeFromScreenTravel(pointer - resizeStartScreenPoint);
        }

        private void FinishResize(object sender, DragCompletedEventArgs e)
        {
            if (!e.Canceled)
            {
                ResizeFromScreenTravel(new Vector(e.HorizontalChange, e.VerticalChange));
            }
        }

        private void ResizeFromScreenTravel(Vector screenTravel)
        {
            Vector pointerTravel = resizePixelsToDips.Transform(screenTravel);
            var scaleDirection = new Vector(resizeEdge.X * DisplayContent.Width, resizeEdge.Y * DisplayContent.Height);
            double scaleChange = Vector.Multiply(pointerTravel, scaleDirection) / scaleDirection.LengthSquared;
            double scale = resizeStartBounds.Width / DisplayContent.Width + scaleChange;
            int minPercent = CountdownSettings.MinDigitScalePercent;
            int maxPercent = Math.Max(minPercent, Math.Min(CountdownSettings.MaxDigitScalePercent,
                (int)Math.Floor(GetMaxScale(resizeWorkArea) * 100)));
            int percent = Math.Max(minPercent, Math.Min((int)Math.Round(scale * 100), maxPercent));
            if (percent == DigitScalePercent)
            {
                return;
            }

            ApplyDigitScale(percent, resizeWorkArea);

            // Anchor the opposite edge, or keep that axis centered when dragging only one edge.
            double left = resizeStartBounds.Left + (resizeStartBounds.Width - Width) * (1 - resizeEdge.X) / 2;
            double top = resizeStartBounds.Top + (resizeStartBounds.Height - Height) * (1 - resizeEdge.Y) / 2;
            Rect bounds = FitPosition(left, top, resizeWorkArea);
            Left = bounds.Left;
            Top = bounds.Top;
            DigitScaleChanged?.Invoke(percent);
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
