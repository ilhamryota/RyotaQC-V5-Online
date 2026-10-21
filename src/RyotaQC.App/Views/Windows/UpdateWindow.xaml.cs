using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using RyotaQC.App.Models;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

public partial class UpdateWindow : Window
{
    private readonly IAppUpdateService _updateService;
    private readonly AppUpdateInfo _update;
    private readonly Version _currentVersion;
    private bool _isUpdating;
    private bool _updateStarted;

    public UpdateWindow(IAppUpdateService updateService, AppUpdateInfo update, Version currentVersion)
    {
        _updateService = updateService;
        _update = update;
        _currentVersion = currentVersion;

        InitializeComponent();
        PopulateInfo();
    }

    private void PopulateInfo()
    {
        VersionText.Text = _update.FormattedVersion;
        CurrentVersionText.Text = "v" + _currentVersion.ToString(3);
        NewVersionText.Text = _update.FormattedVersion;
        DateText.Text = "Tanggal rilis: " + _update.FormattedPublishedDate;
        NotesText.Text = string.IsNullOrWhiteSpace(_update.ReleaseNotes)
            ? "Tidak ada catatan rilis."
            : _update.ReleaseNotes;

        // If the release has no downloadable installer asset, tell the user
        // up-front instead of failing silently after a click.
        if (string.IsNullOrWhiteSpace(_update.DownloadUrl))
        {
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgress.Value = 0;
            ProgressText.Text = "Paket installer tidak tersedia di rilis ini. Buka halaman rilis untuk mengunduh manual.";
            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "Buka Halaman Rilis";
            UpdateButton.Click -= UpdateButton_Click;
            UpdateButton.Click += OpenReleasePage_Click;
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }
        _isUpdating = true;
        _updateStarted = true;

        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Mengunduh...";
        ProgressPanel.Visibility = Visibility.Visible;

        var progress = new Progress<int>(pct =>
        {
            DownloadProgress.Value = pct;
            ProgressText.Text = $"Mengunduh update... {pct}%";
        });

        var (ok, error) = await _updateService.DownloadAndApplyUpdateAsync(_update, progress);

        if (ok)
        {
            ProgressText.Text = "Update selesai. Aplikasi akan dimulai ulang...";
            UpdateButton.Content = "Menutup...";

            // Give the installer / updater a moment to spin up before we exit.
            await Task.Delay(1500);

            Application.Current.Shutdown();
        }
        else
        {
            ProgressText.Text = string.IsNullOrWhiteSpace(error)
                ? "Gagal mengunduh update. Periksa koneksi internet lalu coba lagi."
                : $"Gagal mengunduh update: {error}";
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = "Coba Lagi";
            _isUpdating = false;
            _updateStarted = false;
        }
    }

    private void OpenReleasePage_Click(object sender, RoutedEventArgs e)
    {
        var url = !string.IsNullOrWhiteSpace(_update.ReleaseUrl)
            ? _update.ReleaseUrl
            : "https://github.com/ilhamryota/RyotaQC-V5-Online/releases/latest";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore failures opening the browser.
        }
    }

    /// <summary>
    /// Prevents the user from closing the window (via the X button or Alt+F4)
    /// once an update has started. Before the update starts, allow closing
    /// only via the mandatory Update button path (no "later" option).
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Block closing once the update is in progress.
        if (_updateStarted)
        {
            e.Cancel = true;
            return;
        }

        // Even before the update starts, we make the update mandatory:
        // block the close (X) button so the user cannot dismiss the dialog
        // without updating. They can still minimize / move the window.
        // The only way forward is to click "Update Sekarang".
        e.Cancel = true;

        // Show a toast-style hint to remind the user the update is mandatory.
        MessageBox.Show(
            "Update ini wajib diinstal. Klik tombol \"Update Sekarang\" untuk melanjutkan.",
            "Update Wajib",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}