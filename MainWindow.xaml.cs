using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Countdown
{
    public partial class MainWindow : Window, Forms.IWin32Window
    {
        private enum TimerState { Ready, Running, Paused, Finished }

        private const string InputHint = "分:秒、または 時:分:秒";
        private static readonly Geometry PlayIcon = Geometry.Parse("M 0,0 L 11,6 L 0,12 Z");
        private static readonly Geometry PauseIcon = Geometry.Parse("M 0,0 H 4 V 12 H 0 Z M 7,0 H 11 V 12 H 7 Z");

        private readonly Stopwatch elapsed = new Stopwatch();
        private readonly DispatcherTimer refreshTimer;
        private readonly CountdownSettings initialSettings;
        private readonly DisplayWindow displayWindow;
        private TimeSpan duration;
        private TimerState state = TimerState.Ready;
        private SolidColorBrush digitColor;

        IntPtr Forms.IWin32Window.Handle => new WindowInteropHelper(this).Handle;

        public MainWindow()
        {
            displayWindow = new DisplayWindow();
            InitializeComponent();
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            refreshTimer.Tick += RefreshCountdown;
            initialSettings = CountdownSettings.Load();
            duration = TimeSpan.FromSeconds(initialSettings.DurationSeconds);
            OpacitySlider.Value = initialSettings.BackgroundOpacity;
            digitColor = initialSettings.DigitColor is Color color ? new SolidColorBrush(color) : null;
            var weight = FontWeight.FromOpenTypeWeight(initialSettings.DigitWeight);
            WeightOptions.SelectedItem = WeightOptions.Items.OfType<ListBoxItem>()
                .FirstOrDefault(item => Equals(item.Tag, weight)) ?? DefaultWeightOption;
            ShadowOpacitySlider.Value = initialSettings.ShadowOpacity;
            ShadowBlurSlider.Value = initialSettings.ShadowBlur;
            DigitScaleSlider.Maximum = CountdownSettings.MaxDigitScalePercent;
            DigitScaleSlider.Minimum = CountdownSettings.MinDigitScalePercent;
            DigitScaleSlider.Value = initialSettings.DigitScalePercent;
            DigitScaleSlider.ValueChanged += ChangeDigitScale;
            displayWindow.SetDigitScale(initialSettings.DigitScalePercent, null);
            displayWindow.DigitScaleChanged += percent => DigitScaleSlider.Value = percent;
            UpdateControls();
            UpdateDisplay(duration);
            InitializeUpdateChecking();
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
            bool showHours = duration.TotalHours >= 1;
            displayWindow.SetTime(FormatTime((int)Math.Ceiling(remaining.TotalSeconds), showHours), showHours);
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

            ShowSettings();
        }

        private void ShowSettings()
        {
            if (SettingsPanel.Visibility == Visibility.Collapsed)
            {
                DurationInput.Text = FormatTime((int)duration.TotalSeconds, duration.TotalHours >= 1);
                SettingsPanel.Visibility = Visibility.Visible;
            }
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
                displayWindow.SetBackgroundOpacity(e.NewValue / 100);
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
            displayWindow.SetDigitColor(digitColor ?? (Brush)FindResource(state == TimerState.Finished ? "FinishedBrush" : "ForegroundBrush"));
        }

        private void ChangeDigitWeight(object sender, SelectionChangedEventArgs e)
        {
            if (((ListBox)sender).SelectedItem is ListBoxItem option && option.Tag is FontWeight weight)
            {
                displayWindow.DigitWeight = weight;
            }
        }

        private void ChangeDigitShadow(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ShadowOpacitySlider == null || ShadowBlurSlider == null)
            {
                return;
            }

            displayWindow.SetDigitShadow(ShadowOpacitySlider.Value / 100, ShadowBlurSlider.Value);
        }

        private void ChangeDigitScale(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int percent = (int)e.NewValue;
            if (percent == displayWindow.DigitScalePercent)
            {
                return;
            }

            Rect? controlsBounds = IsLoaded ? new Rect(Left, Top, ActualWidth, ActualHeight) : (Rect?)null;
            displayWindow.SetDigitScale(percent, controlsBounds);
        }

        private void ResetDigitAppearance(object sender, RoutedEventArgs e)
        {
            digitColor = null;
            UpdateDigitColor();
            DefaultWeightOption.IsSelected = true;
            ShadowOpacitySlider.Value = CountdownSettings.DefaultShadowOpacity;
            ShadowBlurSlider.Value = CountdownSettings.DefaultShadowBlur;
            DigitScaleSlider.Value = CountdownSettings.DefaultDigitScalePercent;
        }

        private void ShowDisplayWindow(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            displayWindow.Left = Math.Max(area.Left, area.Right - displayWindow.Width - 24);
            displayWindow.Top = Math.Max(area.Top, Math.Min(area.Top + 24, area.Bottom - displayWindow.Height));

            double besideDisplay = displayWindow.Left - ActualWidth - 10;
            Left = besideDisplay >= area.Left
                ? besideDisplay
                : Math.Max(area.Left, Math.Min(displayWindow.Left, area.Right - ActualWidth));
            Top = besideDisplay >= area.Left
                ? displayWindow.Top
                : Math.Max(area.Top, Math.Min(displayWindow.Top + displayWindow.Height + 10, area.Bottom - ActualHeight));

            displayWindow.Closed += DisplayWindowClosed;
            displayWindow.Show();
        }

        private void DisplayWindowClosed(object sender, EventArgs e)
        {
            Close();
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

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel)
            {
                return;
            }

            var settings = new CountdownSettings
            {
                DurationSeconds = (int)duration.TotalSeconds,
                BackgroundOpacity = OpacitySlider.Value,
                DigitColor = digitColor?.Color,
                DigitWeight = displayWindow.DigitWeight.ToOpenTypeWeight(),
                DigitScalePercent = displayWindow.DigitScalePercent,
                ShadowOpacity = ShadowOpacitySlider.Value,
                ShadowBlur = ShadowBlurSlider.Value
            };

            try
            {
                settings.SaveIfChanged(initialSettings);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "設定を保存できませんでした。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            displayWindow.Closed -= DisplayWindowClosed;
            refreshTimer.Stop();
            updateCheckTimer.Stop();
            updateLifetime.Cancel();
            updateLifetime.Dispose();
            base.OnClosed(e);
        }
    }
}
