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
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
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
}

public class ErpQcPayload
{
    public string PraQcCode { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Dictionary<string, string> Specs { get; set; } = new();
    public int? BatteryHealth { get; set; }
    public int? StorageHealth { get; set; }
    public string Status { get; set; } = "PASS";
    public Dictionary<string, object> Tests { get; set; } = new();
    public string TestedBy { get; set; } = "Teknisi QC";
    public string Notes { get; set; } = string.Empty;
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
    public ErpUnitData? Data { get; set; }
}

public class ErpUnitData
{
    public string Id { get; set; } = string.Empty;
    public string PraQcCode { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
}
