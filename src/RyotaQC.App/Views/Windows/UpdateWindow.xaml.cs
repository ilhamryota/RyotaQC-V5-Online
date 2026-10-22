using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RyotaQC.App.Models;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// Two-phase update dialog.
///
///   1. "Update Sekarang" downloads + verifies the installer (progress bar).
///   2. As soon as the download finishes the user is asked
///      "Ingin update sekarang atau nanti?"
///        - "Install Sekarang" -> the elevated helper installs, the app closes
///          and reopens on the new version.
///        - "Nanti"            -> the app keeps running on the current version
///          and the same prompt comes back on the next launch.
///
/// The dialog never claims success by itself: the installed version is read
/// back from disk after the installer exits (see InstallOutcome) and reported
/// on the next launch.
/// </summary>
public partial class UpdateWindow : Window
{
    private readonly IAppUpdateService _updateService;
    private readonly AppUpdateInfo _update;
    private readonly Version _currentVersion;

    private ReadyUpdate? _ready;
    private bool _busy;

    public UpdateWindow(IAppUpdateService updateService, AppUpdateInfo update, Version currentVersion)
    {
        _updateService = updateService;
        _update = update;
        _currentVersion = currentVersion;

        InitializeComponent();
        Closing += UpdateWindow_Closing;

        PopulateInfo();

        // A previous session may already have downloaded (and verified) this
        // exact target. In that case we skip straight to "install now / later".
        _ready = _updateService.GetReadyUpdate();
        if (_ready is not null && _ready.Target >= _update.Version)
        {
            ShowReadyState();
        }
    }

    private void PopulateInfo()
    {
        CurrentVersionText.Text = "v" + _currentVersion.ToString(3);
        NewVersionText.Text = _update.FormattedVersion;
        SubtitleText.Text = $"Versi terbaru RyotaQC {_update.FormattedVersion}";
        NoticeText.Text =
            "Update tersedia. Anda dapat memasangnya sekarang atau menundanya." +
            Environment.NewLine +
            "Memilih \"Nanti\" berarti aplikasi tetap berjalan pada versi sekarang.";
        NotesText.Text = FormatNotes(_update.ReleaseNotes);

        DownloadLabel.Text = "Siap mengunduh update";
        PercentText.Text = "0%";
        DownloadProgress.Value = 0;
        StatusText.Text = "Pastikan koneksi internet stabil sebelum update.";
        LaterButton.Visibility = Visibility.Collapsed;

        // If the release has no downloadable installer asset, tell the user
        // up-front instead of failing silently after a click.
        if (string.IsNullOrWhiteSpace(_update.DownloadUrl))
        {
            NoticeText.Text = "Rilis ini tidak menyertakan paket installer otomatis.";
            DownloadLabel.Text = "Installer tidak tersedia untuk diunduh otomatis";
            UpdateButton.Content = "Buka Halaman Rilis";
            UpdateButton.Click -= UpdateButton_Click;
            UpdateButton.Click += OpenReleasePage_Click;
            StatusText.Text = "Unduh installer manual dari halaman rilis, lalu jalankan sebagai Administrator.";
        }
    }

    // =====================================================================
    // States
    // =====================================================================

    private void ShowReadyState()
    {
        if (_ready is null)
        {
            return;
        }

        TitleText.Text = "Update Siap Dipasang!";
        SubtitleText.Text = $"{_ready.FormattedVersion} sudah diunduh & diverifikasi";
        NoticeText.Text =
            "Download update selesai. Ingin update sekarang atau nanti?" +
            Environment.NewLine +
            "Memilih \"Nanti\" berarti aplikasi tetap memakai versi sekarang.";
        NewVersionText.Text = _ready.FormattedVersion;

        NotesText.Text = FormatNotes(string.IsNullOrWhiteSpace(_update.ReleaseNotes)
            ? _ready.ReleaseNotes
            : _update.ReleaseNotes);

        DownloadLabel.Text = "Unduhan selesai & terverifikasi";
        PercentText.Text = "100%";
        DownloadProgress.Value = 100;

        UpdateButton.Content = "Install Sekarang";
        UpdateButton.IsEnabled = true;
        LaterButton.Visibility = Visibility.Visible;
        StatusText.Text =
            "Jika memilih Nanti, update akan ditawarkan lagi setiap aplikasi dibuka.";
    }

    private void ShowDownloadFailed(string? error)
    {
        TitleText.Text = "Update Gagal Diunduh";
        DownloadLabel.Text = "Unduhan gagal";
        UpdateButton.Content = "Coba Lagi";
        UpdateButton.IsEnabled = true;
        LaterButton.Visibility = Visibility.Visible;
        StatusText.Text = string.IsNullOrWhiteSpace(error)
            ? "Gagal mengunduh update. Periksa koneksi internet lalu coba lagi."
            : error;
    }

    private void ShowInstallFailed(string? error)
    {
        TitleText.Text = "Update Gagal Dipasang";
        DownloadLabel.Text = "Instalasi tidak dijalankan";
        UpdateButton.Content = "Coba Install Lagi";
        UpdateButton.IsEnabled = true;
        LaterButton.Visibility = Visibility.Visible;
        StatusText.Text = string.IsNullOrWhiteSpace(error)
            ? "Instalasi gagal dimulai. Coba lagi, atau install manual dari file yang sudah diunduh."
            : error;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CloseButton.IsEnabled = !busy;
        LaterButton.IsEnabled = !busy;
    }

    // =====================================================================
    // Actions
    // =====================================================================

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        // Already downloaded: this click means "install now".
        if (_ready is not null)
        {
            await InstallAsync();
            return;
        }

        await DownloadAsync();
    }

    private async Task DownloadAsync()
    {
        SetBusy(true);
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Mengunduh...";
        DownloadLabel.Text = "Mengunduh update...";
        StatusText.Text = "Jangan tutup aplikasi selama proses unduh berlangsung.";

        var progress = new Progress<int>(pct =>
        {
            DownloadProgress.Value = pct;
            PercentText.Text = $"{pct}%";
        });

        var (ok, error) = await _updateService.DownloadUpdateAsync(_update, progress);

        if (ok)
        {
            _ready = _updateService.GetReadyUpdate();
            if (_ready is null)
            {
                SetBusy(false);
                ShowDownloadFailed("Unduhan selesai tetapi file update tidak dapat dibaca. Coba lagi.");
                return;
            }

            SetBusy(false);
            ShowReadyState();
            return;
        }

        SetBusy(false);
        ShowDownloadFailed(error);
    }

    private async Task InstallAsync()
    {
        if (_ready is null)
        {
            return;
        }

        SetBusy(true);
        TitleText.Text = "Memasang Update...";
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Memasang...";
        LaterButton.Visibility = Visibility.Collapsed;
        DownloadLabel.Text = "Menginstall aplikasi...";
        StatusText.Text =
            "Aplikasi akan menutup otomatis, memasang versi terbaru, lalu terbuka kembali sendiri.";

        var (ok, error) = await _updateService.StartInstallAsync(_ready);
        if (!ok)
        {
            SetBusy(false);
            ShowInstallFailed(error);
            return;
        }

        // The elevated helper now waits for this process to exit. Give it a
        // moment to be fully up, then release every file handle immediately.
        await Task.Delay(1200);

        Application.Current.Shutdown();
        Environment.Exit(0);
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        Close();
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

    private void UpdateWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Downloading / installing must not be interrupted half-way.
        if (_busy)
        {
            e.Cancel = true;
        }
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
            // DragMove throws when the button is released before it starts.
        }
    }

    // =====================================================================
    // Release notes -> compact bullet list
    // =====================================================================

    private static string FormatNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "• Tidak ada catatan rilis untuk versi ini.";
        }

        var bullets = notes
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Where(line => line.StartsWith("- ") || line.StartsWith("* ")
                           || Regex.IsMatch(line, @"^\d+\.\s"))
            .Select(StripBullet)
            .Where(line => line.Length > 0)
            .Take(8)
            .ToList();

        if (bullets.Count == 0)
        {
            bullets = notes
                .Split('\n')
                .Select(line => CleanMarkdown(line.Trim()))
                .Where(line => line.Length > 0 && !line.StartsWith("#"))
                .Take(6)
                .ToList();
        }

        return bullets.Count == 0
            ? "• Tidak ada catatan rilis untuk versi ini."
            : string.Join(Environment.NewLine, bullets.Select(b => "• " + b));
    }

    private static string StripBullet(string line)
    {
        if (line.StartsWith("- ") || line.StartsWith("* "))
        {
            line = line[2..];
        }
        else
        {
            var dot = line.IndexOf('.');
            if (dot >= 0)
            {
                line = line[(dot + 1)..];
            }
        }

        return CleanMarkdown(line.Trim());
    }

    private static string CleanMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        text = text.Replace("**", string.Empty).Replace("__", string.Empty);
        text = text.Replace("`", string.Empty);
        return text.Trim();
    }
}
