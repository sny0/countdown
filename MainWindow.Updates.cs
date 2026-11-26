using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Countdown
{
    public partial class MainWindow
    {
        private readonly Version installedVersion = new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
        private readonly DispatcherTimer updateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        private readonly CancellationTokenSource updateLifetime = new CancellationTokenSource();
        private CountdownUpdate availableUpdate;
        private bool updateBusy;

        private void InitializeUpdateChecking()
        {
            InstalledVersionText.Text = $"現在のバージョン v{installedVersion}";
            Loaded += async (sender, args) =>
            {
                updateCheckTimer.Start();
                await CheckForUpdateAsync();
            };
            updateCheckTimer.Tick += async (sender, args) => await CheckForUpdateAsync();
        }

        private void OpenUpdateSettings(object sender, RoutedEventArgs e)
        {
            UpdateTab.IsSelected = true;
            ShowSettings();
            UpdateTab.Focus();
        }

        private async void CheckForUpdates(object sender, RoutedEventArgs e)
        {
            await CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            if (updateBusy)
            {
                return;
            }

            var cancellation = updateLifetime.Token;
            SetUpdateBusy(true);
            UpdateStatusText.Text = "更新を確認しています…";
            try
            {
                availableUpdate = await CountdownUpdate.CheckAsync(installedVersion, cancellation);
                UpdateStatusText.Text = availableUpdate == null ? "更新はありません。" : $"v{availableUpdate.ReleaseVersion} に更新できます。";
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException
                || error is SerializationException || error is System.Xml.XmlException || error is InvalidDataException || error is IOException)
            {
                availableUpdate = null;
                UpdateStatusText.Text = "更新を確認できませんでした。時間をおいて再試行してください。";
            }
            finally
            {
                if (!cancellation.IsCancellationRequested)
                {
                    SetUpdateBusy(false);
                }
            }
        }

        private async void InstallUpdate(object sender, RoutedEventArgs e)
        {
            var update = availableUpdate;
            if (updateBusy || update == null)
            {
                return;
            }

            SetUpdateBusy(true);
            var cancellation = updateLifetime.Token;
            UpdateStatusText.Text = "インストーラーをダウンロードしています…";
            try
            {
                var path = await update.DownloadInstallerAsync(cancellation);
                cancellation.ThrowIfCancellationRequested();

                var installer = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), $"/i \"{path}\" /norestart")
                {
                    UseShellExecute = true
                };
                using (Process.Start(installer))
                {
                    Close();
                }
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException
                || error is InvalidDataException || error is IOException || error is UnauthorizedAccessException
                || error is Win32Exception || error is InvalidOperationException)
            {
                UpdateStatusText.Text = error is InvalidDataException
                    ? "インストーラーを検証できませんでした。更新を確認し直してください。"
                    : "インストーラーを開けませんでした。時間をおいて再試行してください。";
            }
            finally
            {
                if (!cancellation.IsCancellationRequested)
                {
                    SetUpdateBusy(false);
                }
            }
        }

        private void SetUpdateBusy(bool busy)
        {
            updateBusy = busy;
            CheckUpdateButton.IsEnabled = !busy;
            InstallUpdateButton.IsEnabled = !busy;
            InstallUpdateButton.Visibility = availableUpdate == null ? Visibility.Collapsed : Visibility.Visible;
            UpdateNoticeButton.Visibility = InstallUpdateButton.Visibility;
        }
    }
}
