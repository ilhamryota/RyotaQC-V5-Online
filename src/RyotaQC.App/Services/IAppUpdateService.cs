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
    /// Downloads the release's installer EXE, verifies its size, PE header and
    /// SHA-256, then stores it in the persistent updates folder and records a
    /// "ready to install" marker. Nothing is installed here: the caller decides
    /// whether to install now or later.
    /// </summary>
    Task<(bool Success, string? Error)> DownloadUpdateAsync(AppUpdateInfo update, IProgress<int>? progress = null);

    /// <summary>
    /// Returns the previously downloaded update that is waiting to be installed
    /// (null when there is none, or when it is already outdated).
    /// </summary>
    ReadyUpdate? GetReadyUpdate();

    /// <summary>
    /// Starts the elevated helper that waits for this process to exit, runs the
    /// installer silently, records the result and relaunches the application.
    /// The caller is expected to shut the app down right after this succeeds.
    /// </summary>
    Task<(bool Success, string? Error)> StartInstallAsync(ReadyUpdate ready);

    /// <summary>
    /// Reads (and removes) the result written by the install helper. Returns
    /// null when there is no pending result. Called once at startup.
    /// </summary>
    InstallOutcome? ConsumeInstallOutcome();

    /// <summary>Deletes the pending download marker and its installer file.</summary>
    void DiscardReadyUpdate();
}
