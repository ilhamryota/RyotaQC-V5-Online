using System;

namespace RyotaQC.App.Models;

public sealed class AppUpdateInfo
{
    public Version Version { get; init; } = new(0, 0, 0);
    public string ReleaseUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public long DownloadSizeBytes { get; init; }

    /// <summary>
    /// File name of the release asset behind <see cref="DownloadUrl"/>
    /// (e.g. "RyotaQC-V5-Setup-x64-v5.0.6.exe"). Used to locate the matching
    /// line in the release's SHA256SUMS.txt asset.
    /// </summary>
    public string DownloadFileName { get; init; } = string.Empty;

    /// <summary>
    /// Download URL of the release's SHA256SUMS.txt asset, when published.
    /// Empty means integrity verification by checksum is unavailable and the
    /// updater falls back to the size/PE checks only.
    /// </summary>
    public string ChecksumsUrl { get; init; } = string.Empty;

    public bool IsNewerThan(Version current) => Version > current;

    public string FormattedVersion => $"v{Version.Major}.{Version.Minor}.{Version.Build}";

    public string FormattedPublishedDate => PublishedAt == default
        ? "Tanggal tidak diketahui"
        : PublishedAt.ToString("dd MMMM yyyy HH:mm");
}
