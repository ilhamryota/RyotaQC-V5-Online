using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RyotaQC.App.Enums;
using RyotaQC.App.Services;
using RyotaQC.App.Views.Windows;

namespace RyotaQC.App.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly INavigationService _navigationService;
    private readonly IUiFeedbackService _feedbackService;
    private readonly IAppUpdateService _updateService;
    private readonly IAudioFeedbackService _audio;

    public SettingsPageViewModel(
        IAppSettingsService settingsService,
        IStartupRegistrationService startupRegistrationService,
        INavigationService navigationService,
        IUiFeedbackService feedbackService,
        IAppUpdateService updateService,
        IAudioFeedbackService audio)
    {
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;
        _navigationService = navigationService;
        _feedbackService = feedbackService;
        _updateService = updateService;
        _audio = audio;

        AppTitle = settingsService.Current.AppTitle;
        Version = "V" + _updateService.GetCurrentVersion().ToString(3);
        Copyright = "Copyright © 2026 RyotaQC. All rights reserved.";
        Description = "RyotaQC V5 adalah aplikasi Quality Control desktop untuk diagnosa sistem, launcher tools teknisi, wallpaper setup, dan workflow verifikasi perangkat.";

        launchOnStartup = settingsService.Current.LaunchOnStartup;
        UpdateStatusText = "Belum diperiksa.";

        BackToHomeCommand = new RelayCommand(() =>
        {
            _audio.PlayExit();
            _navigationService.Navigate(AppPage.Dashboard);
        });
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
    }

    public string AppTitle { get; }

    public string Version { get; }

    public string Description { get; }

    public string Copyright { get; }

    public IRelayCommand BackToHomeCommand { get; }

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    [ObservableProperty]
    private bool launchOnStartup;

    [ObservableProperty]
    private string updateStatusText;

    [ObservableProperty]
    private bool isCheckingUpdate;

    partial void OnLaunchOnStartupChanged(bool value)
    {
        _settingsService.Current.LaunchOnStartup = value;

        if (_settingsService.Save(_settingsService.Current, out var error))
        {
            if (value)
            {
                _startupRegistrationService.EnsureStartupEnabled();
            }
            else
            {
                _startupRegistrationService.DisableStartup();
            }

            return;
        }

        launchOnStartup = !value;
        _feedbackService.ShowToast(string.IsNullOrWhiteSpace(error)
            ? "Gagal menyimpan pengaturan startup."
            : $"Gagal menyimpan pengaturan startup: {error}", false);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdate)
        {
            return;
        }
        IsCheckingUpdate = true;
        UpdateStatusText = "Memeriksa update...";

        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is null)
            {
                UpdateStatusText = "Tidak dapat menghubungi server update. Coba lagi nanti.";
                _feedbackService.ShowToast("Gagal memeriksa update.", false);
                return;
            }

            var current = _updateService.GetCurrentVersion();
            if (!update.IsNewerThan(current))
            {
                UpdateStatusText = $"Anda sudah menggunakan versi terbaru (v{current.ToString(3)}).";
                _feedbackService.ShowToast("RyotaQC sudah versi terbaru.", true);
                return;
            }

            UpdateStatusText = $"Update tersedia: {update.FormattedVersion}";
            var window = new UpdateWindow(_updateService, update, current)
            {
                Owner = Application.Current.MainWindow
            };
            window.Show();
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }
}
