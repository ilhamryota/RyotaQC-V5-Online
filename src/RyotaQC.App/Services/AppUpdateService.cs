using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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

    // Name of the checksum manifest published next to the release assets.
    private const string ChecksumsAssetName = "SHA256SUMS.txt";

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
            // Housekeeping before touching the network: report the outcome of a
            // previous update attempt and remove leftover download folders.
            CheckPendingUpdateOutcome();
            CleanupStaleTempFolders();

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
            string downloadName = string.Empty;
            long downloadSize = 0;
            string checksumsUrl = string.Empty;

            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                string setupUrl = string.Empty;
                string setupName = string.Empty;
                long setupSize = 0;
                string zipUrl = string.Empty;
                string zipName = string.Empty;
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

                    // 0. Checksum manifest. Remember it but keep scanning - it
                    //    must not stop us from also finding the payload.
                    if (fileName.Equals(ChecksumsAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        checksumsUrl = url;
                        continue;
                    }

                    // 1. Prefer the official installer EXE (highest priority).
                    if (setupUrl.Length == 0
                        && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = url;
                        setupName = fileName;
                        setupSize = assetSize;
                        continue;
                    }

                    // 2. Remember the first ZIP as a fallback.
                    if (zipUrl.Length == 0
                        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zipUrl = url;
                        zipName = fileName;
                        zipSize = assetSize;
                    }
                }

                // Installer EXE wins; ZIP is the fallback for older releases.
                if (setupUrl.Length > 0)
                {
                    downloadUrl = setupUrl;
                    downloadName = setupName;
                    downloadSize = setupSize;
                }
                else if (zipUrl.Length > 0)
                {
                    downloadUrl = zipUrl;
                    downloadName = zipName;
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
                DownloadFileName = downloadName,
                ChecksumsUrl = checksumsUrl,
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

        CleanupStaleTempFolders();

        Log($"apply start: current={GetCurrentVersion()} target={update.FormattedVersion} " +
            $"asset={update.DownloadFileName} url={update.DownloadUrl}");

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
                        progress?.Report(Math.Min(pct, 100));
                    }
                }
            }

            // Guard against truncated downloads: if the server declared a size
            // and we received less, the transfer was cut off (network drop,
            // antivirus interference, or a proxy timeout).
            if (totalBytes > 0 && receivedBytes < totalBytes)
            {
                var gotMb = receivedBytes / (1024.0 * 1024.0);
                var wantMb = totalBytes / (1024.0 * 1024.0);
                return (false,
                    $"Unduhan terputus: hanya {gotMb:F1} MB dari {wantMb:F1} MB. Coba lagi.");
            }

            if (receivedBytes == 0)
            {
                return (false, "Unduhan kosong (0 byte). Periksa koneksi internet.");
            }

            // A truncated installer is the classic cause of Windows' "This app
            // can't run on your PC" dialog: the PE header of a cut-off file is
            // still readable, but the image cannot be loaded. Refuse to hand
            // such a file to the user.
            if (isInstaller && !LooksLikeWindowsExecutable(downloadPath))
            {
                Log($"downloaded payload is not a valid PE image ({receivedBytes} bytes)");
                TryDeleteDirectory(tempDir);
                return (false,
                    "File installer hasil unduhan rusak (bukan aplikasi Windows yang valid). " +
                    "Unduhan kemungkinan terpotong - coba lagi dengan koneksi yang stabil.");
            }

            // Verify the SHA-256 published in the release's SHA256SUMS.txt.
            var (checksumOk, checksumError) = await VerifyChecksumAsync(
                downloadPath, update.DownloadFileName, update.ChecksumsUrl);
            if (!checksumOk)
            {
                Log($"checksum verification failed: {checksumError}");
                TryDeleteDirectory(tempDir);
                return (false, checksumError);
            }

            Log($"download complete: {receivedBytes} bytes (checksum verified={checksumOk})");

            progress?.Report(100);

            if (isInstaller)
            {
                // Launch the official installer and exit.
                //
                // The app normally lives under "C:\Program Files\..." which
                // requires elevation to modify. ProcessStartInfo with
                // UseShellExecute + Verb="runas" triggers a UAC prompt so the
                // installer can replace files and relaunch the app afterwards.
                //
                // Flags:
                //   /SILENT    show progress, no wizard pages
                //   /NORESTART never force a reboot
                //
                // NOTE: we deliberately do NOT pass /CLOSEAPPLICATIONS or
                // /RESTARTAPPLICATIONS. Inno Setup's RestartApplications
                // relaunches every app it closed - racing the installer's
                // own [Run] entry and sometimes bringing back the OLD
                // executable (the app appeared to stay on the previous
                // version after updating). The app exits by itself right
                // after this call, and the installer relaunches the NEW
                // version via its [Run] entry.
                var installerPsi = new ProcessStartInfo
                {
                    FileName = downloadPath,
                    Arguments = "/SILENT /NORESTART",
                    UseShellExecute = true,
                    WorkingDirectory = tempDir,
                    Verb = "runas"
                };

                try
                {
                    WritePendingUpdateMarker(update);
                    Log($"launching installer: {downloadPath} /SILENT /NORESTART (elevated)");
                    Process.Start(installerPsi);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User declined the UAC prompt (or elevation is blocked).
                    Log("installer launch refused (UAC declined or elevation blocked)");
                    ClearPendingUpdateMarker();
                    return (false,
                        "Izin Administrator ditolak. Jalankan ulang sebagai Administrator " +
                        "atau klik Coba Lagi lalu setujui prompt UAC.");
                }

                return (true, null);
            }

            // ---- Legacy ZIP path: extract and swap files via a batch script. ----

            // Validate the archive before extracting. A truncated or corrupt ZIP
            // throws here instead of silently producing a partial install.
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(downloadPath);
                if (archive.Entries.Count == 0)
                {
                    return (false, "Paket update kosong atau rusak. Coba lagi.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Paket update tidak valid (ZIP rusak): {ex.Message}");
            }

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

            // Detect whether the target directory needs elevation to write.
            // Installs under Program Files always do; user-local installs do not.
            var needsElevation = !CanWriteToDirectory(appDir);

            // Build the updater batch script. It waits for the current app
            // process to exit, copies the new files over, restarts the app,
            // and cleans up the temp directory.
            //
            // When the install lives under Program Files the copy requires
            // Administrator rights, so the script re-launches itself elevated
            // via PowerShell Start-Process -Verb RunAs (one UAC prompt).
            var updaterPath = Path.Combine(tempDir, "apply_update.bat");

            var script =
                "@echo off" + Environment.NewLine +
                "chcp 65001 >nul" + Environment.NewLine +
                "title RyotaQC Updater" + Environment.NewLine +
                "setlocal EnableDelayedExpansion" + Environment.NewLine +
                "set \"SOURCE=" + sourceDir + "\"" + Environment.NewLine +
                "set \"TARGET=" + appDir + "\"" + Environment.NewLine +
                "set \"APP_EXE=" + appExe + "\"" + Environment.NewLine +
                "set \"PID=" + pid + "\"" + Environment.NewLine +
                "set \"TEMP_DIR=" + tempDir + "\"" + Environment.NewLine +
                Environment.NewLine;

            if (needsElevation)
            {
                // Re-launch this same script elevated. %~f0 is the full path of
                // the running batch file; the ELEVATED marker stops recursion.
                script +=
                    "if not \"%1\"==\"ELEVATED\" (" + Environment.NewLine +
                    "  echo Meminta izin Administrator..." + Environment.NewLine +
                    "  powershell -NoProfile -ExecutionPolicy Bypass -Command \"Start-Process -FilePath '%~f0' -ArgumentList 'ELEVATED' -Verb RunAs\"" + Environment.NewLine +
                    "  exit /b" + Environment.NewLine +
                    ")" + Environment.NewLine +
                    Environment.NewLine;
            }

            script +=
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
                "if errorlevel 1 (" + Environment.NewLine +
                "  echo GAGAL menyalin file. Coba jalankan installer manual." + Environment.NewLine +
                "  pause" + Environment.NewLine +
                "  exit /b 1" + Environment.NewLine +
                ")" + Environment.NewLine +
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

    // ---------------------------------------------------------------------
    // Diagnostics + integrity helpers.
    //
    // Every path below is best-effort: an update problem must never crash the
    // app, but it must always leave a trace. The previous "update finished but
    // the app still shows the old version" reports were undiagnosable because
    // nothing anywhere recorded what the updater actually did.
    // ---------------------------------------------------------------------

    private static readonly object LogSync = new();

    private static string? _pendingUpdateWarning;

    /// <summary>
    /// Returns (and clears) the warning describing a previous update attempt
    /// that did not actually change the installed version. The startup path
    /// shows it exactly once, so a silent "still the old version" can never
    /// happen unnoticed again.
    /// </summary>
    public static string? ConsumePendingUpdateWarning()
    {
        var warning = _pendingUpdateWarning;
        _pendingUpdateWarning = null;
        return warning;
    }

    private static string LogFilePath =>
        Path.Combine(FileAppLogger.ResolveLogFolder(), "update.log");

    private static string PendingMarkerPath =>
        Path.Combine(FileAppLogger.ResolveLogFolder(), "pending-update.json");

    private static void Log(string message)
    {
        try
        {
            lock (LogSync)
            {
                File.AppendAllText(LogFilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break the updater.
        }
    }

    /// <summary>
    /// Removes leftover "RyotaQC_Update_*" folders from earlier update runs.
    /// Each one holds a full copy of the installer (~330 MB) and they used to
    /// pile up with no cleanup. Called on startup and just before a download,
    /// so no folder of the current session can be removed by mistake.
    /// </summary>
    public static void CleanupStaleTempFolders()
    {
        try
        {
            var root = Path.GetTempPath();
            foreach (var dir in Directory.GetDirectories(root, "RyotaQC_Update_*"))
            {
                if (TryDeleteDirectory(dir))
                {
                    Log($"removed leftover update folder {Path.GetFileName(dir)}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"temp cleanup failed: {ex.Message}");
        }
    }

    private static bool TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            return true;
        }
        catch
        {
            // A running installer may still hold files; leave it for next time.
            return false;
        }
    }

    /// <summary>True when the file starts with the DOS "MZ" signature.</summary>
    private static bool LooksLikeWindowsExecutable(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < 2)
            {
                return false;
            }
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies the downloaded asset against its SHA-256 entry in the release's
    /// SHA256SUMS.txt asset. Succeeds when no manifest is published or the entry
    /// is missing: integrity checking is an extra guard, so a missing manifest
    /// must never block a legitimate update.
    /// </summary>
    private static async Task<(bool Ok, string? Error)> VerifyChecksumAsync(
        string filePath, string assetName, string checksumsUrl)
    {
        if (string.IsNullOrWhiteSpace(checksumsUrl) || string.IsNullOrWhiteSpace(assetName))
        {
            Log("checksum manifest unavailable - skipping SHA-256 verification");
            return (true, null);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, checksumsUrl);
            request.Headers.Add("User-Agent", "RyotaQC-V5-Online-Updater");
            using var response = await DownloadHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log($"checksum manifest HTTP {(int)response.StatusCode} - skipping verification");
                return (true, null);
            }

            var manifest = await response.Content.ReadAsStringAsync();
            var expected = FindChecksumForAsset(manifest, assetName);
            if (string.IsNullOrEmpty(expected))
            {
                Log($"no checksum entry for {assetName} - skipping verification");
                return (true, null);
            }

            var actual = await Task.Run(() => ComputeSha256(filePath));
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                return (false,
                    $"File update korup: SHA-256 tidak cocok (harusnya {expected[..12]}..., hasil {actual[..12]}...). " +
                    "Unduhan kemungkinan terpotong - coba lagi.");
            }

            Log($"SHA-256 verified for {assetName}");
            return (true, null);
        }
        catch (Exception ex)
        {
            Log($"checksum verification error (ignored): {ex.Message}");
            return (true, null);
        }
    }

    /// <summary>
    /// Finds the hash for <paramref name="assetName"/> in a "sha256sum" style
    /// manifest (<c>&lt;hash&gt;  &lt;filename&gt;</c>), tolerating the
    /// "*filename" binary marker and extra whitespace.
    /// </summary>
    private static string FindChecksumForAsset(string manifest, string assetName)
    {
        foreach (var rawLine in manifest.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOfAny(new[] { ' ', '\t' });
            if (separator <= 0)
            {
                continue;
            }

            var hash = line[..separator].Trim();
            var name = Path.GetFileName(line[separator..].Trim().TrimStart('*'));

            if (hash.Length == 64 && name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
            {
                return hash;
            }
        }

        return string.Empty;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    /// <summary>
    /// Records which version is about to be installed. If the app comes back up
    /// still on the old version, this file is the evidence that names the cause.
    /// </summary>
    private static void WritePendingUpdateMarker(AppUpdateInfo update)
    {
        try
        {
            var payload = string.Join(Environment.NewLine, new[]
            {
                "{",
                $"  \"target\": \"{update.Version}\",",
                $"  \"asset\": \"{update.DownloadFileName}\",",
                $"  \"from\": \"{GetCurrentVersionStatic()}\",",
                $"  \"at\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"",
                "}",
                string.Empty
            });

            File.WriteAllText(PendingMarkerPath, payload, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log($"could not write pending-update marker: {ex.Message}");
        }
    }

    private static void ClearPendingUpdateMarker()
    {
        try
        {
            if (File.Exists(PendingMarkerPath))
            {
                File.Delete(PendingMarkerPath);
            }
        }
        catch
        {
            // Ignore.
        }
    }

    private static Version GetCurrentVersionStatic() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Compares the version recorded before the last update attempt with the
    /// version actually running now. A mismatch means the installer ran but the
    /// payload inside it was not the expected application - the exact failure
    /// that shipped in v5.0.3/v5.0.4/v5.0.5.
    /// </summary>
    private static void CheckPendingUpdateOutcome()
    {
        try
        {
            if (!File.Exists(PendingMarkerPath))
            {
                return;
            }

            var json = File.ReadAllText(PendingMarkerPath, Encoding.UTF8);
            ClearPendingUpdateMarker();

            using var doc = JsonDocument.Parse(json);
            var targetText = doc.RootElement.TryGetProperty("target", out var targetEl) ? targetEl.GetString() : null;
            var assetName = doc.RootElement.TryGetProperty("asset", out var assetEl) ? assetEl.GetString() : null;
            var fromText = doc.RootElement.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;

            if (!Version.TryParse(targetText, out var target))
            {
                return;
            }

            var current = GetCurrentVersionStatic();
            if (current >= target)
            {
                Log($"previous update applied successfully: {fromText} -> {current} (asset {assetName})");
            }
            else
            {
                Log($"WARNING: previous update to {target} did NOT take effect - still running " +
                    $"{current} (asset {assetName}). The installer most likely contained an older payload.");

                // Also make it visible in the UI: a failed update must never be
                // silent, otherwise the main menu keeps showing the old version
                // and nobody knows why.
                _pendingUpdateWarning =
                    $"Update ke v{target} selesai dijalankan, tetapi aplikasi masih versi " +
                    $"v{current.Major}.{current.Minor}.{current.Build}." + Environment.NewLine + Environment.NewLine +
                    "Aplikasi akan mencoba update lagi saat dibuka. Kalau masalah ini berulang, " +
                    "kirim file log berikut ke pengembang:" + Environment.NewLine +
                    LogFilePath;
            }
        }
        catch (Exception ex)
        {
            Log($"pending-update outcome check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true when the current user can create files in
    /// <paramref name="directory"/>. Used to decide whether the updater needs
    /// an Administrator (UAC) prompt.
    /// </summary>
    private static bool CanWriteToDirectory(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, ".ryotaqc_write_probe_" + Guid.NewGuid().ToString("N"));
            using (var fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0);
            }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
