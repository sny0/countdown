using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

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
