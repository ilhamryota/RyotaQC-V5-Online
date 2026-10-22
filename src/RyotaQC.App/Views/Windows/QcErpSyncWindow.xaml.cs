using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RyotaQC.App.Models;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// "Hubungkan ke ERP" popup, shown every time the Quality Control menu is
/// opened.
///
/// Two exclusive ways to bind the QC session to a Kanban unit:
///   - Via No PO       : enter a PO, then pick one of its unit codes.
///   - Via Kode Unit   : search a unit code directly (PO is filled in when the
///                       unit happens to belong to one).
///
/// Only ONE mode is shown at a time, and the unit picker is a compact combo box
/// (the old version used a 210px-tall list that swallowed the whole dialog).
///
/// "Tanpa ERP" keeps working exactly as before: QC is usable without a link.
/// </summary>
public partial class QcErpSyncWindow : Window
{
    private readonly IErpSyncService _erpSync;
    private readonly QcErpConnection _current;
    private readonly ObservableCollection<PoUnitItem> _poUnits = new();
    private readonly ObservableCollection<TesterInfo> _testers = new();

    private PoUnitItem? _selectedUnit;
    private string _selectedMode = "po";
    private bool _changingExisting;

    /// <summary>Result handed back to the caller.</summary>
    public QcErpConnection Result { get; private set; } = QcErpConnection.None;

    public QcErpSyncWindow(IErpSyncService erpSync, QcErpConnection? current = null)
    {
        _erpSync = erpSync;
        _current = current ?? QcErpConnection.None;

        InitializeComponent();

        // Keep the tall modal inside the usable desktop area on smaller
        // laptop screens; the inner ScrollViewer handles the overflow.
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 24);

        PoUnitCombo.ItemsSource = _poUnits;
        PoUnitCombo.DisplayMemberPath = "DisplayText";
        PoUnitCombo.SelectionChanged += PoUnitCombo_SelectionChanged;

        TesterCombo.ItemsSource = _testers;
        TesterCombo.DisplayMemberPath = "Name";
        TesterCombo.SelectionChanged += (_, _) => UpdateConnectButton();

        Services.Sfx.Action();
        _ = LoadTestersAsync();
        ShowCurrentConnection();
        UpdateConnectButton();
    }

    // =====================================================================
    // Initial state
    // =====================================================================

    private void ShowCurrentConnection()
    {
        _changingExisting = false;

        if (!_current.Connected || _current.Unit is null)
        {
            ConnectedPanel.Visibility = Visibility.Collapsed;
            ModeSelector.Visibility = Visibility.Visible;
            return;
        }

        ConnectedText.Text = _current.BadgeText.Replace("  ·  ", " · ");
        ConnectedPanel.Visibility = Visibility.Visible;

        // The unit is already linked: hide the picker until the user asks to
        // change it. That keeps "enter QC" a one-click step for repeat users.
        ModeSelector.Visibility = Visibility.Collapsed;
        PoPanel.Visibility = Visibility.Collapsed;
        UnitPanel.Visibility = Visibility.Collapsed;
        ConnectButton.Visibility = Visibility.Collapsed;
        FooterStatusText.Text = "Klik Lanjutkan untuk memakai unit ini, atau Ganti untuk memilih unit lain.";
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

                // Restore the previous tester when re-entering the menu. When
                // there is no previous tester, the user must pick one (tester
                // is mandatory before connecting to ERP).
                if (_current.Tester is not null)
                {
                    var match = _testers.FirstOrDefault(t => t.Id == _current.Tester.Id)
                                ?? _testers.FirstOrDefault(t => string.Equals(
                                    t.Name,
                                    _current.Tester.Name,
                                    StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        TesterCombo.SelectedItem = match;
                    }
                }

                UpdateConnectButton();
            }
        }
        catch
        {
            // Tester list is loaded from the hub; on failure the user can
            // still register a new tester or continue without ERP.
        }
    }

    // =====================================================================
    // Mode switching
    // =====================================================================

    private void ModePo_Checked(object sender, RoutedEventArgs e)
    {
        if (PoPanel is null || UnitPanel is null)
        {
            return;
        }

        _selectedMode = "po";
        PoPanel.Visibility = Visibility.Visible;
        UnitPanel.Visibility = Visibility.Collapsed;
        UpdateConnectButton();
    }

    private void ModeUnit_Checked(object sender, RoutedEventArgs e)
    {
        if (PoPanel is null || UnitPanel is null)
        {
            return;
        }

        _selectedMode = "unit";
        PoPanel.Visibility = Visibility.Collapsed;
        UnitPanel.Visibility = Visibility.Visible;
        UpdateConnectButton();
    }

    private void UpdateConnectButton(bool updateHint = true)
    {
        if (ConnectButton is null)
        {
            return;
        }

        ConnectButton.Content = "↗   Hubungkan ke ERP";

        // While an existing unit is linked, the picker is hidden and the card
        // shows Lanjutkan / Ganti. Do not overwrite that hint with picker
        // instructions until the user actually chooses "Ganti".
        if (_current.Connected && !_changingExisting)
        {
            ConnectButton.IsEnabled = false;
            return;
        }

        var hasUnit = _selectedMode == "po"
            ? PoUnitCombo?.SelectedItem is PoUnitItem
            : _selectedUnit is not null;
        var hasTester = TesterCombo?.SelectedItem is TesterInfo;

        ConnectButton.IsEnabled = hasUnit && hasTester;

        if (!updateHint)
        {
            return;
        }

        if (!hasUnit)
        {
            FooterStatusText.Text = "Pilih kode unit terlebih dahulu.";
        }
        else if (!hasTester)
        {
            FooterStatusText.Text = "Pilih tester terlebih dahulu (wajib sebelum Hubungkan ke ERP).";
        }
        else
        {
            FooterStatusText.Text = "Unit dan tester siap. Klik Hubungkan ke ERP.";
        }
    }

    // =====================================================================
    // Via No PO
    // =====================================================================

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
        _poUnits.Clear();
        _selectedUnit = null;
        UpdateConnectButton();
        SetFooter($"Mencari unit pada PO '{po}' ...", "#4CA7FF");

        try
        {
            var result = await _erpSync.LookupPoUnitsAsync(po);

            if (result is { Success: true, Data: not null } && result.Data.Units.Count > 0)
            {
                foreach (var unit in result.Data.Units)
                {
                    _poUnits.Add(unit);
                }

                PoStatusText.Text =
                    $"{result.Data.Units.Count} unit ditemukan. Pembayaran: {result.Data.PaymentStatus}. " +
                    "Pilih kode unit di atas.";
                SetFooter($"PO '{po}' siap. Pilih kode unit lalu klik Hubungkan ke ERP.", "#39C86B");
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
            UpdateConnectButton(updateHint: false);
        }
    }

    private void PoUnitCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedUnit = PoUnitCombo.SelectedItem as PoUnitItem;
        UpdateConnectButton();

        if (_selectedUnit is not null && _selectedMode == "po")
        {
            PoStatusText.Text = $"Dipilih: {_selectedUnit.InternalId} · {_selectedUnit.Brand} · {_selectedUnit.Status}";
        }
    }

    // =====================================================================
    // Via Kode Unit
    // =====================================================================

    private async void UnitSearch_Click(object sender, RoutedEventArgs e)
    {
        var code = (UnitCodeBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetFooter("Masukkan Kode Unit terlebih dahulu.", "#F0A020");
            return;
        }

        UnitSearchButton.IsEnabled = false;
        _selectedUnit = null;
        UpdateConnectButton();
        UnitDetailText.Text = "Mencari...";
        SetFooter($"Mencari kode unit '{code}' di kanban ERP ...", "#4CA7FF");

        try
        {
            var result = await _erpSync.LookupUnitAsync(code);
            if (result is { Success: true, Data: not null })
            {
                var d = result.Data;
                _selectedUnit = new PoUnitItem
                {
                    Id = d.Id,
                    PraQcCode = d.PraQcCode,
                    InternalId = string.IsNullOrWhiteSpace(d.InternalId) ? code : d.InternalId,
                    SerialNumber = d.SerialNumber,
                    Status = d.Status,
                    ProductName = d.ProductName,
                    Brand = d.Brand
                };

                UnitDetailText.Text =
                    $"Kode Unit   : {_selectedUnit.InternalId}\n" +
                    $"Pra-QC Code : {d.PraQcCode}\n" +
                    $"Serial      : {d.SerialNumber}\n" +
                    $"Produk      : {d.ProductName}\n" +
                    $"Brand       : {d.Brand}\n" +
                    $"Model       : {d.Model}\n" +
                    $"No PO       : {d.PoNumber}\n" +
                    $"Status      : {d.Status}";

                UnitStatusText.Text = _selectedUnit.InternalId;
                SetFooter($"Unit '{_selectedUnit.InternalId}' ditemukan. Klik Hubungkan ke ERP.", "#39C86B");
            }
            else
            {
                UnitDetailText.Text = "Unit tidak ditemukan.";
                UnitStatusText.Text = result?.Message ?? $"Kode '{code}' tidak ada di database ERP.";
                SetFooter(UnitStatusText.Text, "#F0A020");
            }
        }
        catch (Exception ex)
        {
            UnitDetailText.Text = $"Error: {ex.Message}";
            SetFooter(UnitDetailText.Text, "#FF6B6B");
        }
        finally
        {
            UnitSearchButton.IsEnabled = true;
            UpdateConnectButton(updateHint: false);
        }
    }

    // =====================================================================
    // Connect / keep / skip
    // =====================================================================

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedUnit is null)
        {
            SetFooter("Pilih atau cari kode unit terlebih dahulu.", "#F0A020");
            return;
        }

        var tester = ResolveTester();
        if (tester is null)
        {
            SetFooter("Pilih tester terlebih dahulu. Tester wajib diisi sebelum Hubungkan ke ERP.", "#F0A020");
            TesterCombo.Focus();
            return;
        }

        var poNumber = _selectedMode == "po"
            ? (PoBox.Text ?? string.Empty).Trim()
            : string.Empty;

        Result = new QcErpConnection
        {
            Unit = _selectedUnit,
            PoNumber = poNumber,
            Connected = true,
            Tester = tester,
            TesterName = tester.Name,
            Mode = _selectedMode
        };

        Services.Sfx.Launch();
        DialogResult = true;
        Close();
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        var tester = ResolveTester() ?? _current.Tester;
        if (tester is null)
        {
            SetFooter("Pilih tester terlebih dahulu sebelum melanjutkan.", "#F0A020");
            TesterCombo.Focus();
            return;
        }

        Result = new QcErpConnection
        {
            Unit = _current.Unit,
            PoNumber = _current.PoNumber,
            Connected = true,
            Tester = tester,
            TesterName = tester.Name,
            Mode = _current.Mode
        };

        Services.Sfx.Launch();
        DialogResult = true;
        Close();
    }

    private void Change_Click(object sender, RoutedEventArgs e)
    {
        _changingExisting = true;
        ConnectedPanel.Visibility = Visibility.Collapsed;
        ModeSelector.Visibility = Visibility.Visible;
        PoPanel.Visibility = _selectedMode == "po" ? Visibility.Visible : Visibility.Collapsed;
        UnitPanel.Visibility = _selectedMode == "unit" ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.Visibility = Visibility.Visible;
        FooterStatusText.Text = "Pilih kode unit terlebih dahulu.";
        UpdateConnectButton();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Result = new QcErpConnection
        {
            Unit = null,
            PoNumber = string.Empty,
            Connected = false,
            Tester = ResolveTester(),
            TesterName = ResolveTesterName(),
            Mode = string.Empty
        };

        Services.Sfx.Close();
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Closing keeps an existing connection; with no connection it behaves
        // exactly like "Tanpa ERP" so QC stays reachable.
        if (_current.Connected)
        {
            var tester = ResolveTester() ?? _current.Tester;
            Result = tester is null
                ? _current
                : new QcErpConnection
                {
                    Unit = _current.Unit,
                    PoNumber = _current.PoNumber,
                    Connected = true,
                    Tester = tester,
                    TesterName = tester.Name,
                    Mode = _current.Mode
                };
        }
        else
        {
            Result = new QcErpConnection
            {
                Connected = false,
                Tester = ResolveTester(),
                TesterName = ResolveTesterName()
            };
        }

        DialogResult = Result.Connected;
        Close();
    }

    // =====================================================================
    // Testers
    // =====================================================================

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
                var tester = result.SingleTester
                             ?? result.Data.FirstOrDefault()
                             ?? new TesterInfo { Name = name };

                if (!_testers.Any(t => t.Id == tester.Id))
                {
                    _testers.Add(tester);
                }

                TesterCombo.SelectedItem = tester;
                NewTesterBox.Text = string.Empty;
                UpdateConnectButton();

                SetFooter($"Tester '{tester.Name}' terdaftar dan siap dipakai.", "#39C86B");

                QcMessagePopupWindow.Show(
                    "Tester Terdaftar",
                    $"Tester '{tester.Name}' berhasil didaftarkan dan tersimpan di database ERP.\n\n" +
                    "Tester ini sekarang muncul di pilihan dropdown Tester dan akan otomatis " +
                    "mengisi kolom Tester QC pada kanban ERP saat hasil dikirim.",
                    QcMessageTone.Success);
            }
            else
            {
                SetFooter(result?.Message ?? "Gagal mendaftarkan tester.", "#FF6B6B");
                QcMessagePopupWindow.Show(
                    "Gagal Daftar Tester",
                    result?.Message ?? "Tester baru gagal didaftarkan. Periksa koneksi ke hub ERP.",
                    QcMessageTone.Error);
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

    private TesterInfo? ResolveTester()
    {
        if (TesterCombo.SelectedItem is TesterInfo t)
        {
            return t;
        }

        // The previous session's tester is valid even before the asynchronous
        // tester list finishes loading (or when the hub is temporarily down).
        if (_current.Tester is not null)
        {
            return _current.Tester;
        }

        // Allow the user to type an existing tester name directly, but only
        // when it matches a registered tester. A name that has not been added
        // yet must go through "Add New Tester" first (so it exists in the ERP
        // database and can fill the Kanban Tester QC column).
        var typed = (NewTesterBox.Text ?? string.Empty).Trim();
        if (typed.Length == 0)
        {
            return null;
        }

        var match = _testers.FirstOrDefault(x =>
            string.Equals(x.Name, typed, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            TesterCombo.SelectedItem = match;
            return match;
        }

        return null;
    }

    private string ResolveTesterName() => ResolveTester()?.Name ?? string.Empty;

    private void SetFooter(string text, string color = "#8C97AB")
    {
        FooterStatusText.Text = text;
        FooterStatusText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
            // DragMove throws when the button is released before it starts.
        }
    }

    /// <summary>
    /// Shows the connect popup and returns the resulting connection.
    /// <paramref name="current"/> is the existing connection (if any) so the
    /// window can offer "Lanjutkan" instead of forcing a re-selection.
    /// </summary>
    public static QcErpConnection Show(IErpSyncService erpSync, Window? owner = null, QcErpConnection? current = null)
    {
        var window = new QcErpSyncWindow(erpSync, current);
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
        return window.Result;
    }
}
