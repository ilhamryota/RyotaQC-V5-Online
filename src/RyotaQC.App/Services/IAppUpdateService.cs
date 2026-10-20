using System;
using System.Threading.Tasks;
using RyotaQC.App.Models;

namespace RyotaQC.App.Services;

public interface IAppUpdateService
{
    /// <summary>Get the currently running app version from assembly.</summary>
    Version GetCurrentVersion();

    /// <summary>Check GitHub releases for a newer version. Returns null if no update or check failed.</summary>
    Task<AppUpdateInfo?> CheckForUpdateAsync();

    /// <summary>
    /// Download the update package, extract it, and launch an external updater
    /// that swaps files and restarts the app. Returns true if updater was launched.
    /// </summary>
    Task<bool> DownloadAndApplyUpdateAsync(AppUpdateInfo update, IProgress<int>? progress = null);
}
