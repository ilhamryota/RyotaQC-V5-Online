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
    /// Download the update package (installer EXE preferred, ZIP as fallback),
    /// then launch it so it can replace the app and restart it.
    /// Returns (success, errorMessage).
    /// </summary>
    Task<(bool Success, string? Error)> DownloadAndApplyUpdateAsync(AppUpdateInfo update, IProgress<int>? progress = null);
}
