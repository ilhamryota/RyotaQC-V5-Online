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

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
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

            // Find the first asset that looks like an update package (ZIP).
            string downloadUrl = string.Empty;
            long downloadSize = 0;
            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
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
                    downloadUrl = url;
                    if (asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var sz))
                    {
                        downloadSize = sz;
                    }
                    break;
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

    public async Task<bool> DownloadAndApplyUpdateAsync(AppUpdateInfo update, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            return false;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "RyotaQC_Update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, "update.zip");
        var extractDir = Path.Combine(tempDir, "extracted");

        try
        {
            // Download the release asset.
            using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
            request.Headers.Add("User-Agent", "RyotaQC-V5-Online-Updater");

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? update.DownloadSizeBytes;
            long receivedBytes = 0;

            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
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

            // Extract the ZIP.
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

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
            return true;
        }
        catch (Exception)
        {
            // Best-effort cleanup on failure.
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
            catch { /* ignore */ }
            return false;
        }
    }
}
