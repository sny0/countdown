using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Countdown
{
    public partial class MainWindow : Window, Forms.IWin32Window
    {
        private enum TimerState { Ready, Running, Paused, Finished }

        private const string InputHint = "分:秒、または 時:分:秒";
        private const double DefaultShadowOpacity = 85;
        private const double DefaultShadowBlur = 7;
        private static readonly Geometry PlayIcon = Geometry.Parse("M 0,0 L 11,6 L 0,12 Z");
        private static readonly Geometry PauseIcon = Geometry.Parse("M 0,0 H 4 V 12 H 0 Z M 7,0 H 11 V 12 H 7 Z");

        private readonly Stopwatch elapsed = new Stopwatch();
        private readonly DispatcherTimer refreshTimer;
        private readonly DropShadowEffect digitShadow = new DropShadowEffect
        {
            Color = Color.FromRgb(7, 10, 16),
            ShadowDepth = 2
        };
        private TimeSpan duration = TimeSpan.FromMinutes(5);
        private TimerState state = TimerState.Ready;
        private SolidColorBrush digitColor;

        IntPtr Forms.IWin32Window.Handle => new WindowInteropHelper(this).Handle;

        public MainWindow()
        {
            InitializeComponent();
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            refreshTimer.Tick += RefreshCountdown;
            ResetDigitAppearance(this, new RoutedEventArgs());
            UpdateControls();
            UpdateDisplay(duration);
        }

        private void ToggleCountdown(object sender, RoutedEventArgs e)
        {
            if (state == TimerState.Running)
            {
                elapsed.Stop();
                refreshTimer.Stop();
                state = TimerState.Paused;
                RefreshCountdown(this, EventArgs.Empty);
            }
            else
            {
                if (state == TimerState.Finished)
                {
                    elapsed.Reset();
                }

                elapsed.Start();
                refreshTimer.Start();
                state = TimerState.Running;
                RefreshCountdown(this, EventArgs.Empty);
            }

            UpdateControls();
        }

        private void ResetCountdown(object sender, RoutedEventArgs e)
        {
            elapsed.Reset();
            refreshTimer.Stop();
            state = TimerState.Ready;
            UpdateControls();
            UpdateDisplay(duration);
        }

        private void RefreshCountdown(object sender, EventArgs e)
        {
            var remaining = duration - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                elapsed.Stop();
                refreshTimer.Stop();
                state = TimerState.Finished;
                UpdateControls();
            }

            UpdateDisplay(remaining);
        }

        private void UpdateDisplay(TimeSpan remaining)
        {
            TimeText.Text = FormatTime((int)Math.Ceiling(remaining.TotalSeconds), duration.TotalHours >= 1);
            TimeText.FontSize = duration.TotalHours >= 1 ? 60 : 78;
            ProgressScale.ScaleX = remaining.TotalSeconds / duration.TotalSeconds;
        }

        private void UpdateControls()
        {
            switch (state)
            {
                case TimerState.Running:
                    StatusText.Text = "計測中";
                    StartLabel.Text = "一時停止";
                    break;
                case TimerState.Paused:
                    StatusText.Text = "一時停止中";
                    StartLabel.Text = "再開";
                    break;
                case TimerState.Finished:
                    StatusText.Text = "時間です";
                    StartLabel.Text = "もう一度";
                    break;
                default:
                    StatusText.Text = "開始できます";
                    StartLabel.Text = "開始";
                    break;
            }

            var muted = (Brush)FindResource("MutedBrush");
            var accent = (Brush)FindResource("AccentBrush");
            var finished = (Brush)FindResource("FinishedBrush");
            StatusDot.Fill = state == TimerState.Finished ? finished : state == TimerState.Running ? accent : muted;
            UpdateDigitColor();
            StartIcon.Data = state == TimerState.Running ? PauseIcon : PlayIcon;
            AutomationProperties.SetName(StartButton, StartLabel.Text);
        }

        private static string FormatTime(int seconds, bool showHours)
        {
            var time = TimeSpan.FromSeconds(seconds);
            if (showHours)
            {
                return FormattableString.Invariant($"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}");
            }

            return FormattableString.Invariant($"{time.Minutes:00}:{time.Seconds:00}");
        }

        private static bool TryParseDuration(string text, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            var parts = text.Trim().Normalize(NormalizationForm.FormKC).Split(':');
            if (parts.Length != 2 && parts.Length != 3)
            {
                return false;
            }

            int seconds = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                int part;
                if (parts[i].Length > 2 || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out part)
                    || (i > 0 && part >= 60))
                {
                    return false;
                }

                seconds = seconds * 60 + part;
            }

            result = TimeSpan.FromSeconds(seconds);
            return seconds > 0;
        }

        private void ToggleSettings(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                HideSettings();
                return;
            }

            DurationInput.Text = FormatTime((int)duration.TotalSeconds, duration.TotalHours >= 1);
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void HideSettings()
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsButton.Focus();
        }

        private void ChoosePreset(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            DurationInput.Text = button.Tag + ":00";
        }

        private void ApplyDuration(object sender, RoutedEventArgs e)
        {
            TimeSpan newDuration;
            if (!TryParseDuration(DurationInput.Text, out newDuration))
            {
                DurationHint.Text = "0:01〜99:59:59 の範囲で入力してください";
                DurationHint.Foreground = (Brush)FindResource("FinishedBrush");
                DurationInput.Focus();
                return;
            }

            if (newDuration != duration)
            {
                duration = newDuration;
                ResetCountdown(sender, e);
            }

            HideSettings();
        }

        private void ClearDurationError(object sender, TextChangedEventArgs e)
        {
            if (DurationHint != null)
            {
                DurationHint.Text = InputHint;
                DurationHint.Foreground = (Brush)FindResource("MutedBrush");
            }
        }

        private void HandleDurationKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ApplyDuration(sender, e);
            }
        }

        private void HandleEscape(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && SettingsPanel.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                HideSettings();
            }
        }

        private void ChangeOpacity(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityText != null)
            {
                Backdrop.Opacity = e.NewValue / 100;
                ProgressTrack.Opacity = Backdrop.Opacity;
                OpacityText.Text = FormattableString.Invariant($"{e.NewValue:0}%");
            }
        }

        private void ChooseDigitColor(object sender, RoutedEventArgs e)
        {
            digitColor = (SolidColorBrush)((Button)sender).Background;
            UpdateDigitColor();
        }

        private void PickDigitColor(object sender, RoutedEventArgs e)
        {
            var current = ((SolidColorBrush)DigitColorSample.Fill).Color;
            using (var picker = new Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
            })
            {
                if (picker.ShowDialog(this) == Forms.DialogResult.OK)
                {
                    var color = picker.Color;
                    digitColor = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
                    UpdateDigitColor();
                }
            }
        }

        private void UpdateDigitColor()
        {
            DigitColorSample.Fill = digitColor ?? (SolidColorBrush)FindResource("ForegroundBrush");
            TimeText.Foreground = digitColor ?? (Brush)FindResource(state == TimerState.Finished ? "FinishedBrush" : "ForegroundBrush");
        }

        private void ChangeDigitWeight(object sender, SelectionChangedEventArgs e)
        {
            if (((ListBox)sender).SelectedItem is ListBoxItem option && option.Tag is FontWeight weight)
            {
                TimeText.FontWeight = weight;
            }
        }

        private void ChangeDigitShadow(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ShadowOpacitySlider == null || ShadowBlurSlider == null)
            {
                return;
            }

            digitShadow.Opacity = ShadowOpacitySlider.Value / 100;
            digitShadow.BlurRadius = ShadowBlurSlider.Value;
            TimeText.Effect = digitShadow.Opacity > 0 ? digitShadow : null;
        }

        private void ResetDigitAppearance(object sender, RoutedEventArgs e)
        {
            digitColor = null;
            UpdateDigitColor();
            DefaultWeightOption.IsSelected = true;
            ShadowOpacitySlider.Value = DefaultShadowOpacity;
            ShadowBlurSlider.Value = DefaultShadowBlur;
        }

        private void PositionWindow(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left, area.Right - ActualWidth - 24);
            Top = area.Top + 24;
        }

        private void KeepSettingsOnScreen(object sender, SizeChangedEventArgs e)
        {
            if (!IsLoaded || !e.HeightChanged || SettingsPanel?.Visibility != Visibility.Visible)
            {
                return;
            }

            var source = PresentationSource.FromVisual(this);
            var area = Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var fromPixels = source.CompositionTarget.TransformFromDevice;
            double top = fromPixels.Transform(new Point(area.Left, area.Top)).Y;
            double bottom = fromPixels.Transform(new Point(area.Right, area.Bottom)).Y;
            Top = Math.Max(top, Math.Min(Top, bottom - ActualHeight));
        }

        private void DragPanel(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            refreshTimer.Stop();
            base.OnClosed(e);
        }
    }
}
