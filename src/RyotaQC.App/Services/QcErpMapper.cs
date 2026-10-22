using System.Collections.Generic;

namespace RyotaQC.App.Services;

/// <summary>
/// Maps the RyotaQC desktop checklist results onto the exact <c>qcData</c>
/// contract expected by the EsteraOne ERP backend
/// (<c>bestlaptop-erp-be</c> / module <c>quality_control</c>).
///
/// ERP <c>qcData.tests</c> keys (must match <c>TEST_LIST</c> in the ERP frontend):
///   batteryTest, storageTest, lcdTest, wifiTest, keyboard, camera, backlight,
///   microphone, fingerprint, audioSpeaker, port, touchpad, touchscreen, stressTest
/// each value is <c>{ status: "PASS" | "FAIL" | "ISSUE" | "-", notes: string }</c>.
///
/// ERP <c>qcData.numericTests</c> keys:
///   hddSsdHealth (%), batteryHealth (%), batteryHealth2 (%), batteryLifetime (minutes),
///   physicalCondition (%)
///
/// Mapping of RyotaQC checklist kinds to ERP test keys:
///   LcdTest        -> lcdTest        (mandatory)
///   StorageHealth  -> storageTest    (mandatory, numeric -> hddSsdHealth)
///   BatteryHealth  -> batteryTest    (mandatory, numeric -> batteryHealth/​batteryHealth2)
///   BatteryTest    -> batteryTest    (numeric -> batteryLifetime, minutes)
///   KeyboardTest   -> keyboard       (mandatory)
///   AudioTest      -> audioSpeaker   (mandatory)
///   Benchmark      -> stressTest     (mandatory)
///   Camera         -> camera         (mandatory)
///   TouchscreenTest-> touchscreen    (optional)
///   PortTest       -> port           (mandatory)
///   Touchpad       -> touchpad       (mandatory)
///   Wifi           -> wifiTest       (mandatory)
///   Backlight      -> backlight      (optional)
///   Mic            -> microphone     (mandatory)
/// </summary>
public static class QcErpMapper
{
    /// <summary>A single ERP checklist test entry.</summary>
    public sealed class ErpTestValue
    {
        public string Status { get; set; } = "-";
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maps a RyotaQC result status to the ERP test status token.
    /// OK/PASS -> "PASS"; Skip/optional -> "-"; Fail -> "FAIL"; missing -> "-".
    /// </summary>
    public static string MapStatus(string? resultStatus, bool isMandatory)
    {
        var normalized = (resultStatus ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "PASS" => "PASS",
            "FAIL" => "FAIL",
            "ISSUE" => "ISSUE",
            "SKIP" => isMandatory ? "-" : "-",
            _ => "-"
        };
    }

    /// <summary>ERP test key for a given RyotaQC checklist kind name.</summary>
    public static string ErpTestKey(string kindName) => kindName switch
    {
        "LcdTest" => "lcdTest",
        "StorageHealth" => "storageTest",
        "BatteryHealth" => "batteryTest",
        // Battery Report only contributes the numeric lifetime (batteryLifetime);
        // it must not overwrite the batteryTest status produced by Battery Health.
        "BatteryTest" => "",
        "KeyboardTest" => "keyboard",
        "AudioTest" => "audioSpeaker",
        "Benchmark" => "stressTest",
        "RunningTest" => "",              // removed from QC results per requirement
        "Camera" => "camera",
        "TouchscreenTest" => "touchscreen",
        "PortTest" => "port",
        "Touchpad" => "touchpad",
        "Wifi" => "wifiTest",
        "Backlight" => "backlight",
        "Mic" => "microphone",
        _ => string.Empty
    };

    /// <summary>True when the ERP treats the test as optional (may be "-").</summary>
    public static bool IsOptional(string kindName) => kindName switch
    {
        "TouchscreenTest" => true,
        "Backlight" => true,
        _ => false
    };

    /// <summary>
    /// Builds the ERP <c>tests</c> dictionary from the app's checklist items.
    /// <paramref name="items"/> yields (KindName, HasResult, ResultStatus, ResultNote, InfoText).
    /// </summary>
    public static Dictionary<string, object> BuildTests(
        IEnumerable<(string KindName, bool HasResult, string ResultStatus, string ResultNote, string InfoText)> items)
    {
        var tests = new Dictionary<string, object>();

        foreach (var item in items)
        {
            var key = ErpTestKey(item.KindName);
            if (string.IsNullOrEmpty(key))
            {
                continue; // e.g. RunningTest is no longer sent.
            }

            var optional = IsOptional(item.KindName);
            var hasResult = item.HasResult;

            // Optional tests with no result (or "-") are left as "-" and skipped.
            if (optional && (!hasResult || string.IsNullOrWhiteSpace(item.ResultStatus)))
            {
                tests[key] = new ErpTestValue { Status = "-", Notes = string.Empty };
                continue;
            }

            var status = MapStatus(item.ResultStatus, isMandatory: !optional);
            var notes = status == "FAIL"
                ? (item.ResultNote ?? string.Empty)
                : (item.ResultNote ?? string.Empty);
            tests[key] = new ErpTestValue { Status = status, Notes = notes };
        }

        return tests;
    }

    /// <summary>
    /// Builds the ERP <c>numericTests</c> dictionary.
    /// Threshold: 70–100 → PASS, below 70 → FAIL. Values are always sent so the
    /// ERP Kanban reflects the exact submitted number.
    /// </summary>
    public static Dictionary<string, object> BuildNumericTests(
        int? storageHealth,
        int? batteryHealth1,
        int? batteryHealth2,
        int? batteryLifetimeMinutes,
        int? physicalCondition = null)
    {
        var numeric = new Dictionary<string, object>();

        if (storageHealth.HasValue)
        {
            numeric["hddSsdHealth"] = storageHealth.Value;
        }

        if (batteryHealth1.HasValue)
        {
            numeric["batteryHealth"] = batteryHealth1.Value;
        }

        if (batteryHealth2.HasValue && batteryHealth2.Value > 0)
        {
            numeric["batteryHealth2"] = batteryHealth2.Value;
        }

        if (batteryLifetimeMinutes.HasValue && batteryLifetimeMinutes.Value > 0)
        {
            numeric["batteryLifetime"] = batteryLifetimeMinutes.Value;
        }

        if (physicalCondition.HasValue)
        {
            numeric["physicalCondition"] = physicalCondition.Value;
        }

        return numeric;
    }

    /// <summary>
    /// Health verdict used by the popup flow: 70–100 → PASS, below 70 → FAIL.
    /// </summary>
    public static string HealthVerdict(int percent) => percent >= 70 ? "PASS" : "FAIL";
}
