using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// EsteraOne ERP Sync popup shown BEFORE the Quality Control menu.
///
/// Two ways to bind the QC session to an ERP Kanban unit:
///   Kolom 1 — via No PO: enter a PO number, then pick one of the units (EST codes)
///             that belong to that PO.
///   Kolom 2 — via Kode Unit: search directly by internalId / pra-Qc code / serial.
///
/// The selected unit is returned to the caller so the QC results can be written
/// back to the ERP Kanban Checklist QC for that exact unit.
/// </summary>
public partial class QcErpSyncWindow : Window
{
    private readonly IErpSyncService _erpSync;
    private readonly ObservableCollection<PoUnitItem> _poUnits = new();
    private readonly ObservableCollection<TesterInfo> _testers = new();

    /// <summary>The selected ERP unit (null when the user skipped ERP).</summary>
    public PoUnitItem? SelectedUnit { get; private set; }

    /// <summary>PO number the unit was found under (empty when looked up by code).</summary>
    public string SelectedPoNumber { get; private set; } = string.Empty;

    /// <summary>The tester chosen/entered in the gate popup.</summary>
    public TesterInfo? SelectedTester { get; private set; }

    /// <summary>Free-text tester name (fallback when not registered).</summary>
    public string TesterName { get; private set; } = string.Empty;

    /// <summary>False when the user chose "Lanjut Tanpa ERP".</summary>
    public bool ErpConnected { get; private set; }

    public QcErpSyncWindow(IErpSyncService erpSync)
    {
        _erpSync = erpSync;
        InitializeComponent();
        PoUnitCombo.ItemsSource = _poUnits;
        PoUnitCombo.DisplayMemberPath = "DisplayText";
        TesterCombo.ItemsSource = _testers;
        TesterCombo.DisplayMemberPath = "Name";
        Services.Sfx.Action();
        _ = LoadTestersAsync();
    }

    private async Task LoadTestersAsync()
    {
        try
        {
            var result = await _erpSync.GetTestersAsync();
            if (result is { Success: true } && result.Data.Count > 0)
            {
                _testers.Clear();
                foreach (var t in result.Data)
                {
                    _testers.Add(t);
                }
            }
        }
        catch
        {
            // Testers are optional; ignore load failures.
        }
    }

    private async void AddTester_Click(object sender, RoutedEventArgs e)
    {
        var name = (NewTesterBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetFooter("Ketik nama tester baru terlebih dahulu.", "#F0A020");
            return;
        }

        AddTesterButton.IsEnabled = false;
        try
        {
            var result = await _erpSync.RegisterTesterAsync(name);
            if (result is { Success: true })
            {
                var tester = result.SingleTester ?? result.Data.FirstOrDefault();
                if (tester is not null)
                {
                    if (!_testers.Any(t => t.Id == tester.Id))
                    {
                        _testers.Add(tester);
                    }
                    TesterCombo.SelectedItem = tester;
                }
                NewTesterBox.Text = string.Empty;
                SetFooter($"Tester '{name}' siap.", "#39C86B");
            }
            else
            {
                SetFooter(result?.Message ?? "Gagal mendaftarkan tester.", "#FF6B6B");
            }
        }
        catch (Exception ex)
        {
            SetFooter($"Gagal daftar tester: {ex.Message}", "#FF6B6B");
        }
        finally
        {
            AddTesterButton.IsEnabled = true;
        }
    }

    private void CaptureTester()
    {
        if (TesterCombo.SelectedItem is TesterInfo t)
        {
            SelectedTester = t;
            TesterName = t.Name;
        }
        else
        {
            var typed = (NewTesterBox.Text ?? string.Empty).Trim();
            if (typed.Length > 0)
            {
                SelectedTester = new TesterInfo { Id = typed, Name = typed };
                TesterName = typed;
            }
        }
    }

    private void SetFooter(string text, string color = "#8C97AB")
    {
        FooterStatusText.Text = text;
        FooterStatusText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
    }

    // ===== Kolom 1: by No PO =====

    private async void PoSearch_Click(object sender, RoutedEventArgs e)
    {
        var po = (PoBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(po))
        {
            SetFooter("Masukkan No PO terlebih dahulu.", "#F0A020");
            return;
        }

        PoSearchButton.IsEnabled = false;
        PoUnitCombo.IsEnabled = false;
        SetFooter($"Mencari unit pada PO '{po}' ...", "#4CA7FF");

        try
        {
            var result = await _erpSync.LookupPoUnitsAsync(po);
            _poUnits.Clear();

            if (result is { Success: true, Data: not null } && result.Data.Units.Count > 0)
            {
                foreach (var unit in result.Data.Units)
                {
                    _poUnits.Add(unit);
                }

                PoStatusText.Text = $"{result.Data.Units.Count} unit ditemukan (prioritas brand: {result.Data.PreferredBrand}). " +
                                    $"Pembayaran: {result.Data.PaymentStatus}.";
                SetFooter($"PO '{po}' siap. Pilih kode unit lalu klik Hubungkan.", "#39C86B");
            }
            else
            {
                PoStatusText.Text = result?.Message ?? $"PO '{po}' tidak memiliki unit yang siap QC.";
                SetFooter(PoStatusText.Text, "#F0A020");
            }
        }
        catch (Exception ex)
        {
            PoStatusText.Text = $"Gagal mencari PO: {ex.Message}";
            SetFooter(PoStatusText.Text, "#FF6B6B");
        }
        finally
        {
            PoSearchButton.IsEnabled = true;
            PoUnitCombo.IsEnabled = true;
        }
    }

    private void ConnectByPo_Click(object sender, RoutedEventArgs e)
    {
        if (PoUnitCombo.SelectedItem is not PoUnitItem unit)
        {
            SetFooter("Pilih salah satu kode unit EST terlebih dahulu.", "#F0A020");
            return;
        }

        SelectedUnit = unit;
        SelectedPoNumber = (PoBox.Text ?? string.Empty).Trim();
        CaptureTester();
        ErpConnected = true;
        Services.Sfx.Launch();
        DialogResult = true;
        Close();
    }

    // ===== Kolom 2: by Kode Unit =====

    private async void UnitSearch_Click(object sender, RoutedEventArgs e)
    {
        var code = (UnitCodeBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetFooter("Masukkan Kode Unit terlebih dahulu.", "#F0A020");
            return;
        }

        UnitSearchButton.IsEnabled = false;
        UnitConnectButton.IsEnabled = false;
        UnitDetailText.Text = "Mencari...";
        SetFooter($"Mencari kode unit '{code}' di kanban ERP ...", "#4CA7FF");

        try
        {
            var result = await _erpSync.LookupUnitAsync(code);
            if (result is { Success: true, Data: not null })
            {
                var d = result.Data;
                SelectedUnit = new PoUnitItem
                {
                    Id = d.Id,
                    PraQcCode = d.PraQcCode,
                    InternalId = string.IsNullOrWhiteSpace(d.InternalId) ? code : d.InternalId,
                    SerialNumber = d.SerialNumber,
                    Status = d.Status,
                    ProductName = d.ProductName,
                    Brand = d.Brand
                };
                SelectedPoNumber = d.PoNumber;

                UnitDetailText.Text =
                    $"Kode Unit   : {SelectedUnit.InternalId}\n" +
                    $"Pra-QC Code : {d.PraQcCode}\n" +
                    $"Serial      : {d.SerialNumber}\n" +
                    $"Produk      : {d.ProductName}\n" +
                    $"Brand       : {d.Brand}\n" +
                    $"Model       : {d.Model}\n" +
                    $"No PO       : {d.PoNumber}\n" +
                    $"Status      : {d.Status}";

                UnitStatusText.Text = "Unit ditemukan & terhubung. Klik Hubungkan Kode Unit.";
                UnitConnectButton.IsEnabled = true;
                SetFooter($"Unit '{SelectedUnit.InternalId}' siap dihubungkan.", "#39C86B");
            }
            else
            {
                SelectedUnit = null;
                UnitConnectButton.IsEnabled = false;
                UnitDetailText.Text = "Unit tidak ditemukan.";
                UnitStatusText.Text = result?.Message ?? $"Kode '{code}' tidak ada di database ERP.";
                SetFooter(UnitStatusText.Text, "#F0A020");
            }
        }
        catch (Exception ex)
        {
            SelectedUnit = null;
            UnitConnectButton.IsEnabled = false;
            UnitDetailText.Text = $"Error: {ex.Message}";
            SetFooter(UnitDetailText.Text, "#FF6B6B");
        }
        finally
        {
            UnitSearchButton.IsEnabled = true;
        }
    }

    private void ConnectByUnit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedUnit is null)
        {
            SetFooter("Cari dan temukan kode unit terlebih dahulu.", "#F0A020");
            return;
        }

        CaptureTester();
        ErpConnected = true;
        Services.Sfx.Launch();
        DialogResult = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        SelectedUnit = null;
        CaptureTester();
        ErpConnected = false;
        Services.Sfx.Close();
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Shows the sync popup and returns the chosen unit + tester (null when skipped).
    /// </summary>
    public static (PoUnitItem? unit, string poNumber, bool connected, TesterInfo? tester, string testerName)
        Show(IErpSyncService erpSync, Window? owner = null)
    {
        var window = new QcErpSyncWindow(erpSync);
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }
        var ok = window.ShowDialog();
        return (window.SelectedUnit, window.SelectedPoNumber, ok == true && window.ErpConnected,
            window.SelectedTester, window.TesterName);
    }
}
