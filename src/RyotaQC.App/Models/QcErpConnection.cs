using RyotaQC.App.Services;

namespace RyotaQC.App.Models;

/// <summary>
/// Result of the "Hubungkan ke ERP" popup shown before the Quality Control
/// menu. Carries everything needed to display the connection badge and to bind
/// the QC result to the right Kanban unit.
/// </summary>
public sealed class QcErpConnection
{
    /// <summary>Selected ERP unit (null when running without ERP).</summary>
    public PoUnitItem? Unit { get; init; }

    /// <summary>PO number the unit was found under (may also be filled for unit-only lookups).</summary>
    public string PoNumber { get; init; } = string.Empty;

    public bool Connected { get; init; }

    public TesterInfo? Tester { get; init; }

    public string TesterName { get; init; } = string.Empty;

    /// <summary>
    /// How the unit was chosen: "po" (No PO + kode unit) or "unit" (kode unit
    /// only). Drives which codes the QC badge shows.
    /// </summary>
    public string Mode { get; init; } = string.Empty;

    public string UnitCode => Unit?.InternalId ?? string.Empty;

    public static QcErpConnection None => new();

    /// <summary>Compact badge text for the Quality Control screen.</summary>
    public string BadgeText
    {
        get
        {
            if (!Connected || Unit is null)
            {
                return "TANPA ERP";
            }

            return Mode == "po" && !string.IsNullOrWhiteSpace(PoNumber)
                ? $"PO {PoNumber}  ·  {UnitCode}"
                : $"KODE UNIT  {UnitCode}";
        }
    }
}
