using System;

namespace RyotaQC.App.Models;

/// <summary>
/// An update that has already been downloaded AND integrity-verified, but not
/// installed yet. Persisted in %LOCALAPPDATA%\RyotaQC\updates\ready-update.json
/// so the "install later" choice survives closing the application.
/// </summary>
public sealed class ReadyUpdate
{
    public Version Target { get; init; } = new(0, 0, 0);

    /// <summary>Full path of the downloaded, checksum-verified installer EXE.</summary>
    public string InstallerPath { get; init; } = string.Empty;

    /// <summary>Release asset name, e.g. "RyotaQC-V5-Setup-x64-v5.0.8.exe".</summary>
    public string AssetName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public DateTime DownloadedAt { get; init; }

    public string ReleaseUrl { get; init; } = string.Empty;

    /// <summary>Release notes captured at download time (shown again on the next launch).</summary>
    public string ReleaseNotes { get; init; } = string.Empty;

    public string FormattedVersion => $"v{Target.Major}.{Target.Minor}.{Target.Build}";

    public string FormattedDownloadedAt => DownloadedAt == default
        ? "waktu tidak diketahui"
        : DownloadedAt.ToString("dd MMM yyyy HH:mm");
}

/// <summary>
/// Result of an install attempt, written by the elevated helper script right
/// after the installer exits. It is the evidence that the new version really
/// landed on disk - the exact check that was missing when an "update" left the
/// application on the old version.
/// </summary>
public sealed class InstallOutcome
{
    public Version Target { get; init; } = new(0, 0, 0);

    /// <summary>Exit code reported by the installer (0 = success).</summary>
    public int ExitCode { get; init; }

    /// <summary>FileVersion read from the installed RyotaQC.App.exe ("" when unreadable).</summary>
    public string InstalledVersion { get; init; } = string.Empty;

    /// <summary>
    /// True only when the installer exited successfully AND the executable on
    /// disk reports a version equal to (or newer than) the update target.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>Full path of Inno Setup's own /LOG file (for diagnostics).</summary>
    public string InstallerLogPath { get; init; } = string.Empty;

    public DateTime FinishedAt { get; init; }

    public string FormattedTarget => $"v{Target.Major}.{Target.Minor}.{Target.Build}";
}
