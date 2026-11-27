using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RyotaQC.App.Services;

public interface IErpSyncService
{
    Task<ErpSyncResponse> SubmitQcAsync(ErpQcPayload payload);
    Task<ErpUnitInfoResponse?> LookupUnitAsync(string code);
    Task<PoUnitsResponse?> LookupPoUnitsAsync(string poNumber, string preferredBrand = "Lenovo");
    Task<TestersResponse?> GetTestersAsync();
    Task<TestersResponse?> RegisterTesterAsync(string name);
    Task<TestersResponse?> DeleteTesterAsync(string id);
    // QC-LOG (20261106): riwayat hasil QC sebuah unit (maks 7 terbaru).
    Task<QcHistoryResponse?> GetQcHistoryAsync(string code);
}

public sealed class ErpSyncService : IErpSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IAppLogger _logger;
    private readonly string _hubBaseUrl;

    public ErpSyncService(IAppLogger logger, string? hubBaseUrl = null)
    {
        _logger = logger;
        // Default to Contabo Ryota QC Hub Pod (:8005)
        _hubBaseUrl = string.IsNullOrWhiteSpace(hubBaseUrl) ? "http://109.123.239.163:8005" : hubBaseUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>
    /// Mengekstrak kode unit/pra-QC/SN bersih dari input scan QR (termasuk link web /scan/unit/...).
    /// </summary>
    public static string ExtractScanCode(string? raw)
    {
        var cleaned = (raw ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        if (string.IsNullOrEmpty(cleaned))
        {
            return string.Empty;
        }

        Uri? uri = null;
        if (cleaned.Contains("://", StringComparison.Ordinal))
        {
            Uri.TryCreate(cleaned, UriKind.Absolute, out uri);
        }
        else if (cleaned.StartsWith("/scan/", StringComparison.OrdinalIgnoreCase) ||
                 cleaned.StartsWith("scan/", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = cleaned.StartsWith('/') ? cleaned : "/" + cleaned;
            Uri.TryCreate("https://esteraone.local" + normalized, UriKind.Absolute, out uri);
        }
        else if (cleaned.Contains("/scan/", StringComparison.OrdinalIgnoreCase))
        {
            Uri.TryCreate("https://" + cleaned, UriKind.Absolute, out uri);
        }

        if (uri is not null)
        {
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var queryMatch = System.Text.RegularExpressions.Regex.Match(
                    uri.Query,
                    @"(?:^|[?&])(?:code|unit|praQcCode|serialNumber)=([^&]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (queryMatch.Success && !string.IsNullOrWhiteSpace(queryMatch.Groups[1].Value))
                {
                    return Uri.UnescapeDataString(queryMatch.Groups[1].Value).Trim();
                }
            }

            var pathMatch = System.Text.RegularExpressions.Regex.Match(
                uri.AbsolutePath,
                @"/(?:scan/unit|scan)/([^/?#]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (pathMatch.Success && !string.IsNullOrWhiteSpace(pathMatch.Groups[1].Value))
            {
                return Uri.UnescapeDataString(pathMatch.Groups[1].Value).Trim();
            }

            var segments = uri.AbsolutePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0 && !string.IsNullOrWhiteSpace(segments[^1]))
            {
                return Uri.UnescapeDataString(segments[^1]).Trim();
            }
        }

        return cleaned;
    }

    public async Task<ErpSyncResponse> SubmitQcAsync(ErpQcPayload payload)
    {
        try
        {
            if (payload is not null)
            {
                payload.PraQcCode = ExtractScanCode(payload.PraQcCode);
                payload.SerialNumber = ExtractScanCode(payload.SerialNumber);
            }

            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/submit";
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<ErpSyncResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new ErpSyncResponse { Success = false, Message = "Respon kosong dari server hub" };
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal mengirim payload QC ke EsteraOne ERP Hub", ex);
            return new ErpSyncResponse { Success = false, Message = $"Koneksi error: {ex.Message}" };
        }
    }

    public async Task<ErpUnitInfoResponse?> LookupUnitAsync(string code)
    {
        try
        {
            var normalizedCode = ExtractScanCode(code);
            if (string.IsNullOrEmpty(normalizedCode)) return null;

            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/unit-info/{Uri.EscapeDataString(normalizedCode)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ErpUnitInfoResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Gagal lookup data unit untuk kode: {code}", ex);
            return null;
        }
    }

    public async Task<PoUnitsResponse?> LookupPoUnitsAsync(string poNumber, string preferredBrand = "Lenovo")
    {
        try
        {
            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/po/{Uri.EscapeDataString(poNumber.Trim())}/units?brand={Uri.EscapeDataString(preferredBrand)}";
            var response = await _httpClient.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PoUnitsResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Gagal lookup PO units untuk PO: {poNumber}", ex);
            return null;
        }
    }

    public async Task<TestersResponse?> GetTestersAsync()
    {
        try
        {
            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/testers";
            var response = await _httpClient.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TestersResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal mengambil list tester", ex);
            return null;
        }
    }

    public async Task<TestersResponse?> RegisterTesterAsync(string name)
    {
        try
        {
            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/testers";
            var json = JsonSerializer.Serialize(new { name }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TestersResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Gagal register tester: {name}", ex);
            return null;
        }
    }

    public async Task<TestersResponse?> DeleteTesterAsync(string id)
    {
        try
        {
            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/testers/{Uri.EscapeDataString(id)}";
            var response = await _httpClient.DeleteAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TestersResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Gagal menghapus tester: {id}", ex);
            return null;
        }
    }

    // QC-LOG (20261106): ambil riwayat hasil QC unit dari hub ERP.
    public async Task<QcHistoryResponse?> GetQcHistoryAsync(string code)
    {
        try
        {
            var normalizedCode = ExtractScanCode(code);
            if (string.IsNullOrEmpty(normalizedCode)) return null;

            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/history/{Uri.EscapeDataString(normalizedCode)}";
            var response = await _httpClient.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<QcHistoryResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Error($"Gagal mengambil log QC untuk unit: {code}", ex);
            return null;
        }
    }
}

public class ErpQcPayload
{
    public string PraQcCode { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>Unit completeness / accessories, mapped to qcData.completeness in ERP.</summary>
    public string Completeness { get; set; } = string.Empty;

    public ErpSpecs Specs { get; set; } = new();
    public int? BatteryHealth { get; set; }
    public int? BatteryHealth2 { get; set; }
    public int? StorageHealth { get; set; }
    public int? BatteryLifetimeMinutes { get; set; }
    public string Status { get; set; } = "PASS";
    public string QcStatus { get; set; } = "LULUS";
    public Dictionary<string, object> Tests { get; set; } = new();
    public Dictionary<string, object> NumericTests { get; set; } = new();
    public string TestedBy { get; set; } = "Teknisi QC";
    public string? TesterId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal RepairCost { get; set; }
    public string RejectReason { get; set; } = string.Empty;
}

public class ErpSpecs
{
    public string Processor { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string StorageHdd { get; set; } = string.Empty;
    public string StorageSsd { get; set; } = string.Empty;
    public string Vga { get; set; } = string.Empty;
    public string VgaIntegrated { get; set; } = string.Empty;
    public string VgaDedicated { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    // Back-compat single storage field
    public string Storage { get; set; } = string.Empty;
}

public class ErpSyncResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TargetDb { get; set; } = string.Empty;
    public int? DbPort { get; set; }
}

public class ErpUnitInfoResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ErpUnitData? Data { get; set; }
}

public class ErpUnitData
{
    public string Id { get; set; } = string.Empty;
    public string PraQcCode { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string DestinationOutletName { get; set; } = string.Empty;

    public string CleanProductName
    {
        get
        {
            var p = (ProductName ?? string.Empty).Trim();
            var b = (Brand ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(b) && p.StartsWith(b + " - ", StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(b.Length + 3).Trim();
            }
            return string.IsNullOrEmpty(p) ? $"{b} Laptop" : p;
        }
    }
}

// ===== PO Units Lookup response types =====
public class PoUnitsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PoUnitsData? Data { get; set; }
}

public class PoUnitsData
{
    public string PoNumber { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string ReceiptStatus { get; set; } = string.Empty;
    public List<PoUnitItem> Units { get; set; } = new();
    public string PreferredBrand { get; set; } = string.Empty;
}

public class PoUnitItem
{
    public string Id { get; set; } = string.Empty;
    public string PraQcCode { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string DestinationOutletName { get; set; } = string.Empty;

    public string CleanProductName
    {
        get
        {
            var p = (ProductName ?? string.Empty).Trim();
            var b = (Brand ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(b) && p.StartsWith(b + " - ", StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(b.Length + 3).Trim();
            }
            return string.IsNullOrEmpty(p) ? $"{b} Laptop" : p;
        }
    }

    /// <summary>Label posisi unit di kanban QC (Antrean / Tes Running / Selesai / Service / Retur).</summary>
    public string StatusLabel => (Status ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "QC_QUEUE" or "QC_ANTREAN" or "ANTREAN" or "QUEUE" => "QC antrean",
        "QC_PROCESS" or "RUNNING_TEST" or "RUNNING" or "TES_RUNNING" => "Tes running",
        "PASS" or "SELESAI" => "Selesai",
        "SERVICE" => "Service",
        "RETUR" or "RETURN" => "Retur",
        "" => "-",
        var other => other
    };

    /// <summary>
    /// Dropdown format: Kode Unit - Nama Item (Contoh: Dell Latitude 3380 i5-7 8/256-BGR) - Tujuan pengiriman Outlet ( Status di kanban )
    /// Contoh: PO-SUP-004-191126-002 - Lenovo Thinkpad T450 i5-5 8/256-BGR - Bestlaptop Bogor ( QC antrean )
    /// </summary>
    public string DisplayText
    {
        get
        {
            var dest = string.IsNullOrWhiteSpace(DestinationOutletName) ? string.Empty : $" - {DestinationOutletName.Trim()}";
            return $"{InternalId} - {CleanProductName}{dest} ( {StatusLabel} )";
        }
    }
}

// ===== Testers response types =====
public class TestersResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<TesterInfo> Data { get; set; } = new();
    public TesterInfo? SingleTester { get; set; }
}

public class TesterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

// ===== QC-LOG (20261106): riwayat hasil QC =====
public class QcHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<QcHistoryEntry> Data { get; set; } = new();
    public QcUnitDetails? UnitInfo { get; set; }
    public string? InternalId { get; set; }
    public string? SerialNumber { get; set; }
}

public class QcUnitDetails
{
    public string Id { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DestinationOutletName { get; set; } = string.Empty;
    public QcHardwareSpecs? Specs { get; set; }
    public System.Text.Json.JsonElement? Tests { get; set; }
    public System.Text.Json.JsonElement? NumericTests { get; set; }
    public double? BatteryHealth { get; set; }
    public double? BatteryHealth2 { get; set; }
    public double? StorageHealth { get; set; }
    public double? BatteryLifetimeMinutes { get; set; }
    public string? RemainingLifetime { get; set; }
    public string? LifetimeWrites { get; set; }
    public string? Notes { get; set; }
}

public class QcHardwareSpecs
{
    public string Processor { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Ssd { get; set; } = string.Empty;
    public string Hdd { get; set; } = string.Empty;
    public string Vga { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
}

public class QcHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string? TesterName { get; set; }
    public string? QcStatusName { get; set; }
    public string? OverallStatus { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public System.Text.Json.JsonElement? Snapshot { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>Label status ringkas untuk daftar log di aplikasi.</summary>
    public string StatusLabel
    {
        get
        {
            var raw = (QcStatusName ?? OverallStatus ?? "-").Trim();
            var up = raw.ToUpperInvariant();
            if (up.Contains("SELESAI") || up.Contains("READY")) return "Selesai";
            if (up.Contains("RUNNING") || up.Contains("PROSES")) return "Tes Running";
            if (up.Contains("RETUR")) return "Retur";
            if (up.Contains("SERVIS") || up.Contains("SERVICE")) return "Service";
            if (up.Contains("ANTREAN") || up.Contains("QUEUE")) return "Antrean";
            if (up == "PASS") return "PASS";
            if (up == "FAIL") return "FAIL";
            if (up == "ISSUE") return "ISSUE";
            return string.IsNullOrWhiteSpace(raw) ? "-" : raw;
        }
    }

    /// <summary>Waktu log diformat lokal (enak dibaca).</summary>
    public string TimeLabel =>
        CreatedAt.HasValue ? CreatedAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm") : "-";

    public string SourceLabel =>
        string.Equals(Source, "APP", StringComparison.OrdinalIgnoreCase) ? "App RyotaQC" : "Web Kanban";

    public string TesterLabel =>
        string.IsNullOrWhiteSpace(TesterName) ? "(tanpa tester)" : TesterName!;
}
