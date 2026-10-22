using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
/// Checks GitHub releases for a newer version and manages the two-phase update
/// flow introduced in v5.0.8:
///
///   1. DOWNLOAD  - the installer EXE is downloaded, verified (size, PE header,
///                  SHA-256) and stored in %LOCALAPPDATA%\RyotaQC\updates\.
///                  Nothing is installed yet; "install now" and "later" are
///                  both valid answers.
///   2. INSTALL   - an elevated helper waits for this process to exit, runs the
///                  installer silently, reads the FileVersion of the freshly
///                  installed RyotaQC.App.exe, writes the result to
///                  install-result.json and relaunches the application.
///
/// The helper's exit code + on-disk version check is what makes a failed update
/// impossible to miss: the old flow trusted that "installer was launched" meant
/// "installer installed", which is how users ended up on the previous version
/// with no error at all.
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

    private const string ReadyUpdateFileName = "ready-update.json";
    private const string InstallResultFileName = "install-result.json";
    private const string InstallerLogFileName = "installer.log";
    private const string HelperFileName = "apply-update.ps1";

    // Shared client for the small release-metadata call (fast, 20s is plenty).
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    // Dedicated client for downloading release assets. Installer packages are
    // hundreds of MB, so a short timeout would abort the transfer mid-stream.
    private static readonly HttpClient DownloadHttp = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    private static readonly object LogSync = new();

    public Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    // =====================================================================
    // Phase 0 - check for a newer release
    // =====================================================================

    public async Task<AppUpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            // Housekeeping before touching the network: report the outcome of a
            // previous update attempt and remove leftover temp folders.
            CheckLegacyPendingMarker();
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

    // =====================================================================
    // Phase 1 - download + verify (no install)
    // =====================================================================

    public async Task<(bool Success, string? Error)> DownloadUpdateAsync(
        AppUpdateInfo update, IProgress<int>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            return (false, "Rilis ini tidak memiliki paket installer yang bisa diunduh.");
        }

        var isInstaller = update.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(update.DownloadUrl).Contains("Setup", StringComparison.OrdinalIgnoreCase);

        if (!isInstaller)
        {
            return (false,
                "Rilis ini hanya menyediakan paket ZIP, bukan installer EXE. " +
                "Unduh installer manual dari halaman rilis.");
        }

        CleanupStaleTempFolders();

        var versionTag = $"v{update.Version.Major}.{update.Version.Minor}.{update.Version.Build}";
        var versionFolder = Path.Combine(UpdatesFolder, versionTag);
        Directory.CreateDirectory(versionFolder);

        var downloadPath = Path.Combine(versionFolder, "RyotaQC-Setup.exe");
        var partialPath = downloadPath + ".part";

        Log($"download start: current={GetCurrentVersion()} target={versionTag} " +
            $"asset={update.DownloadFileName} dest={downloadPath}");

        try
        {
            SafeDelete(partialPath);
            SafeDelete(downloadPath);

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
            await using (var fileStream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
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
                SafeDelete(partialPath);
                return (false,
                    $"Unduhan terputus: hanya {gotMb:F1} MB dari {wantMb:F1} MB. Coba lagi.");
            }

            if (receivedBytes == 0)
            {
                SafeDelete(partialPath);
                return (false, "Unduhan kosong (0 byte). Periksa koneksi internet.");
            }

            // A truncated installer is the classic cause of Windows' "This app
            // can't run on your PC" dialog: the PE header of a cut-off file is
            // still readable, but the image cannot be loaded.
            if (!LooksLikeWindowsExecutable(partialPath))
            {
                Log($"downloaded payload is not a valid PE image ({receivedBytes} bytes)");
                SafeDelete(partialPath);
                return (false,
                    "File installer hasil unduhan rusak (bukan aplikasi Windows yang valid). " +
                    "Unduhan kemungkinan terpotong - coba lagi dengan koneksi yang stabil.");
            }

            // Verify the SHA-256 published in the release's SHA256SUMS.txt.
            var (checksumOk, checksumError, sha256) = await VerifyChecksumAsync(
                partialPath, update.DownloadFileName, update.ChecksumsUrl);
            if (!checksumOk)
            {
                Log($"checksum verification failed: {checksumError}");
                SafeDelete(partialPath);
                return (false, checksumError);
            }

            File.Move(partialPath, downloadPath, overwrite: true);

            var ready = new ReadyUpdate
            {
                Target = update.Version,
                InstallerPath = downloadPath,
                AssetName = update.DownloadFileName,
                Sha256 = sha256,
                DownloadedAt = DateTime.Now,
                ReleaseUrl = update.ReleaseUrl,
                ReleaseNotes = update.ReleaseNotes
            };
            WriteReadyUpdate(ready);

            Log($"download complete + verified: {receivedBytes} bytes, sha256={sha256}, " +
                $"stored={downloadPath}");
            progress?.Report(100);
            return (true, null);
        }
        catch (Exception ex)
        {
            SafeDelete(partialPath);
            Log($"download failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // =====================================================================
    // Phase 2 - install (elevated helper; caller then shuts down)
    // =====================================================================

    public async Task<(bool Success, string? Error)> StartInstallAsync(ReadyUpdate ready)
    {
        try
        {
            if (ready is null || !File.Exists(ready.InstallerPath))
            {
                return (false, "File installer hasil unduhan tidak ditemukan. Silakan unduh ulang update.");
            }

            var appExe = Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppContext.BaseDirectory, "RyotaQC.App.exe");

            var helperPath = Path.Combine(UpdatesFolder, HelperFileName);
            var script = BuildHelperScript(Environment.ProcessId, ready, appExe);
            await File.WriteAllTextAsync(helperPath, script, new UTF8Encoding(false));

            // Everything the elevated helper needs is passed through the script
            // itself, so there is no argument-quoting surface to get wrong.
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + helperPath + "\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = UpdatesFolder
            };

            Log($"launching elevated install helper: target={ready.FormattedVersion} " +
                $"installer={ready.InstallerPath} appExe={appExe} helper={helperPath}");
            Process.Start(psi);
            return (true, null);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Log("install helper launch refused (UAC declined or elevation blocked)");
            return (false,
                "Izin Administrator ditolak. Update tidak dipasang - setujui prompt UAC " +
                "agar update bisa diinstal.");
        }
        catch (Exception ex)
        {
            Log($"install helper launch failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Builds the elevated helper. It must NOT depend on the app being alive:
    /// it waits for this process to exit, runs the installer, verifies the
    /// installed version on disk, records the result, then relaunches the app
    /// as the interactive user.
    /// </summary>
    private static string BuildHelperScript(int parentPid, ReadyUpdate ready, string appExe)
    {
        static string Q(string? value) => "'" + (value ?? string.Empty).Replace("'", "''") + "'";

        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            "$parentPid = " + parentPid.ToString(CultureInfo.InvariantCulture),
            "$installer = " + Q(ready.InstallerPath),
            "$appExe    = " + Q(appExe),
            "$target    = " + Q(ready.Target.ToString()),
            "$result    = " + Q(InstallResultPath),
            "$instLog   = " + Q(InstallerLogPath),
            "$updateLog = " + Q(LogFilePath),
            "",
            "function Write-UpdateLog([string]$msg) {",
            "  try {",
            "    Add-Content -LiteralPath $updateLog -Value (\"[{0}] {1}\" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $msg)",
            "  } catch { }",
            "}",
            "",
            "Write-UpdateLog \"helper start: waiting for app pid $parentPid to exit\"",
            "for ($i = 0; $i -lt 600; $i++) {",
            "  if (-not (Get-Process -Id $parentPid -ErrorAction SilentlyContinue)) { break }",
            "  Start-Sleep -Milliseconds 200",
            "}",
            "Start-Sleep -Milliseconds 700",
            "",
            "$exitCode = -1",
            "try {",
            "  $installArgs = @('/SILENT', '/NORESTART', '/SUPPRESSMSGBOXES', ('/LOG=\"' + $instLog + '\"'))",
            "  $proc = Start-Process -FilePath $installer -ArgumentList $installArgs -PassThru -Wait",
            "  $exitCode = $proc.ExitCode",
            "} catch {",
            "  Write-UpdateLog ('installer launch failed: ' + $_.Exception.Message)",
            "}",
            "",
            "$installed = ''",
            "try { $installed = (Get-Item -LiteralPath $appExe).VersionInfo.FileVersion } catch { }",
            "Write-UpdateLog \"installer finished: exit=$exitCode installed=$installed target=$target\"",
            "",
            "$payload = [ordered]@{",
            "  target = $target",
            "  exitCode = $exitCode",
            "  installedVersion = $installed",
            "  installerLog = $instLog",
            "  finishedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')",
            "}",
            "try { ($payload | ConvertTo-Json -Compress) | Set-Content -LiteralPath $result -Encoding UTF8 } catch { }",
            "",
            "# Relaunch as the interactive, non-elevated user (this helper is elevated).",
            "try { Start-Process -FilePath 'explorer.exe' -ArgumentList ('\"' + $appExe + '\"') } catch {",
            "  try { Start-Process -FilePath $appExe } catch { }",
            "}",
            ""
        });
    }

    // =====================================================================
    // Pending-download + install-result persistence
    // =====================================================================

    public ReadyUpdate? GetReadyUpdate()
    {
        try
        {
            if (!File.Exists(ReadyUpdatePath))
            {
                return null;
            }

            var json = File.ReadAllText(ReadyUpdatePath);
            var dto = JsonSerializer.Deserialize<ReadyUpdateDto>(json);
            if (dto is null || !Version.TryParse(dto.Target, out var target))
            {
                DiscardReadyUpdate();
                return null;
            }

            // Already installed (or a newer version is running): the marker is
            // stale and the installer file is no longer needed.
            if (target <= GetCurrentVersion())
            {
                Log($"ready update {target} already satisfied - discarding");
                DiscardReadyUpdate();
                return null;
            }

            if (string.IsNullOrWhiteSpace(dto.InstallerPath) || !File.Exists(dto.InstallerPath))
            {
                Log("ready update marker points at a missing installer - discarding");
                DiscardReadyUpdate();
                return null;
            }

            return new ReadyUpdate
            {
                Target = target,
                InstallerPath = dto.InstallerPath!,
                AssetName = dto.AssetName ?? string.Empty,
                Sha256 = dto.Sha256 ?? string.Empty,
                DownloadedAt = DateTime.TryParse(dto.DownloadedAt, out var at) ? at : default,
                ReleaseUrl = dto.ReleaseUrl ?? string.Empty,
                ReleaseNotes = dto.ReleaseNotes ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            Log($"could not read ready-update marker: {ex.Message}");
            return null;
        }
    }

    public InstallOutcome? ConsumeInstallOutcome()
    {
        try
        {
            if (!File.Exists(InstallResultPath))
            {
                return null;
            }

            var json = File.ReadAllText(InstallResultPath);
            SafeDelete(InstallResultPath);

            var dto = JsonSerializer.Deserialize<InstallResultDto>(json);
            if (dto is null)
            {
                Log("install-result.json could not be parsed");
                return null;
            }

            Version.TryParse(dto.Target ?? string.Empty, out var target);
            var installedText = (dto.InstalledVersion ?? string.Empty).Trim();
            var installedOk = Version.TryParse(installedText, out var installedVersion);

            var success = dto.ExitCode == 0 && installedOk && target is not null && installedVersion >= target;

            Log($"install outcome: exit={dto.ExitCode} installed='{installedText}' " +
                $"target='{dto.Target}' success={success}");

            if (success)
            {
                DiscardReadyUpdate();
                SafeDelete(HelperPath);
            }

            return new InstallOutcome
            {
                Target = target ?? new Version(0, 0, 0),
                ExitCode = dto.ExitCode,
                InstalledVersion = installedText,
                Success = success,
                InstallerLogPath = dto.InstallerLog ?? string.Empty,
                FinishedAt = DateTime.TryParse(dto.FinishedAt, out var at) ? at : default
            };
        }
        catch (Exception ex)
        {
            Log($"could not read install result: {ex.Message}");
            return null;
        }
    }

    public void DiscardReadyUpdate()
    {
        try
        {
            var installerPath = string.Empty;
            if (File.Exists(ReadyUpdatePath))
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<ReadyUpdateDto>(File.ReadAllText(ReadyUpdatePath));
                    installerPath = dto?.InstallerPath ?? string.Empty;
                }
                catch
                {
                    // Marker is corrupt; deleting it is all that matters.
                }
                SafeDelete(ReadyUpdatePath);
            }

            if (!string.IsNullOrWhiteSpace(installerPath))
            {
                SafeDelete(installerPath);
                SafeDelete(installerPath + ".part");
                TryDeleteDirectory(Path.GetDirectoryName(installerPath) ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Log($"discard ready update failed: {ex.Message}");
        }
    }

    private static void WriteReadyUpdate(ReadyUpdate ready)
    {
        var dto = new ReadyUpdateDto
        {
            Target = ready.Target.ToString(),
            InstallerPath = ready.InstallerPath,
            AssetName = ready.AssetName,
            Sha256 = ready.Sha256,
            DownloadedAt = ready.DownloadedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReleaseUrl = ready.ReleaseUrl,
            ReleaseNotes = ready.ReleaseNotes
        };

        File.WriteAllText(ReadyUpdatePath,
            JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    // =====================================================================
    // Paths
    // =====================================================================

    /// <summary>
    /// Persistent folder for downloaded installers. Deliberately NOT %TEMP%:
    /// running an installer from the temp folder is blocked by Defender's
    /// "block executables from running" attack-surface-reduction rule, which
    /// makes the installer silently never start - one of the ways an update
    /// could "finish" without the app changing version.
    /// </summary>
    internal static string UpdatesFolder
    {
        get
        {
            var candidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RyotaQC", "updates"),
                Path.Combine(Path.GetTempPath(), "RyotaQC", "updates")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }
                catch
                {
                    // Try the next fallback folder.
                }
            }

            return Path.GetTempPath();
        }
    }

    private static string ReadyUpdatePath => Path.Combine(UpdatesFolder, ReadyUpdateFileName);
    private static string InstallResultPath => Path.Combine(UpdatesFolder, InstallResultFileName);
    private static string InstallerLogPath => Path.Combine(UpdatesFolder, InstallerLogFileName);
    private static string HelperPath => Path.Combine(UpdatesFolder, HelperFileName);

    private static string LogFilePath =>
        Path.Combine(FileAppLogger.ResolveLogFolder(), "update.log");

    private static string LegacyPendingMarkerPath =>
        Path.Combine(FileAppLogger.ResolveLogFolder(), "pending-update.json");

    // =====================================================================
    // Diagnostics + integrity helpers
    // =====================================================================

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
    /// Removes leftover "RyotaQC_Update_*" folders from the pre-v5.0.8 updater,
    /// which downloaded into %TEMP% on every attempt. Called on startup and
    /// just before a download.
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

    /// <summary>Reads and clears the marker written by the old (pre-v5.0.8) updater.</summary>
    private static void CheckLegacyPendingMarker()
    {
        try
        {
            if (!File.Exists(LegacyPendingMarkerPath))
            {
                return;
            }

            var json = File.ReadAllText(LegacyPendingMarkerPath, Encoding.UTF8);
            SafeDelete(LegacyPendingMarkerPath);

            using var doc = JsonDocument.Parse(json);
            var targetText = doc.RootElement.TryGetProperty("target", out var targetEl) ? targetEl.GetString() : null;
            Log($"legacy pending marker cleared (target={targetText}, running={GetCurrentVersionStatic()})");
        }
        catch (Exception ex)
        {
            Log($"legacy pending marker check failed: {ex.Message}");
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A running installer may still hold the file; leave it.
        }
    }

    private static bool TryDeleteDirectory(string directory)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            return true;
        }
        catch
        {
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
    /// must never block a legitimate update. The computed hash is always
    /// returned so it can be recorded in the ready-update marker.
    /// </summary>
    private static async Task<(bool Ok, string? Error, string Sha256)> VerifyChecksumAsync(
        string filePath, string assetName, string checksumsUrl)
    {
        var actual = await Task.Run(() => ComputeSha256(filePath));

        if (string.IsNullOrWhiteSpace(checksumsUrl) || string.IsNullOrWhiteSpace(assetName))
        {
            Log("checksum manifest unavailable - skipping SHA-256 comparison");
            return (true, null, actual);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, checksumsUrl);
            request.Headers.Add("User-Agent", "RyotaQC-V5-Online-Updater");
            using var response = await DownloadHttp.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log($"checksum manifest HTTP {(int)response.StatusCode} - skipping comparison");
                return (true, null, actual);
            }

            var manifest = await response.Content.ReadAsStringAsync();
            var expected = FindChecksumForAsset(manifest, assetName);
            if (string.IsNullOrEmpty(expected))
            {
                Log($"no checksum entry for {assetName} - skipping comparison");
                return (true, null, actual);
            }

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                return (false,
                    $"File update korup: SHA-256 tidak cocok (harusnya {expected[..12]}..., hasil {actual[..12]}...). " +
                    "Unduhan kemungkinan terpotong - coba lagi.", actual);
            }

            Log($"SHA-256 verified for {assetName}");
            return (true, null, actual);
        }
        catch (Exception ex)
        {
            Log($"checksum verification error (ignored): {ex.Message}");
            return (true, null, actual);
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

    private static Version GetCurrentVersionStatic() =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    // =====================================================================
    // JSON contracts (must match the helper script / marker files)
    // =====================================================================

    private sealed class ReadyUpdateDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("target")]
        public string? Target { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("installerPath")]
        public string? InstallerPath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("assetName")]
        public string? AssetName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("downloadedAt")]
        public string? DownloadedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("releaseUrl")]
        public string? ReleaseUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }
    }

    private sealed class InstallResultDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("target")]
        public string? Target { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("exitCode")]
        public int ExitCode { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("installedVersion")]
        public string? InstalledVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("installerLog")]
        public string? InstallerLog { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("finishedAt")]
        public string? FinishedAt { get; set; }
    }
}
