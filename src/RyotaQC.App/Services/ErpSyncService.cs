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

    public async Task<ErpSyncResponse> SubmitQcAsync(ErpQcPayload payload)
    {
        try
        {
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
            var url = $"{_hubBaseUrl.TrimEnd('/')}/api/v1/qc/unit-info/{Uri.EscapeDataString(code.Trim())}";
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

    /// <summary>Label posisi unit di kanban QC (Antrean / Tes Running / Selesai / Service / Retur).</summary>
    public string StatusLabel => (Status ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "QC_QUEUE" or "QC_ANTREAN" or "ANTREAN" or "QUEUE" => "QC Antrean",
        "QC_PROCESS" or "RUNNING_TEST" or "RUNNING" or "TES_RUNNING" => "Tes Running",
        "PASS" or "SELESAI" => "Selesai",
        "SERVICE" => "Service",
        "RETUR" or "RETURN" => "Retur",
        "" => "-",
        var other => other
    };

    /// <summary>Label used by ComboBox display, e.g. "EST-2610-0003-01 - Lenovo - ThinkPad X395 (QC Antrean)".</summary>
    public string DisplayText =>
        $"{InternalId} - {Brand} - {ProductName} ({StatusLabel})";
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
