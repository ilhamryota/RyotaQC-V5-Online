using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RyotaQC.App.Services;
using RyotaQC.App.ViewModels;
using RyotaQC.App.ViewModels.Pages;
using RyotaQC.App.Views.Pages;

namespace RyotaQC.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers — catch unhandled exceptions so they are
        // logged instead of silently crashing the app.
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var logger = new FileAppLogger();
                logger.Error("Unhandled dispatcher exception.", args.Exception);
            }
            catch { }
            MessageBox.Show(
                "Terjadi error: " + args.Exception.Message +
                "\n\nDetail telah dicatat di log.",
                "RyotaQC V5",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var logger = new FileAppLogger();
                logger.Error("Unhandled AppDomain exception.", args.ExceptionObject as Exception);
            }
            catch { }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                var logger = new FileAppLogger();
                logger.Error("Unobserved task exception.", args.Exception);
            }
            catch { }
            args.SetObserved();
        };

        // Run startup synchronously enough to surface fatal errors via the
        // WPF dispatcher. Async work is awaited through a safe helper so an
        // unhandled exception is logged and re-thrown on the dispatcher
        // instead of being silently swallowed (the classic async-void trap).
        try
        {
            RunStartup();
        }
        catch (Exception ex)
        {
            var logger = new FileAppLogger();
            // Log the FULL exception with stack trace and inner exceptions
            var detail = ex.ToString();
            if (ex.InnerException != null)
            {
                detail += "\n\n--- INNER EXCEPTION ---\n" + ex.InnerException.ToString();
                if (ex.InnerException.InnerException != null)
                {
                    detail += "\n\n--- INNER INNER EXCEPTION ---\n" + ex.InnerException.InnerException.ToString();
                }
            }
            logger.Error("Fatal error during startup. Details:\n" + detail, ex);
            MessageBox.Show(
                "RyotaQC V5 gagal dimulai.\n\n" + ex.Message +
                "\n\nInner: " + (ex.InnerException?.Message ?? "none") +
                "\n\nDetail telah dicatat di log.",
                "RyotaQC V5",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void RunStartup()
    {
        Services = ConfigureServices();

        var appSettings = Services.GetRequiredService<IAppSettingsService>();
        appSettings.Load();

        var toolConfig = Services.GetRequiredService<IToolConfigService>();
        toolConfig.Load();

        var splashDelayMs = Math.Clamp(appSettings.Current.SplashDelayMs, 0, 3000);

        Views.Windows.SplashWindow? splash = null;
        if (splashDelayMs > 0)
        {
            splash = Services.GetRequiredService<Views.Windows.SplashWindow>();
            splash.Show();
            Thread.Sleep(splashDelayMs);
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        if (splash is not null)
        {
            splash.Close();
        }

        mainWindow.Show();

        // Fire-and-forget background check for app updates. We wait a few
        // seconds after the window appears so the startup path stays snappy
        // and any update prompt surfaces once the UI is ready.
        _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            // Give the main window time to render before we hit the network.
            await Task.Delay(5000);

            var updateService = Services.GetRequiredService<IAppUpdateService>();
            var update = await updateService.CheckForUpdateAsync();

            // If the previous update did not actually change the installed
            // version, tell the user instead of letting it pass silently - the
            // main menu would otherwise keep showing the old version with no
            // explanation.
            var pendingWarning = AppUpdateService.ConsumePendingUpdateWarning();
            if (!string.IsNullOrWhiteSpace(pendingWarning))
            {
                Dispatcher.Invoke(() => MessageBox.Show(
                    pendingWarning,
                    "Update Tidak Terpasang",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
            }

            if (update is null)
            {
                return;
            }

            var current = updateService.GetCurrentVersion();
            if (!update.IsNewerThan(current))
            {
                return;
            }

            // Surface the update prompt on the UI thread. MANDATORY: this is
            // a modal dialog (ShowDialog) — the user cannot use the app
            // until the update is applied.
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var window = new Views.Windows.UpdateWindow(updateService, update, current)
                    {
                        Owner = MainWindow
                    };
                    // Modal: blocks the main window until the update completes.
                    window.ShowDialog();
                }
                catch
                {
                    // Showing the window is best-effort; never crash on it.
                }
            });
        }
        catch
        {
            // Update check is purely advisory. Any failure should be silent.
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppLogger, FileAppLogger>();
        services.AddSingleton<IUiFeedbackService, UiFeedbackService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<IToolConfigService, ToolConfigService>();
        services.AddSingleton<IExternalToolLauncher, ExternalToolLauncher>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IQualityControlRunner, QualityControlRunner>();
        services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();
        services.AddSingleton<IAudioFeedbackService, AudioFeedbackService>();
        services.AddSingleton<IErpSyncService, ErpSyncService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddSingleton<HomeScreenViewModel>();

        services.AddSingleton<DashboardPageViewModel>();
        services.AddSingleton<SystemInformationPageViewModel>();
        services.AddSingleton<WallpaperDesktopPageViewModel>();
        services.AddSingleton<QualityControlPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();

        services.AddSingleton<DashboardPage>();
        services.AddSingleton<SystemInformationPage>();
        services.AddSingleton<WallpaperDesktopPage>();
        services.AddSingleton<QualityControlPage>();
        services.AddSingleton<SettingsPage>();

        services.AddTransient<Views.Windows.SplashWindow>();

        return services.BuildServiceProvider();
    }
}
