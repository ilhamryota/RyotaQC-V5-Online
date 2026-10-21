using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using RyotaQC.App.Models;

namespace RyotaQC.App.Services;

/// <summary>
/// Checks GitHub releases for newer app versions and applies updates
/// by downloading the release asset (ZIP), extracting, and running an
/// external updater batch script that swaps files and restarts the app.
/// </summary>
public sealed class AppUpdateService : IAppUpdateService
{
    // Update these if the repo owner/name changes.
    private const string RepoOwner = "ilhamryota";
    private const string RepoName = "RyotaQC-V5-Online";
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";

    // Shared client for the small release-metadata call (fast, 20s is plenty).
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    // Dedicated client for downloading release assets. Installer packages are
    // hundreds of MB, so a short timeout would abort the transfer mid-stream.
    // 30 minutes covers very slow connections without hanging forever.
    private static readonly HttpClient DownloadHttp = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.Add("User-Agent", "RyotaQC-V5-Online-UpdateCheck");
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagEl) || tagEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var tag = tagEl.GetString() ?? string.Empty;
            var versionStr = tag.TrimStart('v', 'V').Trim();
            if (!Version.TryParse(versionStr, out var remoteVersion))
            {
                return null;
            }

            // Find the best update asset from the release. We prefer (in order):
            //   1. An installer EXE (RyotaQC-V5-Setup-*.exe) - the official package
            //   2. A ZIP bundle (legacy / portable updates)
            // Other assets (checksums, release notes) are skipped.
            string downloadUrl = string.Empty;
            long downloadSize = 0;
            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                string zipUrl = string.Empty;
                long zipSize = 0;

                foreach (var asset in assetsEl.EnumerateArray())
                {
                    if (!asset.TryGetProperty("browser_download_url", out var urlEl))
                    {
                        continue;
                    }
                    var url = urlEl.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    long assetSize = 0;
                    if (asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var sz))
                    {
                        assetSize = sz;
                    }

                    var hasName = asset.TryGetProperty("name", out var nameEl)
                        && nameEl.ValueKind == JsonValueKind.String;
                    var assetName = hasName ? (nameEl.GetString() ?? string.Empty) : string.Empty;
                    var fileName = assetName.Length > 0
                        ? assetName
                        : Path.GetFileName(url);

                    // 1. Prefer the official installer EXE (highest priority).
                    if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = url;
                        downloadSize = assetSize;
                        break;
                    }

                    // 2. Remember the first ZIP as a fallback.
                    if (zipUrl.Length == 0
                        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zipUrl = url;
                        zipSize = assetSize;
                    }
                }

                // No installer EXE found -> fall back to ZIP if available.
                if (downloadUrl.Length == 0 && zipUrl.Length > 0)
                {
                    downloadUrl = zipUrl;
                    downloadSize = zipSize;
                }
            }

            var releaseUrl = root.TryGetProperty("html_url", out var urlProperty) && urlProperty.ValueKind == JsonValueKind.String
                ? urlProperty.GetString() ?? string.Empty
                : string.Empty;

            var notes = root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String
                ? bodyEl.GetString() ?? string.Empty
                : string.Empty;

            DateTime publishedAt = default;
            if (root.TryGetProperty("published_at", out var pubEl) && pubEl.ValueKind == JsonValueKind.String)
            {
                DateTime.TryParse(pubEl.GetString(), out publishedAt);
            }

            // If the release has no downloadable asset, we still surface it
            // so the UI can direct the user to the release page.
            return new AppUpdateInfo
            {
                Version = remoteVersion,
                DownloadUrl = downloadUrl,
                ReleaseUrl = releaseUrl,
                ReleaseNotes = notes,
                PublishedAt = publishedAt,
                DownloadSizeBytes = downloadSize
            };
        }
        catch (Exception)
        {
            // Network errors, DNS failures, parse errors -> no update available.
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> DownloadAndApplyUpdateAsync(AppUpdateInfo update, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            return (false, "Rilis ini tidak memiliki paket installer yang bisa diunduh.");
        }

        var isInstaller = update.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(update.DownloadUrl).Contains("Setup", StringComparison.OrdinalIgnoreCase);

        var tempDir = Path.Combine(Path.GetTempPath(), "RyotaQC_Update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // Installs use the same download mechanism but a different apply step.
        var downloadPath = Path.Combine(tempDir, isInstaller ? "RyotaQC-Setup.exe" : "update.zip");
        var extractDir = Path.Combine(tempDir, "extracted");

        try
        {
            // Download the release asset.
            using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
            request.Headers.Add("User-Agent", "RyotaQC-V5-Online-Updater");

            using var response = await DownloadHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"HTTP {(int)response.StatusCode} saat mengunduh {Path.GetFileName(update.DownloadUrl)}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? update.DownloadSizeBytes;
            long receivedBytes = 0;

            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    receivedBytes += read;
                    if (totalBytes > 0)
                    {
                        var pct = (int)((receivedBytes * 100) / totalBytes);
                        progress?.Report(pct);
                    }
                }
            }

            progress?.Report(100);

            if (isInstaller)
            {
                // Launch the official installer silently and exit. Inno Setup
                // handles closing the running app, installing, and relaunching.
                // /SILENT shows progress; /NORESTART avoids a reboot prompt;
                // /CLOSEAPPLICATIONS closes the running RyotaQC instance.
                var installerPsi = new ProcessStartInfo
                {
                    FileName = downloadPath,
                    Arguments = "/SILENT /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true,
                    WorkingDirectory = tempDir
                };
                Process.Start(installerPsi);
                return (true, null);
            }

            // ---- Legacy ZIP path: extract and swap files via a batch script. ----

            // Extract the ZIP.
            ZipFile.ExtractToDirectory(downloadPath, extractDir, overwriteFiles: true);

            // If the ZIP contains a single top-level folder, descend into it
            // so the updater copies the actual app files, not a wrapper folder.
            var sourceDir = extractDir;
            var topEntries = Directory.GetFileSystemEntries(extractDir);
            if (topEntries.Length == 1 && Directory.Exists(topEntries[0]))
            {
                sourceDir = topEntries[0];
            }

            var appDir = AppContext.BaseDirectory;
            var appExe = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(appDir, "RyotaQC.App.exe");
            var pid = Environment.ProcessId;

            // Build the updater batch script. It waits for the current app
            // process to exit, copies the new files over, restarts the app,
            // and cleans up the temp directory.
            var updaterPath = Path.Combine(tempDir, "apply_update.bat");
            var script =
                "@echo off" + Environment.NewLine +
                "chcp 65001 >nul" + Environment.NewLine +
                "title RyotaQC Updater" + Environment.NewLine +
                "setlocal" + Environment.NewLine +
                "set \"SOURCE=" + sourceDir + "\"" + Environment.NewLine +
                "set \"TARGET=" + appDir + "\"" + Environment.NewLine +
                "set \"APP_EXE=" + appExe + "\"" + Environment.NewLine +
                "set \"PID=" + pid + "\"" + Environment.NewLine +
                "set \"TEMP_DIR=" + tempDir + "\"" + Environment.NewLine +
                Environment.NewLine +
                "echo Menunggu aplikasi RyotaQC ditutup..." + Environment.NewLine +
                ":wait_loop" + Environment.NewLine +
                "tasklist /FI \"PID eq %PID%\" 2>nul | find \"%PID%\" >nul" + Environment.NewLine +
                "if not errorlevel 1 (" + Environment.NewLine +
                "  timeout /t 1 /nobreak >nul" + Environment.NewLine +
                "  goto wait_loop" + Environment.NewLine +
                ")" + Environment.NewLine +
                Environment.NewLine +
                "echo Menyalin file update..." + Environment.NewLine +
                "xcopy /E /Y /I \"%SOURCE%\\*\" \"%TARGET%\\\" >nul" + Environment.NewLine +
                Environment.NewLine +
                "echo Memulai ulang aplikasi..." + Environment.NewLine +
                "start \"\" \"%APP_EXE%\"" + Environment.NewLine +
                Environment.NewLine +
                "echo Membersihkan folder sementara..." + Environment.NewLine +
                "rmdir /S /Q \"%TEMP_DIR%\"" + Environment.NewLine +
                "exit" + Environment.NewLine;

            await File.WriteAllTextAsync(updaterPath, script);

            // Launch the updater. Hide the window initially; the script will
            // surface its own title bar so the user sees progress text.
            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Minimized,
                CreateNoWindow = false
            };
            Process.Start(psi);

            // Request the host to shut down. The updater takes over from here.
            return (true, null);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup on failure.
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
            catch { /* ignore */ }
            return (false, ex.Message);
        }
    }
}
