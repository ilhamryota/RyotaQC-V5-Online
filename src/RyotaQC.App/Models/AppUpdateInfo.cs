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

    public bool IsNewerThan(Version current) => Version > current;

    public string FormattedVersion => $"v{Version.Major}.{Version.Minor}.{Version.Build}";

    public string FormattedPublishedDate => PublishedAt == default
        ? "Tanggal tidak diketahui"
        : PublishedAt.ToString("dd MMMM yyyy HH:mm");
}
