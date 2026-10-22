
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RyotaQC.App.Enums;
using RyotaQC.App.Models;
using RyotaQC.App.Services;

namespace RyotaQC.App.ViewModels.Pages;

public sealed partial class QualityControlPageViewModel : ObservableObject
{
    private readonly IQualityControlRunner _runner;
    private readonly IUiFeedbackService _feedback;
    private readonly IAppLogger _logger;
    private readonly INavigationService _navigationService;
    private readonly IAudioFeedbackService _audio;
    private readonly IErpSyncService _erpSyncService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly string _workspaceRoot;
    private readonly string _qcRoot;
    private readonly string _hdsStatusPath;
    private readonly string _driverInstallStampPath;
    private readonly DispatcherTimer _clockTimer;
    private bool _isAutoUpdatingChecks;
    private DateTime? _qcStartDate;
    private DateTime? _driverInstallDate;
    private string _cachedSystemModel = string.Empty;
    private string _cachedSystemManufacturer = string.Empty;

    private int? _batteryHealthPercent;
    private int? _batteryHealth2Percent;
    private string _batteryHealthNote = string.Empty;
    private int? _storageHealthPercent;
    private int? _batteryLifetimeMinutes;
    private PortCheckResult? _lastPortResult;
    private bool _touchpadOk;
    private bool _wifiOk;
    private bool _backlightOk;
    private bool _micOk;

    [ObservableProperty]
    private string lastRefreshText = string.Empty;

    [ObservableProperty]
    private string checklistSummary = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool isSavingPdf;

    [ObservableProperty]
    private BenchmarkOption? selectedBenchmark;

    [ObservableProperty]
    private string benchmarkActionStatus = "Idle";

    [ObservableProperty]
    private string praQcCode = string.Empty;

    [ObservableProperty]
    private string erpUnitDetails = string.Empty;

    [ObservableProperty]
    private bool hasErpUnitDetails;

    [ObservableProperty]
    private string erpSyncStatus = string.Empty;

    [ObservableProperty]
    private bool isSyncingErp;

    [ObservableProperty]
    private System.Windows.Media.Brush erpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGray;

    // ===== Connection badge (shown at the top of the QC screen) =====
    [ObservableProperty]
    private string erpBadgeText = "TANPA ERP";

    /// <summary>"po" when connected via No PO, "unit" for unit-only, empty otherwise.</summary>
    [ObservableProperty]
    private string connectionMode = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.Brush erpBadgeBackgroundBrush = System.Windows.Media.Brushes.Transparent;

    [ObservableProperty]
    private System.Windows.Media.Brush erpBadgeBorderBrush = System.Windows.Media.Brushes.Transparent;

    [ObservableProperty]
    private System.Windows.Media.Brush erpBadgeForegroundBrush = System.Windows.Media.Brushes.LightGray;

    // ===== NEW: PO-based workflow =====
    [ObservableProperty]
    private string poNumber = string.Empty;

    [ObservableProperty]
    private bool hasPoUnits;

    [ObservableProperty]
    private PoUnitItem? selectedUnit;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string testerName = string.Empty;

    [ObservableProperty]
    private TesterInfo? selectedTester;

    [ObservableProperty]
    private string qcStatus = "LULUS";

    [ObservableProperty]
    private bool isLookingUpPo;

    [ObservableProperty]
    private bool isRegisteringTester;

    public ObservableCollection<PoUnitItem> PoUnits { get; } = new();

    public ObservableCollection<TesterInfo> Testers { get; } = new();

    public ObservableCollection<string> QcStatusOptions { get; } = new()
    {
        "LULUS",
        "SERVICE",
        "RETUR",
        "RUNNING_TEST"
    };

    public IAsyncRelayCommand SyncToErpCommand { get; }

    public IAsyncRelayCommand LookupUnitInfoCommand { get; }

    public IAsyncRelayCommand LookupPoUnitsCommand { get; }

    public IAsyncRelayCommand RegisterTesterCommand { get; }

    public IAsyncRelayCommand RefreshTestersCommand { get; }

    public IAsyncRelayCommand ConnectCommand { get; }

    /// <summary>
    /// Exposes the ERP sync service so the page can show the pre-menu
    /// EsteraOne ERP Sync popup (which lives in Views.Windows).
    /// </summary>
    public IErpSyncService ErpSyncService => _erpSyncService;

    /// <summary>
    /// Called after the user answers the "Hubungkan ke ERP" popup. Binds the QC
    /// session to the chosen unit (or runs without ERP) and refreshes the
    /// connection badge shown at the top of the Quality Control screen.
    /// </summary>
    public void ApplyErpConnection(QcErpConnection connection)
    {
        connection ??= QcErpConnection.None;

        SelectedTester = connection.Tester;
        if (SelectedTester is null && !string.IsNullOrWhiteSpace(connection.TesterName))
        {
            SelectedTester = new TesterInfo { Id = connection.TesterName, Name = connection.TesterName };
        }

        ConnectionMode = connection.Mode;
        ErpBadgeText = connection.BadgeText;
        IsConnected = connection.Connected && connection.Unit is not null;

        if (!IsConnected || connection.Unit is null)
        {
            // Chose "Tanpa ERP": QC still works, results stay local.
            SelectedUnit = null;
            PoNumber = string.Empty;
            PraQcCode = string.Empty;
            ErpUnitDetails = string.Empty;
            HasErpUnitDetails = false;
            ErpSyncStatus = SelectedTester is not null
                ? $"Tester: {SelectedTester.Name}. Belum terhubung ke unit ERP."
                : "Belum terhubung ke unit ERP.";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGray;
            ApplyBadgeStyle(connected: false);
            return;
        }

        PoNumber = connection.PoNumber ?? string.Empty;
        PraQcCode = connection.Unit.PraQcCode;
        SelectedUnit = connection.Unit;
        ErpUnitDetails = $"Unit: {connection.Unit.ProductName} ({connection.Unit.Brand})\n" +
                         $"Kode: {connection.Unit.InternalId} | Status: {connection.Unit.Status}";
        HasErpUnitDetails = true;
        ErpSyncStatus = $"Terhubung ke unit ERP {connection.Unit.InternalId}. Hasil QC akan otomatis terikat ke kanban unit ini.";
        ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGreen;
        ApplyBadgeStyle(connected: true);
    }

    /// <summary>
    /// Snapshot of the current binding, handed back to the popup so a repeat
    /// visit can offer "Lanjutkan" instead of forcing a new selection.
    /// </summary>
    public QcErpConnection CurrentErpConnection => new()
    {
        Unit = IsConnected ? SelectedUnit : null,
        PoNumber = PoNumber,
        Connected = IsConnected,
        Tester = SelectedTester,
        TesterName = SelectedTester?.Name ?? string.Empty,
        Mode = ConnectionMode
    };

    private void ApplyBadgeStyle(bool connected)
    {        if (connected)
        {
            ErpBadgeBackgroundBrush = Brush("#2226A35A");
            ErpBadgeBorderBrush = Brush("#8826A35A");
            ErpBadgeForegroundBrush = Brush("#7BE3A2");
        }
        else
        {
            ErpBadgeBackgroundBrush = Brush("#221F293B");
            ErpBadgeBorderBrush = Brush("#551E293B");
            ErpBadgeForegroundBrush = Brush("#98A6BC");
        }
    }

    private static System.Windows.Media.Brush Brush(string hex) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;

    partial void OnIsSyncingErpChanged(bool value) => SyncToErpCommand.NotifyCanExecuteChanged();

    partial void OnSelectedUnitChanged(PoUnitItem? value)
    {
        ConnectCommand.NotifyCanExecuteChanged();
        if (value != null)
        {
            PraQcCode = value.PraQcCode;
            ErpUnitDetails = $"Unit: {value.ProductName} ({value.Brand})\nKode: {value.InternalId} | Status: {value.Status}";
            HasErpUnitDetails = true;
        }
    }

    partial void OnIsConnectedChanged(bool value)
    {
        SyncToErpCommand.NotifyCanExecuteChanged();

        // Status text is authored by ApplyErpConnection (it names the unit).
        // Only the colour is derived here.
        ErpSyncStatusColorBrush = value
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.LightGray;
    }

    public QualityControlPageViewModel(
        IQualityControlRunner runner,
        IUiFeedbackService feedback,
        IAppLogger logger,
        INavigationService navigationService,
        IAudioFeedbackService audio,
        IErpSyncService erpSyncService,
        ISystemInfoService systemInfoService)
    {
        _runner = runner;
        _feedback = feedback;
        _logger = logger;
        _navigationService = navigationService;
        _audio = audio;
        _erpSyncService = erpSyncService;
        _systemInfoService = systemInfoService;

        _workspaceRoot = WorkspacePathService.FindWorkspaceRoot();
        _qcRoot = Path.Combine(_workspaceRoot, "tools", "qc");
        _hdsStatusPath = Path.Combine(_qcRoot, "Hard Disk Sentinel", "HDSentinel.sta");
        _driverInstallStampPath = Path.Combine(_workspaceRoot, "logs", "driver-pack-install.txt");
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateRealtimeClock();
        _clockTimer.Start();

        ChecklistItems = new ObservableCollection<QcChecklistItem>();
        DisplayChecklistItems = new ObservableCollection<QcChecklistItem>();
        BenchmarkOptions = new ObservableCollection<BenchmarkOption>();

        RefreshCommand = new RelayCommand(RefreshData);
        BackToHomeCommand = new RelayCommand(() =>
        {
            _audio.PlayExit();
            _navigationService.Navigate(AppPage.Dashboard);
        });
        SavePdfCommand = new AsyncRelayCommand(SavePdfAsync, () => !IsSavingPdf);
        InstallBenchmarkCommand = new AsyncRelayCommand(InstallBenchmarkAsync, () => SelectedBenchmark is not null);
        RemoveBenchmarkCommand = new AsyncRelayCommand(RemoveBenchmarkAsync, () => SelectedBenchmark is not null);
        ShowQcResultsCommand = new AsyncRelayCommand(ShowQcResultsAsync);
        ShowOverallQcResultsCommand = new AsyncRelayCommand(ShowOverallQcResultsAsync);
        SaveNotepadCommand = new AsyncRelayCommand(SaveNotepadAsync, () => !IsSavingPdf);
        SyncToErpCommand = new AsyncRelayCommand(SyncToErpAsync, () => !IsSyncingErp && IsConnected);
        LookupUnitInfoCommand = new AsyncRelayCommand(LookupUnitInfoAsync);
        LookupPoUnitsCommand = new AsyncRelayCommand(LookupPoUnitsAsync, () => !IsLookingUpPo);
        RegisterTesterCommand = new AsyncRelayCommand(RegisterTesterAsync, () => !IsRegisteringTester);
        RefreshTestersCommand = new AsyncRelayCommand(RefreshTestersAsync);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => SelectedUnit is not null && !IsConnected);

        // Pre-load testers list in background (fire-and-forget)
        _ = RefreshTestersAsync();

        BuildChecklistItems();
        OnPropertyChanged(nameof(BenchmarkChecklistItem));
        BuildBenchmarkOptions();
        RefreshData();
    }

    public ObservableCollection<QcChecklistItem> ChecklistItems { get; }

    public ObservableCollection<QcChecklistItem> DisplayChecklistItems { get; }

    public ObservableCollection<BenchmarkOption> BenchmarkOptions { get; }

    public QcChecklistItem? BenchmarkChecklistItem =>
        ChecklistItems.FirstOrDefault(x => x.Kind == QcChecklistKind.Benchmark);

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand BackToHomeCommand { get; }

    public IAsyncRelayCommand SavePdfCommand { get; }

    public IAsyncRelayCommand InstallBenchmarkCommand { get; }

    public IAsyncRelayCommand RemoveBenchmarkCommand { get; }

    public IAsyncRelayCommand ShowQcResultsCommand { get; }

    public IAsyncRelayCommand ShowOverallQcResultsCommand { get; }

    public IAsyncRelayCommand SaveNotepadCommand { get; }

    partial void OnIsSavingPdfChanged(bool value) => SavePdfCommand.NotifyCanExecuteChanged();

    partial void OnSelectedBenchmarkChanged(BenchmarkOption? value)
    {
        InstallBenchmarkCommand.NotifyCanExecuteChanged();
        RemoveBenchmarkCommand.NotifyCanExecuteChanged();
    }

    private void BuildChecklistItems()
    {
        foreach (var existing in ChecklistItems)
        {
            existing.PropertyChanged -= ItemOnPropertyChanged;
        }

        ChecklistItems.Clear();
        DisplayChecklistItems.Clear();

        AddItem("LCD Test", QcChecklistKind.LcdTest, "Uji warna layar (merah, hijau, biru, putih, hitam).", "lcd-test.png");
        AddItem("Storage Health", QcChecklistKind.StorageHealth, "Baca health dari Hard Disk Sentinel.", "storage-health.png");
        AddItem("Battery Health", QcChecklistKind.BatteryHealth, "Baca persentase battery health.", "battery-health.png");
        AddItem("Battery Report", QcChecklistKind.BatteryTest, "Jalankan battery report.", "battery-test.png");
        AddItem("Keyboard Test", QcChecklistKind.KeyboardTest, "Uji penekanan semua tombol keyboard.", "keyboard-test.png");
        AddItem("Audio Test", QcChecklistKind.AudioTest, "Audio 1/2 normal dan Left/Right channel.", "audio-test.png");
        AddItem("Benchmark", QcChecklistKind.Benchmark, "Jalankan benchmark yang dipilih.", "benchmark.png");
        AddItem("Camera", QcChecklistKind.Camera, "Buka aplikasi Camera Windows.", "camera-test.png");
        AddItem("Touchscreen Test", QcChecklistKind.TouchscreenTest, "Uji sentuhan layar.", "touchscreen-test.png");
        AddItem("Port Test", QcChecklistKind.PortTest, "Cek USB, audio jack, charger.", "port-test.png");
        AddItem("Touchpad", QcChecklistKind.Touchpad, "Buka program test touchpad (klik kiri/kanan, scroll, sensitivitas).", "touchpad-test.png");
        AddItem("Wifi", QcChecklistKind.Wifi, "Cek koneksi Wi-Fi aktif.", "wifi-test.png");
        AddItem("Backlight", QcChecklistKind.Backlight, "Cek brightness/backlight monitor.", "backlight-test.png");
        AddItem("Mic", QcChecklistKind.Mic, "Cek mikrofon terdeteksi.", "mic-test.png");
    }

    private void AddItem(string name, QcChecklistKind kind, string info, string iconFileName)
    {
        var item = new QcChecklistItem
        {
            Name = name,
            Kind = kind,
            InfoText = info,
            StatusText = "-",
            IconPath = ResolveQcIconPath(iconFileName)
        };

        item.RunCommand = new AsyncRelayCommand(() => RunItemAsync(item), () => !item.IsRunning);
        item.PropertyChanged += ItemOnPropertyChanged;
        ChecklistItems.Add(item);
        // Benchmark is exposed through the dedicated benchmark selector, not the list.
        if (kind is not QcChecklistKind.Benchmark)
        {
            DisplayChecklistItems.Add(item);
        }
    }

    private string ResolveQcIconPath(string iconFileName)
    {
        var runtimePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "Icons", "QC", iconFileName);
        if (File.Exists(runtimePath))
        {
            return runtimePath;
        }

        var sourcePath = Path.Combine(_workspaceRoot, "src", "RyotaQC.App", "Assets", "Images", "Icons", "QC", iconFileName);
        if (File.Exists(sourcePath))
        {
            return sourcePath;
        }

        return runtimePath;
    }

    private void BuildBenchmarkOptions()
    {
        BenchmarkOptions.Clear();

        var names = new[] { "HW Information", "CPU Heavy Load", "FurMark GPU Test", "Monitor Test", "SSD Test Speed" };
        foreach (var name in names)
        {
            var folder = FindQcFolder(name);
            BenchmarkOptions.Add(new BenchmarkOption
            {
                Name = name,
                FolderPath = folder ?? string.Empty,
                ExecutablePath = string.IsNullOrWhiteSpace(folder) ? string.Empty : (ResolveExecutable(folder) ?? string.Empty)
            });
        }

        SelectedBenchmark = BenchmarkOptions.FirstOrDefault();
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not QcChecklistItem item)
        {
            return;
        }

        if (e.PropertyName == nameof(QcChecklistItem.IsChecked))
        {
            if (item.IsChecked && !_isAutoUpdatingChecks)
            {
                EnsureQcStartDate();
            }

            UpdateStatusText(item);
            UpdateChecklistSummary();
        }
    }

    private void RefreshData()
    {
        BuildBenchmarkOptions();
        _driverInstallDate = TryReadDriverInstallDate();
        _cachedSystemModel = TryReadWmiValue("SELECT Model FROM Win32_ComputerSystem", "Model");
        _cachedSystemManufacturer = TryReadWmiValue("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
        _storageHealthPercent = TryReadStorageHealthPercent();
        _touchpadOk = DetectTouchpadAvailable();
        _wifiOk = DetectWifiConnected();
        _backlightOk = DetectBacklightActive();
        _micOk = DetectMicrophoneAvailable();
        _lastPortResult = null;
        BenchmarkActionStatus = "Idle";

        _isAutoUpdatingChecks = true;
        try
        {
            SetItemChecked(QcChecklistKind.Wifi, _wifiOk);
        }
        finally
        {
            _isAutoUpdatingChecks = false;
        }

        EnsureNotesTemplate();
        UpdateRealtimeClock();
        UpdateAllStatuses();
    }

    private void UpdateRealtimeClock()
    {
        LastRefreshText = $"Realtime: {DateTime.Now.ToString("dd MMMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("id-ID"))}";
    }

    private async Task RunItemAsync(QcChecklistItem item)
    {
        if (item.IsRunning)
        {
            return;
        }

        EnsureQcStartDate();
        item.IsRunning = true;
        item.RunCommand.NotifyCanExecuteChanged();

        // Audio: a QC test/tool is being launched.
        _audio.PlayLaunch();

        try
        {
            switch (item.Kind)
            {
                case QcChecklistKind.LcdTest:
                    _runner.RunLcdTest();
                    item.InfoText = "LCD Test selesai.";
                    await ShowResultPopupAsync(item, "LCD Test - Hasil:", "Apakah hasil LCD test OK?");
                    break;

                case QcChecklistKind.StorageHealth:
                    _runner.RunSentinel();
                    item.InfoText = "Hard Disk Sentinel dibuka. Baca nilai Health lalu input di popup.";
                    // Capture the submitted health percentage, then ask for the verdict.
                    var healthPercent = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        Views.Windows.QcNumberInputPopupWindow.Show(
                            "Storage Health",
                            "Masukkan nilai Health dari Hard Disk Sentinel:"));

                    if (healthPercent >= 0)
                    {
                        _storageHealthPercent = healthPercent;
                        item.InfoText = $"Storage Health: {healthPercent}%";

                        // Verdict popup: OK Pass / - Optional / X Fail (fail requires a note).
                        var verdict = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            Views.Windows.QcHealthVerdictPopupWindow.Show(
                                "Storage Health - Hasil:",
                                $"{healthPercent}%",
                                "70% - 100% = Pass · di bawah 70% = Fail"));
                        await ApplyHealthVerdictAsync(item, verdict, healthPercent);
                    }
                    else
                    {
                        await ShowResultPopupAsync(item, "Storage Health - Hasil:",
                            "Hard Disk Sentinel sudah dibuka. Apakah hasil OK?");
                    }
                    break;

                case QcChecklistKind.BatteryHealth:
                    _runner.RunBatteryHealth();
                    await Task.Delay(900);

                    // Capture Battery Health 1 (mandatory) + Battery Health 2 (optional).
                    var detectedHealth = await TryReadBatteryHealthPercentAsync();
                    var (bh1, bh2) = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        Views.Windows.QcDualBatteryPopupWindow.Show(
                            "Battery Health",
                            "Masukkan nilai Battery Health dari battery report:",
                            detectedHealth));

                    if (bh1 > 0)
                    {
                        _batteryHealthPercent = bh1;
                        _batteryHealth2Percent = bh2 > 0 ? bh2 : null;
                        item.InfoText = bh2 > 0
                            ? $"Battery Health: {bh1}% / {bh2}% (dual battery)"
                            : $"Battery Health: {bh1}%";

                        var verdictLabel = bh2 > 0 ? $"{bh1}% / {bh2}%" : $"{bh1}%";
                        var verdict = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            Views.Windows.QcHealthVerdictPopupWindow.Show(
                                "Battery Health - Hasil:",
                                verdictLabel,
                                "70% - 100% = Pass · di bawah 70% = Fail"));
                        await ApplyHealthVerdictAsync(item, verdict, bh1, bh2 > 0 ? bh2 : (int?)null);
                    }
                    else
                    {
                        await ShowResultPopupAsync(item, "Battery Health - Hasil:", "Battery Health sudah dicek. Apakah hasil OK?");
                        item.InfoText = "Battery Health: manual review";
                    }
                    break;

                case QcChecklistKind.BatteryTest:
                    await _runner.RunBatteryReportAsync();
                    item.InfoText = "Battery Report dijalankan.";
                    // Show result popup (OK/X/-) then capture the duration (jam + menit).
                    var batResult = Views.Windows.QcResultPopupWindow.Show(
                        "Battery Report - Hasil:",
                        "Battery Report sudah terbuka. Apakah hasil OK?");
                    if (batResult != Views.Windows.QcResultPopupWindow.QcResult.None)
                    {
                        var (timeText, totalMin) = Views.Windows.QcTimeInputPopupWindow.Show(
                            "Berapa lama hasil test?",
                            "Masukkan durasi hasil battery report (jam & menit):");
                        if (!string.IsNullOrEmpty(timeText))
                        {
                            _batteryLifetimeMinutes = totalMin;
                            item.ResultLabel = timeText;
                            item.ResultStatus = totalMin >= 60 ? "PASS" : "FAIL";
                            item.ResultColor = totalMin >= 60 ? "#FF1E7A4B" : "#FFB91C1C";
                            item.HasResult = true;
                            item.InfoText = $"Battery Report: {timeText}";
                            if (batResult == Views.Windows.QcResultPopupWindow.QcResult.Fail)
                            {
                                var note = Views.Windows.QcNotePopupWindow.Show(
                                    "Catatan: Mengapa Battery Report gagal (X)?");
                                item.ResultNote = note;
                            }
                        }
                        else
                        {
                            item.SetResult(batResult);
                        }
                    }
                    break;

                case QcChecklistKind.KeyboardTest:
                    _runner.RunKeyboardTest();
                    item.InfoText = "Keyboard test dibuka.";
                    await ShowResultPopupAsync(item, "Keyboard Test - Hasil:",
                        "PassMark KeyboardTest sudah dibuka dan ditutup. Apakah hasil OK?");
                    break;

                case QcChecklistKind.AudioTest:
                    _runner.RunAudioTest();
                    item.InfoText = "Audio test selesai.";
                    await ShowResultPopupAsync(item, "Audio Test - Hasil:",
                        "Audio test sudah selesai. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Benchmark:
                    await InstallBenchmarkAsync();
                    item.InfoText = SelectedBenchmark is not null
                        ? $"Benchmark dijalankan: {SelectedBenchmark.Name}."
                        : "Benchmark belum dipilih.";
                    await ShowResultPopupAsync(item, "Benchmark - Hasil:",
                        "Benchmark sudah dijalankan. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Camera:
                    _runner.RunCameraTest();
                    item.InfoText = "Camera dibuka.";
                    await ShowResultPopupAsync(item, "Camera - Hasil:",
                        "Camera sudah dibuka dan ditutup. Apakah hasil OK?");
                    break;

                case QcChecklistKind.TouchscreenTest:
                    var touchscreenAllOk = _runner.RunTouchscreenTest();
                    if (touchscreenAllOk)
                    {
                        // All boxes tapped green — auto Pass
                        item.SetResult(Views.Windows.QcResultPopupWindow.QcResult.Pass);
                        item.InfoText = "Touchscreen: Test Done Bang Aman! Semua kotak hijau.";
                    }
                    else
                    {
                        // Some boxes not tapped — show popup
                        item.InfoText = "Touchscreen: beberapa kotak belum di-tap.";
                        await ShowResultPopupAsync(item, "Touchscreen Test - Hasil:",
                            "Beberapa kotak belum hijau. Apakah hasil OK?");
                    }
                    break;

                case QcChecklistKind.PortTest:
                    _lastPortResult = DetectPortStatus();
                    item.InfoText = _lastPortResult.Value.ToDetailText();
                    await ShowResultPopupAsync(item, "Port Test - Hasil:",
                        $"Port: {_lastPortResult.Value.ToDetailText()}. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Touchpad:
                    var touchpadResult = _runner.RunTouchpadTest();
                    _touchpadOk = touchpadResult.Passed;
                    item.InfoText = touchpadResult.Passed
                        ? $"Touchpad valid. {touchpadResult.ToInfoText()}"
                        : $"Touchpad belum valid. {touchpadResult.ToInfoText()}";
                    await ShowResultPopupAsync(item, "Touchpad - Hasil:",
                        "Touchpad test sudah selesai. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Wifi:
                    _wifiOk = DetectWifiConnected();
                    item.InfoText = _wifiOk ? "Wi-Fi terhubung." : "Wi-Fi tidak terhubung.";
                    await ShowResultPopupAsync(item, "WiFi - Hasil:",
                        $"WiFi: {(_wifiOk ? "terhubung" : "tidak terhubung")}. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Backlight:
                    _backlightOk = DetectBacklightActive();
                    item.InfoText = _backlightOk ? "Backlight aktif." : "Backlight tidak terdeteksi.";
                    await ShowResultPopupAsync(item, "Backlight - Hasil:",
                        $"Backlight: {(_backlightOk ? "aktif" : "tidak terdeteksi")}. Apakah hasil OK?");
                    break;

                case QcChecklistKind.Mic:
                    _micOk = _runner.RunMicTest();
                    item.InfoText = _micOk ? "Mic test selesai, rekaman berhasil diputar." : "Mic test selesai, rekaman belum valid.";
                    await ShowResultPopupAsync(item, "Mic Test - Hasil:",
                        "Mic test sudah selesai. Apakah hasil OK?");
                    break;
            }

            item.IsChecked = true;
            UpdateAllStatuses();
            UpdateChecklistSummary();
        }
        catch (Exception ex)
        {
            _logger.Error($"QC run error: {item.Name}", ex);
            _feedback.ShowToast($"{item.Name} gagal dijalankan.", false);
        }
        finally
        {
            item.IsRunning = false;
            item.RunCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Shows the QC result popup (OK/X/-), and if the result is Fail (X),
    /// shows a follow-up note popup to capture the failure reason.
    /// Stores the result in the checklist item.
    /// </summary>
    private async Task ShowResultPopupAsync(QcChecklistItem item, string title, string message)
    {
        // Run on UI thread since popups are WPF windows
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Audio: QC test completed, result popup shown.
            _audio.PlayAction();
            var result = Views.Windows.QcResultPopupWindow.Show(title, message);

            if (result == Views.Windows.QcResultPopupWindow.QcResult.Fail)
            {
                // Show note popup for failure reason
                var note = Views.Windows.QcNotePopupWindow.Show($"Catatan: Mengapa {item.Name} gagal (X)?");
                item.SetResult(result, note);
            }
            else if (result != Views.Windows.QcResultPopupWindow.QcResult.None)
            {
                item.SetResult(result);
            }
        });
    }

    /// <summary>
    /// Applies a health-verdict choice (OK / - / X) to a checklist item.
    /// On Fail, the user must supply a reason note that is sent to the ERP.
    /// The numeric health value is always stored so the ERP Kanban reflects it.
    /// </summary>
    private async Task ApplyHealthVerdictAsync(
        QcChecklistItem item,
        Views.Windows.QcResultPopupWindow.QcResult verdict,
        int health1,
        int? health2 = null)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            switch (verdict)
            {
                case Views.Windows.QcResultPopupWindow.QcResult.Pass:
                    item.SetResult(verdict);
                    item.ResultLabel = health2.HasValue ? $"{health1}% / {health2}% OK" : $"{health1}% OK";
                    break;
                case Views.Windows.QcResultPopupWindow.QcResult.Skip:
                    item.SetResult(verdict);
                    item.ResultLabel = health2.HasValue ? $"{health1}% / {health2}%" : $"{health1}%";
                    break;
                case Views.Windows.QcResultPopupWindow.QcResult.Fail:
                    var note = Views.Windows.QcNotePopupWindow.Show(
                        $"Catatan: Mengapa {item.Name} gagal (X)?");
                    item.SetResult(verdict, note);
                    item.ResultLabel = health2.HasValue ? $"{health1}% / {health2}% X" : $"{health1}% X";
                    break;
            }
        });
    }

    private void UpdateAllStatuses()
    {
        foreach (var item in ChecklistItems)
        {
            UpdateStatusText(item);
        }

        UpdateChecklistSummary();
    }

    private void UpdateStatusText(QcChecklistItem item)
    {
        if (!item.IsChecked)
        {
            item.StatusText = "-";
            return;
        }

        switch (item.Kind)
        {
            case QcChecklistKind.StorageHealth:
                item.StatusText = _storageHealthPercent.HasValue ? $"{_storageHealthPercent.Value}%" : "PENDING";
                break;
            case QcChecklistKind.BatteryHealth:
                item.StatusText = _batteryHealthPercent.HasValue ? $"{_batteryHealthPercent.Value}%" : "PENDING";
                break;
            case QcChecklistKind.BatteryTest:
                item.StatusText = "OK";
                break;
            case QcChecklistKind.PortTest:
                item.StatusText = _lastPortResult.HasValue ? (_lastPortResult.Value.AllOk ? "OK" : "CHECK") : "PENDING";
                break;
            case QcChecklistKind.Touchpad:
                item.StatusText = _touchpadOk ? "OK" : "CHECK";
                break;
            case QcChecklistKind.Wifi:
                item.StatusText = _wifiOk ? "OK" : "CHECK";
                break;
            case QcChecklistKind.Backlight:
                item.StatusText = _backlightOk ? "OK" : "CHECK";
                break;
            case QcChecklistKind.Mic:
                item.StatusText = _micOk ? "OK" : "CHECK";
                break;
            default:
                item.StatusText = "OK";
                break;
        }
    }

    /// <summary>
    /// Shows the standard QC result popup (OK / - / X + failure note) for the Benchmark item.
    /// Bound to the "Status" button next to Uninstall in the Benchmark section.
    /// Same behaviour as the popup shown after the LCD Test.
    /// </summary>
    private async Task ShowQcResultsAsync()
    {
        var benchItem = ChecklistItems.FirstOrDefault(x => x.Kind == QcChecklistKind.Benchmark);
        if (benchItem is null) return;

        await ShowResultPopupAsync(
            benchItem,
            "Benchmark - Status:",
            "Apakah hasil Benchmark OK?");
    }

    /// <summary>
    /// Opens a popup showing ALL QC test results (overall checklist).
    /// Bound to the "Hasil QC" button below Catatan Tambahan.
    /// Also fills the Catatan Otomatis with the results.
    /// The popup contains Save PDF and Save Notepad buttons.
    /// </summary>
    private async Task ShowOverallQcResultsAsync()
    {
        var rows = BuildOverallResultRows();

        // Fill the Catatan Otomatis with current results
        UpdateChecklistSummary();

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Views.Windows.QcResultsPopupWindow.Show(
                rows,
                savePdfCallback: async () => await SavePdfAsync(),
                saveNotepadCallback: async () => await SyncToErpAsync());
        });
    }

    /// <summary>
    /// Builds the note for a row, including Running Test duration.
    /// </summary>
    private string BuildRowNote(QcChecklistItem item, QcChecklistKind kind)
    {
        // For Running Test, always include the duration
        if (kind == QcChecklistKind.RunningTest && item.HasResult && !string.IsNullOrWhiteSpace(item.ResultLabel))
        {
            var timeNote = $"Durasi: {item.ResultLabel}";
            if (item.ResultStatus == "FAIL" && !string.IsNullOrWhiteSpace(item.ResultNote))
                return $"{timeNote} | {item.ResultNote}";
            return timeNote;
        }

        return item.ResultNote ?? "";
    }

    /// <summary>
    /// Builds result rows for all QC items in the QC list order.
    /// </summary>
    private List<Views.Windows.QcResultRow> BuildOverallResultRows()
    {
        var orderedKinds = new[]
        {
            QcChecklistKind.LcdTest,
            QcChecklistKind.StorageHealth,
            QcChecklistKind.BatteryHealth,
            QcChecklistKind.BatteryTest,
            QcChecklistKind.KeyboardTest,
            QcChecklistKind.AudioTest,
            QcChecklistKind.Benchmark,
            QcChecklistKind.Camera,
            QcChecklistKind.TouchscreenTest,
            QcChecklistKind.PortTest,
            QcChecklistKind.Touchpad,
            QcChecklistKind.Wifi,
            QcChecklistKind.Backlight,
            QcChecklistKind.Mic
        };

        var rows = new List<Views.Windows.QcResultRow>();
        foreach (var kind in orderedKinds)
        {
            var item = ChecklistItems.FirstOrDefault(x => x.Kind == kind);
            if (item is null) continue;

            rows.Add(new Views.Windows.QcResultRow
            {
                Name = item.Name,
                ResultLabel = item.HasResult ? item.ResultLabel : "",
                ResultColor = item.HasResult ? item.ResultColor : "Transparent",
                Status = item.ResultStatus,
                Note = BuildRowNote(item, kind)
            });
        }
        return rows;
    }

    private void UpdateChecklistSummary()
    {
        var map = ChecklistItems.ToDictionary(item => item.Kind, item => item);
        string R(QcChecklistKind kind)
        {
            if (!map.TryGetValue(kind, out var item) || !item.HasResult)
                return "-";
            var label = item.ResultLabel;
            if (item.ResultStatus == "FAIL" && !string.IsNullOrWhiteSpace(item.ResultNote))
                return $"X ({item.ResultNote})";
            return label;
        }

        // Order must match the QC list on the right panel exactly
        ChecklistSummary = string.Join(Environment.NewLine,
        [
            $"LCD = {R(QcChecklistKind.LcdTest)}",
            $"Storage Health = {R(QcChecklistKind.StorageHealth)}",
            $"Battery Health = {R(QcChecklistKind.BatteryHealth)}",
            $"Battery Report = {R(QcChecklistKind.BatteryTest)}",
            $"Keyboard = {R(QcChecklistKind.KeyboardTest)}",
            $"Audio = {R(QcChecklistKind.AudioTest)}",
            $"Benchmark = {R(QcChecklistKind.Benchmark)}",
            $"Camera = {R(QcChecklistKind.Camera)}",
            $"Touchscreen = {R(QcChecklistKind.TouchscreenTest)}",
            $"Port = {R(QcChecklistKind.PortTest)}",
            $"Touchpad = {R(QcChecklistKind.Touchpad)}",
            $"Wifi = {R(QcChecklistKind.Wifi)}",
            $"Backlight = {R(QcChecklistKind.Backlight)}",
            $"Mic = {R(QcChecklistKind.Mic)}"
        ]);
    }

    private void SetItemChecked(QcChecklistKind kind, bool value)
    {
        var item = ChecklistItems.FirstOrDefault(x => x.Kind == kind);
        if (item is null || item.IsChecked == value)
        {
            return;
        }

        item.IsChecked = value;
    }

    private void EnsureQcStartDate()
    {
        if (_qcStartDate.HasValue)
        {
            return;
        }

        _qcStartDate = DateTime.Now;
        EnsureNotesTemplate();
    }

    private DateTime? TryReadDriverInstallDate()
    {
        try
        {
            if (!File.Exists(_driverInstallStampPath))
            {
                return null;
            }

            var raw = File.ReadAllText(_driverInstallStampPath).Trim();
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToLocalTime();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Read driver install timestamp failed.", ex);
        }

        return null;
    }

    private void EnsureNotesTemplate()
    {
        var text = string.IsNullOrWhiteSpace(Notes) ? string.Empty : Notes;
        var lines = text
            .Split([Environment.NewLine, "\n"], StringSplitOptions.None)
            .ToList();

        var kodeUnit = GetLineValue(lines, "Kode Unit =");
        if (string.IsNullOrWhiteSpace(kodeUnit))
        {
            kodeUnit = "( Di Isi Manual )";
        }

        var tanggalTest = FormatDate(_qcStartDate);
        var modelLaptop = ResolveModelLaptopForNotes(_cachedSystemModel, _cachedSystemManufacturer);

        UpsertLine(lines, "Kode Unit =", $"Kode Unit = {kodeUnit}");
        UpsertLine(lines, "Tanggal Test =", $"Tanggal Test = {tanggalTest}");
        UpsertLine(lines, "Model Laptop =", $"Model Laptop = {modelLaptop}");

        Notes = string.Join(Environment.NewLine, lines).TrimEnd();
    }

    /// <summary>
    /// Untuk Dell dan HP, System Model WMI sudah menampilkan nama model yang bisa dibaca
    /// (mis. "HP EliteBook 845 G8" atau "Dell Latitude 3210") → isi automatis.
    /// Untuk Thinkpad, Asus, dll yang System Model-nya berupa kode acak
    /// (mis. "20JSJSDSJL") → kosongkan agar diisi manual oleh user.
    /// </summary>
    private static string ResolveModelLaptopForNotes(string systemModel, string manufacturer)
    {
        var lowerMfr = (manufacturer ?? string.Empty).ToLowerInvariant();
        var isDellOrHp = lowerMfr.Contains("dell") || lowerMfr.Contains("hewlett");

        if (isDellOrHp)
        {
            if (!string.IsNullOrWhiteSpace(systemModel) &&
                !string.Equals(systemModel, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                return systemModel.Trim();
            }
        }

        return "( Di Isi Manual )";
    }

    private static string? GetLineValue(IReadOnlyList<string> lines, string prefix)
    {
        foreach (var line in lines)
        {
            if (!line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var index = line.IndexOf('=');
            return index >= 0 ? line[(index + 1)..].Trim() : string.Empty;
        }

        return null;
    }

    private static void UpsertLine(IList<string> lines, string prefix, string newLine)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lines[i] = newLine;
            return;
        }

        var insertIndex = 0;
        if (prefix.StartsWith("Model Laptop", StringComparison.OrdinalIgnoreCase))
        {
            insertIndex = Math.Min(2, lines.Count);
        }
        else if (prefix.StartsWith("Tanggal Test", StringComparison.OrdinalIgnoreCase))
        {
            insertIndex = Math.Min(1, lines.Count);
        }

        lines.Insert(insertIndex, newLine);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("id-ID"))
            : "-";
    }

    private async Task InstallBenchmarkAsync()
    {
        if (SelectedBenchmark is null)
        {
            return;
        }

        if (!SelectedBenchmark.IsAvailable)
        {
            BenchmarkActionStatus = $"Tidak ditemukan: {SelectedBenchmark.Name}";
            _feedback.ShowToast($"Benchmark {SelectedBenchmark.Name} tidak ditemukan.", false);
            return;
        }

        BenchmarkActionStatus = $"Menjalankan instalasi: {SelectedBenchmark.Name}";
        _runner.RunExternalTarget(SelectedBenchmark.ExecutablePath, true, SelectedBenchmark.Name);
        _ = RemoveDesktopShortcut(SelectedBenchmark.Name);
        SetItemChecked(QcChecklistKind.Benchmark, true);
        _feedback.ShowToast($"{SelectedBenchmark.Name} dijalankan.", true);
        BenchmarkActionStatus = $"Selesai: {SelectedBenchmark.Name}";

        await Task.CompletedTask;
    }

    private async Task RemoveBenchmarkAsync()
    {
        if (SelectedBenchmark is null)
        {
            return;
        }

        var uninstallStarted = TryRunUninstall(SelectedBenchmark.Name);

        if (uninstallStarted)
        {
            BenchmarkActionStatus = $"Uninstall dijalankan: {SelectedBenchmark.Name}";
            _feedback.ShowToast($"Uninstall {SelectedBenchmark.Name} dijalankan.", true);
        }
        else
        {
            BenchmarkActionStatus = $"Uninstall tidak tersedia: {SelectedBenchmark.Name}";
            _feedback.ShowToast($"Uninstall {SelectedBenchmark.Name} tidak tersedia.", false);
        }

        await Task.CompletedTask;
    }

    private bool RemoveDesktopShortcut(string shortcutName)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var linkPath = Path.Combine(desktopPath, $"{SanitizeFileName(shortcutName)}.lnk");
            if (!File.Exists(linkPath))
            {
                return false;
            }

            File.Delete(linkPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Remove shortcut failed: {shortcutName}", ex);
            return false;
        }
    }

    private bool TryRunUninstall(string appName)
    {
        try
        {
            var uninstallCommand = FindUninstallCommand(appName);
            if (string.IsNullOrWhiteSpace(uninstallCommand))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {uninstallCommand}",
                UseShellExecute = true,
                Verb = "runas"
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Uninstall failed: {appName}", ex);
            return false;
        }
    }

    private static string? FindUninstallCommand(string appName)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstallRoot = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstallRoot is null)
                    {
                        continue;
                    }

                    foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                    {
                        using var appKey = uninstallRoot.OpenSubKey(subKeyName);
                        var displayName = appKey?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        if (!displayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var uninstallString = appKey?.GetValue("UninstallString")?.ToString();
                        if (!string.IsNullOrWhiteSpace(uninstallString))
                        {
                            return uninstallString;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return null;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Shortcut" : sanitized;
    }

    private async Task<int?> TryReadBatteryHealthPercentAsync()
    {
        try
        {
            _batteryHealthNote = string.Empty;
            var logsPath = Path.Combine(_workspaceRoot, "logs");
            Directory.CreateDirectory(logsPath);

            var reportPath = Path.Combine(logsPath, $"battery-health-{DateTime.Now:yyyyMMdd_HHmmss}.html");

            var generated = await TryGenerateBatteryReportAsync(reportPath, elevated: false);

            if (!generated && !IsRunningAsAdmin())
            {
                generated = await TryGenerateBatteryReportAsync(reportPath, elevated: true);
            }

            if (!generated || !File.Exists(reportPath))
            {
                if (string.IsNullOrWhiteSpace(_batteryHealthNote))
                {
                    _batteryHealthNote = "Battery Health: report tidak berhasil dibuat.";
                }

                return null;
            }

            var html = await File.ReadAllTextAsync(reportPath);
            var design = ExtractCapacity(html, "DESIGN CAPACITY");
            var full = ExtractCapacity(html, "FULL CHARGE CAPACITY");
            if (design is not > 0 || full is not > 0)
            {
                return null;
            }

            var percent = (int)Math.Round((double)full.Value / design.Value * 100.0d);
            return Math.Clamp(percent, 0, 100);
        }
        catch (Exception ex)
        {
            _logger.Error("Read battery health failed.", ex);
            return null;
        }
    }

    private async Task<bool> TryGenerateBatteryReportAsync(string reportPath, bool elevated)
    {
        if (!elevated)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/batteryreport /output \"{reportPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                return true;
            }

            _logger.Error($"Battery health: powercfg gagal (exit code {process.ExitCode}).");
            return false;
        }

        try
        {
            using var elevatedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/batteryreport /output \"{reportPath}\"",
                UseShellExecute = true,
                Verb = "runas"
            });

            if (elevatedProcess is null)
            {
                return false;
            }

            await elevatedProcess.WaitForExitAsync();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _batteryHealthNote = "Battery Health butuh hak admin (izinkan UAC).";
            return false;
        }
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static long? ExtractCapacity(string html, string label)
    {
        var pattern = $@"{Regex.Escape(label)}.*?<td>\s*([0-9\.,\s]+)\s*mWh";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var digits = new string(match.Groups[1].Value.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var value) ? value : null;
    }

    private int? TryReadStorageHealthPercent()
    {
        try
        {
            var statusPath = ResolveSentinelStatusPath();
            if (string.IsNullOrWhiteSpace(statusPath) || !File.Exists(statusPath))
            {
                return null;
            }

            var healthValues = new List<int>();
            foreach (var line in File.ReadAllLines(statusPath))
            {
                // Format umum HDSentinel.sta:
                // 46115=40,40.00,12,97,40,22169792 -> health berada di index ke-4 (97)
                if (Regex.IsMatch(line, @"^\d{5}="))
                {
                    var parts = line[(line.IndexOf('=') + 1)..].Split(',');
                    if (parts.Length >= 4 && int.TryParse(parts[3], out var valueFromCsv))
                    {
                        healthValues.Add(Math.Clamp(valueFromCsv, 0, 100));
                        continue;
                    }
                }

                // Fallback jika format berbeda, misal ada teks "Health 97%" pada baris status.
                var healthMatch = Regex.Match(line, @"\bHealth\b[^0-9]{0,20}(\d{1,3})\s*%?", RegexOptions.IgnoreCase);
                if (healthMatch.Success &&
                    int.TryParse(healthMatch.Groups[1].Value, out var valueFromText))
                {
                    healthValues.Add(Math.Clamp(valueFromText, 0, 100));
                }
            }

            return healthValues.Count == 0 ? null : healthValues.Min();
        }
        catch (Exception ex)
        {
            _logger.Error("Read storage health failed.", ex);
            return null;
        }
    }

    private async Task<int?> TryReadStorageHealthPercentAsync()
    {
        // Sentinel butuh waktu menulis file status pada beberapa laptop.
        // Retry singkat agar hasil health lebih konsisten terbaca.
        const int retries = 8;
        for (var i = 0; i < retries; i++)
        {
            var value = TryReadStorageHealthPercent();
            if (value.HasValue)
            {
                return value.Value;
            }

            await Task.Delay(700);
        }

        return null;
    }

    private string? ResolveSentinelStatusPath()
    {
        var candidatePaths = new List<string>
        {
            _hdsStatusPath,
            Path.Combine(_qcRoot, "Hard Disk Sentinel", "HDSentinel.sta"),
            Path.Combine(_qcRoot, "Sentinel", "HDSentinel.sta"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hard Disk Sentinel", "HDSentinel.sta"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Hard Disk Sentinel", "HDSentinel.sta"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hard Disk Sentinel", "HDSentinel.sta")
        };

        if (Directory.Exists(_qcRoot))
        {
            candidatePaths.AddRange(
                Directory.EnumerateFiles(_qcRoot, "HDSentinel.sta", SearchOption.AllDirectories));
        }

        return candidatePaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => info.FullName)
            .FirstOrDefault();
    }

    private static PortCheckResult DetectPortStatus()
    {
        return new PortCheckResult(DetectUsbConnected(), DetectAudioJackConnected(), DetectChargerConnected());
    }

    private static bool DetectUsbConnected()
    {
        try
        {
            if (DriveInfo.GetDrives().Any(d => d.DriveType == DriveType.Removable && d.IsReady))
            {
                return true;
            }

            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%'");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var name = item["Name"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Contains("Root Hub", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Controller", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Composite Device", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool DetectAudioJackConnected()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name,Status FROM Win32_SoundDevice");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var name = item["Name"]?.ToString() ?? string.Empty;
                var status = item["Status"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) &&
                    (status.Equals("OK", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(status)))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool DetectChargerConnected()
    {
        try
        {
            return GetSystemPowerStatus(out var powerStatus) && powerStatus.ACLineStatus == 1;
        }
        catch
        {
            return false;
        }
    }

    private static bool DetectTouchpadAvailable()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PointingDevice");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var name = item["Name"]?.ToString() ?? string.Empty;
                if (name.Contains("touchpad", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("precision", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("synaptics", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("elan", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool DetectWifiConnected()
    {
        try
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Any(net =>
                    net.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    net.OperationalStatus == OperationalStatus.Up);
        }
        catch
        {
            return false;
        }
    }

    private static bool DetectBacklightActive()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var brightness = item["CurrentBrightness"];
                if (brightness is byte byteValue)
                {
                    return byteValue > 0;
                }

                if (brightness is ushort ushortValue)
                {
                    return ushortValue > 0;
                }
            }
        }
        catch
        {
            // ignore
        }

        return true;
    }

    private static bool DetectMicrophoneAvailable()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Microphone%' OR Name LIKE '%Mic%'");
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var name = item["Name"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private string? FindQcFolder(string requestedName)
    {
        if (!Directory.Exists(_qcRoot))
        {
            return null;
        }

        var exact = Path.Combine(_qcRoot, requestedName);
        if (Directory.Exists(exact))
        {
            return exact;
        }

        var token = NormalizeToken(requestedName);
        return Directory
            .EnumerateDirectories(_qcRoot)
            .FirstOrDefault(path => NormalizeToken(Path.GetFileName(path)) == token)
            ?? Directory
                .EnumerateDirectories(_qcRoot)
                .FirstOrDefault(path => NormalizeToken(Path.GetFileName(path)).Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveExecutable(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(folderPath, "*.exe", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("unins", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string NormalizeToken(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string TryReadWmiValue(string query, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                var value = item[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // ignore
        }

        return "N/A";
    }

    private async Task SavePdfAsync()
    {
        if (IsSavingPdf)
        {
            return;
        }

        IsSavingPdf = true;

        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var saveDialog = new SaveFileDialog
            {
                Title = "Simpan Hasil QC ke PDF",
                Filter = "PDF File (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                AddExtension = true,
                FileName = $"qc-report-{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            var selected = saveDialog.ShowDialog();
            if (selected != true || string.IsNullOrWhiteSpace(saveDialog.FileName))
            {
                _feedback.ShowToast("Simpan PDF dibatalkan.", false);
                return;
            }

            var reportPath = saveDialog.FileName;
            var systemModel = TryReadWmiValue("SELECT Model FROM Win32_ComputerSystem", "Model");
            var serial = TryReadWmiValue("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");
            var generatedAt = DateTime.Now;
            var checklistRows = BuildReportChecklistRows();
            var cardRows = BuildReportCardRows();
            var notesLines = (Notes ?? string.Empty).Split([Environment.NewLine, "\n"], StringSplitOptions.None);
            var codeUnit = GetLineValue(notesLines, "Kode Unit =") ?? "( Di Isi Manual )";
            var modelLaptop = GetLineValue(notesLines, "Model Laptop =") ?? "( Di Isi Manual )";
            var tanggalTest = FormatDate(_qcStartDate);
            var additionalNotes = BuildAdditionalNotes(Notes);

            await Task.Run(() =>
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"ryotaqc-{Guid.NewGuid():N}.pdf");

                try
                {
                    GenerateStyledReportPdf(
                        tempPath,
                        generatedAt,
                        systemModel,
                        serial,
                        checklistRows,
                        cardRows,
                        codeUnit,
                        tanggalTest,
                        modelLaptop,
                        additionalNotes);
                    _logger.Info("Styled QC PDF template generated.");

                    if (!IsPdfFileValid(tempPath))
                    {
                        throw new InvalidDataException("Generated PDF invalid or empty.");
                    }

                    File.Copy(tempPath, reportPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            });

            _feedback.ShowToast("PDF report QC berhasil disimpan.", true);
            _logger.Info($"QC PDF saved: {reportPath}");

            Process.Start(new ProcessStartInfo
            {
                FileName = reportPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Save QC PDF failed.", ex);
            _feedback.ShowToast("Gagal menyimpan PDF report QC.", false);
        }
        finally
        {
            IsSavingPdf = false;
        }
    }

    /// <summary>
    /// Saves the QC results as a Notepad (.txt) file with a readable table format.
    /// </summary>
    private async Task SaveNotepadAsync()
    {
        if (IsSavingPdf)
        {
            return;
        }

        IsSavingPdf = true;

        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Simpan Hasil QC ke Notepad",
                Filter = "Text File (*.txt)|*.txt",
                DefaultExt = "txt",
                AddExtension = true,
                FileName = $"qc-report-{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            var selected = saveDialog.ShowDialog();
            if (selected != true || string.IsNullOrWhiteSpace(saveDialog.FileName))
            {
                _feedback.ShowToast("Simpan Notepad dibatalkan.", false);
                return;
            }

            var reportPath = saveDialog.FileName;
            var systemModel = TryReadWmiValue("SELECT Model FROM Win32_ComputerSystem", "Model");
            var serial = TryReadWmiValue("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");
            var generatedAt = DateTime.Now;
            var rows = BuildOverallResultRows();
            var notesLines = (Notes ?? string.Empty).Split([Environment.NewLine, "\n"], StringSplitOptions.None);
            var modelLaptop = GetLineValue(notesLines, "Model Laptop =") ?? "( Di Isi Manual )";

            var sb = new StringBuilder();

            // Header
            sb.AppendLine("============================================");
            sb.AppendLine("           RYOTAQC V5 - HASIL QC");
            sb.AppendLine("============================================");
            sb.AppendLine();
            sb.AppendLine($"Tanggal Test   : {FormatDate(_qcStartDate)}");
            sb.AppendLine($"Model Laptop   : {modelLaptop}");
            sb.AppendLine($"Dibuat         : {generatedAt:dd MMMM yyyy HH:mm:ss}");
            sb.AppendLine($"System Model   : {systemModel}");
            sb.AppendLine($"Serial Number  : {serial}");
            sb.AppendLine();

            // Table header
            sb.AppendLine("============================================");
            sb.AppendLine($"{"Kategori QC",-22} {"Pass",-6} {"-",6} {"X Gagal"}");
            sb.AppendLine("============================================");

            // Data rows
            foreach (var row in rows)
            {
                var passMark = row.Status == "PASS" ? "[V]" : "";
                var skipMark = row.Status == "SKIP" ? "[V]" : "";
                // For FAIL: show [X] + note. For non-FAIL with note (Running Test): show note (duration).
                var failMark = row.Status == "FAIL"
                    ? "[X]" + (string.IsNullOrWhiteSpace(row.Note) ? "" : $" - {row.Note}")
                    : (!string.IsNullOrWhiteSpace(row.Note) ? row.Note : "");

                sb.AppendLine($"{row.Name,-22} {passMark,-6} {skipMark,-6} {failMark}");
            }

            sb.AppendLine("============================================");
            sb.AppendLine();

            // Summary
            var passCount = rows.Count(r => r.Status == "PASS");
            var failCount = rows.Count(r => r.Status == "FAIL");
            var skipCount = rows.Count(r => r.Status == "SKIP");
            var noResult = rows.Count(r => string.IsNullOrEmpty(r.Status));

            sb.AppendLine($"Total: {rows.Count}  |  Pass: {passCount}  |  Skip: {skipCount}  |  Fail: {failCount}  |  Belum: {noResult}");
            sb.AppendLine();

            // Additional notes (filtered: tidak menampilkan Kode Unit / Tanggal Test / Model Laptop
            // karena sudah punya field dedicated di header Notepad / Info section PDF)
            var additionalNotes = BuildAdditionalNotes(Notes);
            if (additionalNotes.Count > 0)
            {
                sb.AppendLine("============================================");
                sb.AppendLine("Catatan Tambahan:");
                sb.AppendLine("============================================");
                foreach (var noteLine in additionalNotes)
                {
                    sb.AppendLine(noteLine);
                }
            }

            await Task.Run(() => File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8));

            _feedback.ShowToast("Notepad QC berhasil disimpan.", true);
            _logger.Info($"QC Notepad saved: {reportPath}");

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{reportPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Save QC Notepad failed.", ex);
            _feedback.ShowToast("Gagal menyimpan Notepad QC.", false);
        }
        finally
        {
            IsSavingPdf = false;
        }
    }

    private void GenerateStyledReportPdf(
        string outputPath,
        DateTime generatedAt,
        string systemModel,
        string serial,
        IReadOnlyList<(string Label, string Status, string Note)> checklistRows,
        IReadOnlyList<ReportCard[]> cardRows,
        string codeUnit,
        string tanggalTest,
        string modelLaptop,
        IReadOnlyList<string> additionalNotes)
    {
        var generatedText = generatedAt.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("id-ID"))
                            + " \u00B7 "
                            + generatedAt.ToString("HH:mm", CultureInfo.GetCultureInfo("id-ID"));

        var passCount = checklistRows.Count(r => r.Status == "OK");
        var failCount = checklistRows.Count(r => r.Status == "X");
        var otherCount = checklistRows.Count - passCount - failCount;

        Document
            .Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor("#FFFFFF");
                    page.DefaultTextStyle(t => t.FontSize(10).FontColor("#111827").LineHeight(1.2f));

                    page.Content().Column(root =>
                    {
                        root.Spacing(0);

                        // ---------- Header ----------
                        root.Item().PaddingTop(2).Text("RYOTAQC V / QUALITY ASSURANCE")
                            .FontSize(9.5f).SemiBold().FontColor("#2E4B8F").LetterSpacing(0.08f);

                        root.Item().PaddingTop(4).Text("Quality Control Report")
                            .FontSize(24).Bold().FontColor("#0F1B47");

                        root.Item().PaddingTop(1).Text("Laporan pemeriksaan unit laptop")
                            .FontSize(10).FontColor("#4B5563");

                        // ---------- Info grid ----------
                        root.Item().PaddingTop(14).Table(infoTable =>
                        {
                            infoTable.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            void InfoLabel(string text)
                            {
                                infoTable.Cell().Border(0.8f).BorderColor("#C9D3E0")
                                    .Background("#F3F6FB").PaddingVertical(5).PaddingHorizontal(10)
                                    .Text(text).FontSize(8.5f).SemiBold().FontColor("#5A6B85").LetterSpacing(0.06f);
                            }

                            void InfoValue(string text)
                            {
                                infoTable.Cell().Border(0.8f).BorderColor("#C9D3E0")
                                    .Background("#FFFFFF").PaddingVertical(6).PaddingHorizontal(10)
                                    .Text(string.IsNullOrWhiteSpace(text) ? "-" : text)
                                    .FontSize(11).SemiBold().FontColor("#0F1B47");
                            }

                            InfoLabel("MODEL SYSTEM");
                            InfoLabel("SERIAL NUMBER");
                            InfoLabel("TANGGAL TEST");
                            InfoValue(systemModel);
                            InfoValue(serial);
                            InfoValue(tanggalTest);

                            InfoLabel("GENERATED");
                            InfoLabel("KODE UNIT");
                            InfoLabel("MODEL LAPTOP");
                            InfoValue(generatedText);
                            InfoValue(string.IsNullOrWhiteSpace(codeUnit) ? "Diisi manual" : codeUnit);
                            InfoValue(string.IsNullOrWhiteSpace(modelLaptop) ? "Diisi manual" : modelLaptop);
                        });

                        // ---------- Hasil checklist ----------
                        root.Item().PaddingTop(14).Text("HASIL CHECKLIST")
                            .FontSize(11).Bold().FontColor("#0F1B47").LetterSpacing(0.05f);

                        root.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3.1f);   // Kategori QC
                                cols.RelativeColumn(1.15f);  // PASS
                                cols.RelativeColumn(1.15f);  // -
                                cols.RelativeColumn(1.15f);  // FAIL
                                cols.RelativeColumn(4.6f);   // Catatan
                            });

                            // Header row
                            table.Header(header =>
                            {
                                void Head(string text, bool center = false)
                                {
                                    if (center)
                                    {
                                        header.Cell().Background("#16244B")
                                            .BorderColor("#16244B").BorderTop(0.8f).BorderBottom(0.8f)
                                            .BorderLeft(0.8f).BorderRight(0.8f)
                                            .PaddingVertical(7).PaddingHorizontal(8)
                                            .Text(text).FontSize(9).Bold().FontColor("#FFFFFF")
                                            .LetterSpacing(0.05f).AlignCenter();
                                    }
                                    else
                                    {
                                        header.Cell().Background("#16244B")
                                            .BorderColor("#16244B").BorderTop(0.8f).BorderBottom(0.8f)
                                            .BorderLeft(0.8f).BorderRight(0.8f)
                                            .PaddingVertical(7).PaddingHorizontal(8)
                                            .Text(text).FontSize(9).Bold().FontColor("#FFFFFF")
                                            .LetterSpacing(0.05f);
                                    }
                                }

                                Head("KATEGORI QC");
                                Head("PASS", center: true);
                                Head("\u2014", center: true);
                                Head("FAIL", center: true);
                                Head("CATATAN / KETERANGAN FAIL", center: true);
                            });

                            for (var i = 0; i < checklistRows.Count; i++)
                            {
                                var (label, status, note) = checklistRows[i];
                                var isPass = status == "OK";
                                var isFail = status == "X";

                                var rowBg = isFail ? "#FDF2F2" : isPass ? "#F3FBF5" : "#FFFFFF";
                                var passBg = isPass ? "#E6F7EA" : "#FFFFFF";
                                var failBg = isFail ? "#FDE8E8" : "#FFFFFF";

                                QuestPDF.Infrastructure.IContainer CellBase(QuestPDF.Infrastructure.IContainer c, string bg)
                                {
                                    return c.Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background(bg).PaddingVertical(5).PaddingHorizontal(8);
                                }

                                // Kategori
                                CellBase(table.Cell(), rowBg)
                                    .Text(label).FontSize(10.5f).Bold().FontColor("#0F1B47");

                                // PASS
                                if (isPass)
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background(passBg).PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text("\u2713").FontSize(14).Bold().FontColor("#1F9D55");
                                }
                                else
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background(passBg).PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(string.Empty);
                                }

                                // -
                                if (status == "-")
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background("#FFFFFF").PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text("\u2014").FontSize(11).Bold().FontColor("#8A94A6");
                                }
                                else
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background("#FFFFFF").PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(string.Empty);
                                }

                                // FAIL
                                if (isFail)
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background(failBg).PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text("\u2713").FontSize(14).Bold().FontColor("#DC2626");
                                }
                                else
                                {
                                    table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                        .BorderTop(0).BorderBottom(0.7f)
                                        .BorderLeft(0).BorderRight(0.7f)
                                        .Background(failBg).PaddingVertical(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(string.Empty);
                                }

                                // Catatan
                                var noteText = note;
                                if (string.IsNullOrWhiteSpace(noteText))
                                {
                                    noteText = "\u2014";
                                }

                                table.Cell().Border(0.7f).BorderColor("#C9D3E0")
                                    .BorderTop(0).BorderBottom(0.7f)
                                    .BorderLeft(0).BorderRight(0.7f)
                                    .Background(rowBg).PaddingVertical(5).PaddingHorizontal(8)
                                    .Text(noteText)
                                    .FontSize(9.5f)
                                    .FontColor(isFail ? "#0F1B47" : "#8A94A6");
                            }
                        });

                        // ---------- Summary bar ----------
                        root.Item().PaddingTop(9).Table(summary =>
                        {
                            summary.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            summary.Cell().Border(0.8f).BorderColor("#C9D3E0")
                                .Background("#F3F6FB").PaddingVertical(9).PaddingHorizontal(12)
                                .Text(t =>
                                {
                                    t.Span($"{passCount} ").FontSize(14).Bold().FontColor("#1F9D55");
                                    t.Span("PASS").FontSize(11).Bold().FontColor("#1F9D55");
                                });

                            summary.Cell().Border(0.8f).BorderColor("#C9D3E0")
                                .Background("#F3F6FB").PaddingVertical(9).PaddingHorizontal(12)
                                .Text(t =>
                                {
                                    t.Span($"{otherCount} ").FontSize(14).Bold().FontColor("#5A6B85");
                                    t.Span("BELUM / \u2014").FontSize(11).Bold().FontColor("#5A6B85");
                                });

                            summary.Cell().Border(0.8f).BorderColor("#C9D3E0")
                                .Background("#F3F6FB").PaddingVertical(9).PaddingHorizontal(12)
                                .Text(t =>
                                {
                                    t.Span($"{failCount} ").FontSize(14).Bold().FontColor("#DC2626");
                                    t.Span("FAIL").FontSize(11).Bold().FontColor("#DC2626");
                                });
                        });

                        // ---------- Catatan tambahan ----------
                        if (additionalNotes is { Count: > 0 })
                        {
                            var visibleNotes = additionalNotes
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .Take(4)
                                .ToList();

                            if (visibleNotes.Count > 0)
                            {
                                root.Item().PaddingTop(10).Column(notesCol =>
                                {
                                    notesCol.Spacing(1);
                                    foreach (var noteLine in visibleNotes)
                                    {
                                        notesCol.Item().Text(noteLine).FontSize(8.5f).FontColor("#4B5563");
                                    }
                                });
                            }
                        }

                        // ---------- Legend ----------
                        root.Item().PaddingTop(9).Text(t =>
                        {
                            t.Span("Catatan: ").FontSize(9).FontColor("#6B7280");
                            t.Span("\u2713").FontSize(9).SemiBold().FontColor("#1F9D55");
                            t.Span(" menandakan hasil sesuai kolom. Tanda ").FontSize(9).FontColor("#6B7280");
                            t.Span("\u2014").FontSize(9).SemiBold().FontColor("#8A94A6");
                            t.Span(" menunjukkan item belum diuji / tidak ada hasil pada laporan sumber.")
                                .FontSize(9).FontColor("#6B7280");
                        });

                        root.Item().PaddingTop(10).LineHorizontal(0.8f).LineColor("#E5E7EB");

                        // ---------- Footer ----------
                        root.Item().PaddingTop(8).Row(footer =>
                        {
                            footer.RelativeItem().Text(t =>
                            {
                                t.Span("Dibuat oleh ").FontSize(9).FontColor("#6B7280");
                                t.Span("IllhamRyota").FontSize(9).SemiBold().FontColor("#0F1B47");
                                t.Span("  \u00B7  Copyright \u00A9 RyotaQC V").FontSize(9).FontColor("#6B7280");
                            });

                            footer.ConstantItem(200).AlignRight()
                                .Text("RYOTAQC \u00B7 QC & MAINTENANCE")
                                .FontSize(9).Bold().FontColor("#2E4B8F").LetterSpacing(0.05f);
                        });
                    });
                });
            })
            .GeneratePdf(outputPath);
    }

    private static bool IsPdfFileValid(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length < 32)
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        var read = stream.Read(header);
        if (!(read == 5 &&
              header[0] == (byte)'%' &&
              header[1] == (byte)'P' &&
              header[2] == (byte)'D' &&
              header[3] == (byte)'F' &&
              header[4] == (byte)'-'))
        {
            return false;
        }

        var seekBack = Math.Min(4096, (int)stream.Length);
        stream.Seek(-seekBack, SeekOrigin.End);
        var tailBuffer = new byte[seekBack];
        var tailRead = stream.Read(tailBuffer, 0, seekBack);
        if (tailRead <= 0)
        {
            return false;
        }

        var tailText = System.Text.Encoding.ASCII.GetString(tailBuffer, 0, tailRead);
        return tailText.Contains("%%EOF", StringComparison.Ordinal);
    }

    private List<(string Label, string Status, string Note)> BuildReportChecklistRows()
    {
        return
        [
            ("LCD", GetChecklistStatus(QcChecklistKind.LcdTest), GetChecklistNote(QcChecklistKind.LcdTest)),
            ("Storage Health", GetChecklistStatus(QcChecklistKind.StorageHealth), GetChecklistNote(QcChecklistKind.StorageHealth)),
            ("Battery Health", GetChecklistStatus(QcChecklistKind.BatteryHealth), GetChecklistNote(QcChecklistKind.BatteryHealth)),
            ("Battery Report", GetChecklistStatus(QcChecklistKind.BatteryTest), GetChecklistNote(QcChecklistKind.BatteryTest)),
            ("Keyboard", GetChecklistStatus(QcChecklistKind.KeyboardTest), GetChecklistNote(QcChecklistKind.KeyboardTest)),
            ("Audio", GetChecklistStatus(QcChecklistKind.AudioTest), GetChecklistNote(QcChecklistKind.AudioTest)),
            ("Benchmark", GetChecklistStatus(QcChecklistKind.Benchmark), GetChecklistNote(QcChecklistKind.Benchmark)),
            ("Touchscreen", GetChecklistStatus(QcChecklistKind.TouchscreenTest), GetChecklistNote(QcChecklistKind.TouchscreenTest)),
            ("Port", GetChecklistStatus(QcChecklistKind.PortTest), GetChecklistNote(QcChecklistKind.PortTest)),
            ("Touchpad", GetChecklistStatus(QcChecklistKind.Touchpad), GetChecklistNote(QcChecklistKind.Touchpad)),
            ("Wifi", GetChecklistStatus(QcChecklistKind.Wifi), GetChecklistNote(QcChecklistKind.Wifi)),
            ("Camera", GetChecklistStatus(QcChecklistKind.Camera), GetChecklistNote(QcChecklistKind.Camera)),
            ("Backlight", GetChecklistStatus(QcChecklistKind.Backlight), GetChecklistNote(QcChecklistKind.Backlight)),
            ("Mic", GetChecklistStatus(QcChecklistKind.Mic), GetChecklistNote(QcChecklistKind.Mic))
        ];
    }

    private List<ReportCard[]> BuildReportCardRows()
    {
        var wifiStatus = GetChecklistStatus(QcChecklistKind.Wifi);
        var cameraStatus = GetChecklistStatus(QcChecklistKind.Camera);
        var combinedWifiCamera = (wifiStatus, cameraStatus) switch
        {
            ("OK", "OK") => "OK",
            ("-", "-") => "-",
            _ => "CHECK"
        };

        return
        [
            [new ReportCard("Storage Health", GetChecklistStatus(QcChecklistKind.StorageHealth), ["benchmark.png", "storage-health.png"]),
             new ReportCard("Battery Health", GetChecklistStatus(QcChecklistKind.BatteryHealth), ["battery-test.png", "battery-health.png"])],
            [new ReportCard("Keyboard", GetChecklistStatus(QcChecklistKind.KeyboardTest), ["keyboard-test.png", "audio-test.png"]),
             new ReportCard("Touchscreen", GetChecklistStatus(QcChecklistKind.TouchscreenTest), ["port-test.png", "touchscreen-test.png"])],
            [new ReportCard("WiFi / Camera", combinedWifiCamera, ["wifi-test.png", "camera-test.png"]),
             new ReportCard("Backlight", GetChecklistStatus(QcChecklistKind.Backlight), ["mic-test.png", "backlight-test.png"])]
        ];
    }

    private static List<string> BuildAdditionalNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return [];
        }

        var result = new List<string>();
        var lines = notes.Split([Environment.NewLine, "\n"], StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Kode Unit =", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Tanggal Test =", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Tanggal Test Instalasi Driver =", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Tanggal Driver =", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Model Laptop =", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(line);
        }

        return result.Take(5).ToList();
    }

    private string GetChecklistStatus(QcChecklistKind kind)
    {
        var item = ChecklistItems.FirstOrDefault(x => x.Kind == kind);
        if (item is null || !item.HasResult)
        {
            return "-";
        }

        // Use the new result system: PASS → "OK", SKIP → "-", FAIL → "X"
        return item.ResultStatus switch
        {
            "PASS" => "OK",
            "SKIP" => "-",
            "FAIL" => "X",
            _ => NormalizeReportStatus(item.StatusText)
        };
    }

    /// <summary>
    /// Returns the failure note for a checklist item (if result is FAIL).
    /// Used in the PDF report to show the reason next to the X mark.
    /// </summary>
    private string GetChecklistNote(QcChecklistKind kind)
    {
        var item = ChecklistItems.FirstOrDefault(x => x.Kind == kind);
        if (item is null)
        {
            return string.Empty;
        }

        // For Running Test, always include the duration (time input from popup)
        if (kind == QcChecklistKind.RunningTest && item.HasResult && !string.IsNullOrWhiteSpace(item.ResultLabel))
        {
            var timeNote = $"Durasi: {item.ResultLabel}";
            if (item.ResultStatus == "FAIL" && !string.IsNullOrWhiteSpace(item.ResultNote))
                return $"{timeNote} | {item.ResultNote}";
            return timeNote;
        }

        if (item.ResultStatus != "FAIL")
        {
            return string.Empty;
        }
        return item.ResultNote ?? string.Empty;
    }

    /// <summary>
    /// Computes the overall QC status from the mapped ERP tests + numeric tests,
    /// mirroring the ERP rule set: any mandatory FAIL → FAIL, any ISSUE → ISSUE,
    /// numeric health below 70 → FAIL, otherwise PASS.
    /// </summary>
    private static string ComputeOverallStatus(
        Dictionary<string, object> tests,
        Dictionary<string, object> numericTests)
    {
        string StatusOf(string key) =>
            tests.TryGetValue(key, out var v) && v is QcErpMapper.ErpTestValue t
                ? t.Status
                : string.Empty;

        var mandatory = new[]
        {
            "lcdTest", "wifiTest", "keyboard", "camera",
            "microphone", "audioSpeaker", "port", "touchpad", "stressTest"
        };

        foreach (var key in mandatory)
        {
            if (StatusOf(key) == "FAIL")
            {
                return "FAIL";
            }
        }

        if (tests.Values.OfType<QcErpMapper.ErpTestValue>().Any(t => t.Status == "ISSUE"))
        {
            return "ISSUE";
        }

        if (numericTests.TryGetValue("hddSsdHealth", out var sh) && sh is int shv && shv < 70)
        {
            return "FAIL";
        }

        if (numericTests.TryGetValue("batteryHealth", out var bh) && bh is int bhv && bhv < 70)
        {
            return "FAIL";
        }

        return "PASS";
    }

    private static string NormalizeReportStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "-";
        }

        var value = status.Trim();
        if (value.Equals("-", StringComparison.OrdinalIgnoreCase))
        {
            return "-";
        }

        if (value.Contains("OK", StringComparison.OrdinalIgnoreCase))
        {
            return "OK";
        }

        if (value.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
        {
            return "CHECK";
        }

        if (value.EndsWith("%", StringComparison.Ordinal))
        {
            return value;
        }

        if (value.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            return "TEST";
        }

        return value;
    }

    private static void ComposeReportStatusBadge(QuestPDF.Infrastructure.IContainer container, string status)
    {
        var normalized = NormalizeReportStatus(status);
        var background = normalized switch
        {
            "OK" => "#2CAD52",
            "-" => "#A8B0B7",
            "CHECK" => "#C9893A",
            _ when normalized.EndsWith("%", StringComparison.Ordinal) => "#2E8AC8",
            _ => "#7B8B98"
        };

        container
            .Border(0.8f)
            .BorderColor("#9FB2BE")
            .CornerRadius(6)
            .Background(background)
            .PaddingVertical(2.5f)
            .PaddingHorizontal(7)
            .AlignCenter()
            .Text(normalized)
            .SemiBold()
            .FontSize(9.8f)
            .FontColor(Colors.White);
    }

    private byte[]? LoadPdfIconBytes(string iconFileName)
    {
        var runtimePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", "Icons", "QC", iconFileName);
        if (File.Exists(runtimePath))
        {
            return File.ReadAllBytes(runtimePath);
        }

        var sourcePath = Path.Combine(_workspaceRoot, "src", "RyotaQC.App", "Assets", "Images", "Icons", "QC", iconFileName);
        if (File.Exists(sourcePath))
        {
            return File.ReadAllBytes(sourcePath);
        }

        return null;
    }

    private async Task LookupUnitInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(PraQcCode))
        {
            _feedback.ShowWarning("Silakan masukkan Kode Pra-QC terlebih dahulu.");
            return;
        }

        ErpSyncStatus = "Mencari data unit di ERP...";
        ErpSyncStatusColorBrush = System.Windows.Media.Brushes.Yellow;

        var result = await _erpSyncService.LookupUnitAsync(PraQcCode.Trim());
        if (result != null && result.Success && result.Data != null)
        {
            var d = result.Data;
            ErpUnitDetails = $"Unit: {d.ProductName} ({d.Brand} {d.Model})\nPO: {d.PoNumber} | Status: {d.Status}";
            HasErpUnitDetails = true;
            ErpSyncStatus = "Unit ditemukan di ERP!";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            ErpUnitDetails = string.Empty;
            HasErpUnitDetails = false;
            ErpSyncStatus = $"Unit '{PraQcCode}' tidak ditemukan di ERP.";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private async Task SyncToErpAsync()
    {
        if (string.IsNullOrWhiteSpace(PraQcCode))
        {
            _feedback.ShowWarning("Pilih unit dari daftar PO terlebih dahulu sebelum kirim ke ERP.");
            return;
        }

        if (!IsConnected)
        {
            _feedback.ShowWarning("Klik Hubungkan terlebih dahulu untuk binding app ke unit ERP.");
            return;
        }

        try
        {
            IsSyncingErp = true;
            ErpSyncStatus = "Mengambil data WMI dan mengunggah ke ERP Hub...";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.Cyan;

            // 1. Ambil spesifikasi WMI hardware
            var sysInfo = await _systemInfoService.GetSystemInfoAsync();

            // 2. Bangun ErpSpecs (support dual storage HDD+SSD dan dual VGA)
            var specs = new ErpSpecs
            {
                Processor = sysInfo.CpuName,
                Ram = string.IsNullOrWhiteSpace(sysInfo.RamInstalled) ? sysInfo.RamTotal : sysInfo.RamInstalled,
                Storage = string.IsNullOrWhiteSpace(sysInfo.StorageDetail) ? sysInfo.StorageSummary : sysInfo.StorageDetail,
                Display = string.IsNullOrWhiteSpace(sysInfo.DisplayDetail) ? sysInfo.LcdInfo : sysInfo.DisplayDetail
            };

            // Coba split storage HDD vs SSD jika ada info dual
            if (!string.IsNullOrWhiteSpace(sysInfo.StorageDetail))
            {
                var (hdd, ssd) = SplitStorageHddSsd(sysInfo.StorageDetail);
                specs.StorageHdd = hdd;
                specs.StorageSsd = ssd;
            }

            // VGA: cek apakah dual (integrated + dedicated)
            if (!string.IsNullOrWhiteSpace(sysInfo.VgaInfo))
            {
                var (integrated, dedicated) = SplitVgaIntegratedDedicated(sysInfo.VgaInfo);
                if (!string.IsNullOrEmpty(integrated) && !string.IsNullOrEmpty(dedicated))
                {
                    specs.VgaIntegrated = integrated;
                    specs.VgaDedicated = dedicated;
                }
                else
                {
                    specs.Vga = sysInfo.VgaInfo;
                }
            }

            // 3. Kumpulkan hasil parameter checklist dalam format ERP Kanban
            //    (qcData.tests + qcData.numericTests) via QcErpMapper.
            var mapperItems = ChecklistItems
                .Select(i => (i.Kind.ToString(), i.HasResult, i.ResultStatus, i.ResultNote, i.InfoText))
                .ToList();

            var tests = QcErpMapper.BuildTests(mapperItems);
            var numericTests = QcErpMapper.BuildNumericTests(
                _storageHealthPercent,
                _batteryHealthPercent,
                _batteryHealth2Percent,
                _batteryLifetimeMinutes);

            // Overall status from the mapped ERP tests + numeric thresholds.
            var overallStatus = ComputeOverallStatus(tests, numericTests);
            var allPassed = overallStatus == "PASS";

            // 4. Bangun payload ERP (qcData.tests + numericTests) dengan QcStatus + TesterId
            var payload = new ErpQcPayload
            {
                PraQcCode = PraQcCode.Trim(),
                SerialNumber = sysInfo.BiosSerialNumber,
                Brand = _cachedSystemManufacturer,
                Model = _cachedSystemModel,
                Specs = specs,
                BatteryHealth = _batteryHealthPercent,
                BatteryHealth2 = _batteryHealth2Percent,
                StorageHealth = _storageHealthPercent,
                BatteryLifetimeMinutes = _batteryLifetimeMinutes,
                Status = allPassed ? "PASS" : "FAIL",
                QcStatus = string.IsNullOrWhiteSpace(QcStatus) ? "LULUS" : QcStatus.ToUpperInvariant(),
                Tests = tests,
                NumericTests = numericTests,
                TestedBy = SelectedTester?.Name ?? sysInfo.UserName ?? "Teknisi QC",
                TesterId = SelectedTester?.Id,
                Notes = string.Empty
            };

            // 5. Kirim ke Hub Contabo :8005
            var response = await _erpSyncService.SubmitQcAsync(payload);

            if (response.Success)
            {
                ErpSyncStatus = $"Berhasil! Status: {payload.QcStatus}\n{response.Message}";
                ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGreen;
                _audio.PlayAction();
                _feedback.ShowInfo($"Hasil QC untuk {PraQcCode} berhasil dikirim ke ERP!\nStatus: {payload.QcStatus}");
            }
            else
            {
                ErpSyncStatus = $"Gagal kirim: {response.Message}";
                ErpSyncStatusColorBrush = System.Windows.Media.Brushes.OrangeRed;
                _audio.PlayClose();
                _feedback.ShowError($"Gagal kirim ke ERP: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            ErpSyncStatus = $"Error: {ex.Message}";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.Red;
            _audio.PlayClose();
            _logger.Error("Error syncing QC to ERP", ex);
            _feedback.ShowError(ex.Message);
        }
        finally
        {
            IsSyncingErp = false;
        }
    }

    /// <summary>
    /// Split storage detail menjadi HDD vs SSD berdasarkan interface.
    /// Aturan: NVMe / M.2 NVMe / SSD → SSD; HDD / M.2 SATA / SATA (I/II/III) → HDD.
    /// Jika hanya terbaca SSD NVMe saja, kolom HDD dikosongkan.
    /// Contoh input: "SSD M.2 NVMe — 238 GB" atau "HDD SATA III — 500 GB"
    /// </summary>
    private static (string hdd, string ssd) SplitStorageHddSsd(string storageDetail)
    {
        if (string.IsNullOrWhiteSpace(storageDetail)) return (string.Empty, string.Empty);

        var parts = storageDetail.Split(
            new[] { '/', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string hdd = string.Empty, ssd = string.Empty;

        foreach (var p in parts)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;

            var lower = p.ToLowerInvariant();

            // SATA-family / rotational / hard-disk signals → HDD (Hardisk column).
            var isHddFamily =
                lower.Contains("m.2 sata") || lower.Contains("m2 sata") || lower.Contains("ngff")
                || lower.Contains("sata ii") || lower.Contains("sata iii") || lower.Contains("sata3")
                || lower.Contains("sata2")
                || lower.Contains("hdd") || lower.Contains("hard disk") || lower.Contains("harddisk")
                || lower.Contains("7200rpm") || lower.Contains("5400rpm");

            // Solid-state signals → SSD column.
            var isSsdFamily =
                lower.Contains("nvme") || lower.Contains("ssd") || lower.Contains("solid state");

            if (isHddFamily)
            {
                hdd = string.IsNullOrEmpty(hdd) ? p : $"{hdd}; {p}";
            }
            else if (isSsdFamily)
            {
                ssd = string.IsNullOrEmpty(ssd) ? p : $"{ssd}; {p}";
            }
            else
            {
                // Unknown disk: if it has a SATA interface treat as HDD, else SSD.
                if (lower.Contains("sata"))
                {
                    hdd = string.IsNullOrEmpty(hdd) ? p : $"{hdd}; {p}";
                }
                else
                {
                    ssd = string.IsNullOrEmpty(ssd) ? p : $"{ssd}; {p}";
                }
            }
        }

        return (hdd, ssd);
    }

    /// <summary>
    /// Cek apakah VGA info mengandung dua adapter (integrated + dedicated).
    /// Contoh: "Intel HD 530 / NVIDIA RTX 3090"
    /// </summary>
    private static (string integrated, string dedicated) SplitVgaIntegratedDedicated(string vgaInfo)
    {
        if (string.IsNullOrWhiteSpace(vgaInfo)) return (string.Empty, string.Empty);

        // Split by / or , atau " + "
        var separators = new[] { '/', '+', ';', '\n' };
        var parts = vgaInfo.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return (string.Empty, string.Empty);

        string integrated = string.Empty, dedicated = string.Empty;
        foreach (var p in parts)
        {
            bool isIntel = p.Contains("Intel", StringComparison.OrdinalIgnoreCase);
            bool isAmdIntegrated = p.Contains("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase)
                                  || p.Contains("Radeon Graphics", StringComparison.OrdinalIgnoreCase);

            if ((isIntel || isAmdIntegrated) && string.IsNullOrEmpty(integrated))
            {
                integrated = p;
            }
            else if (string.IsNullOrEmpty(dedicated))
            {
                dedicated = p;
            }
        }

        return (integrated, dedicated);
    }

    // ===== NEW: PO Units Lookup =====
    private async Task LookupPoUnitsAsync()
    {
        if (string.IsNullOrWhiteSpace(PoNumber))
        {
            _feedback.ShowWarning("Masukkan No PO terlebih dahulu.");
            return;
        }

        try
        {
            IsLookingUpPo = true;
            ErpSyncStatus = $"Mencari unit pada PO '{PoNumber.Trim()}' di ERP...";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.Yellow;
            PoUnits.Clear();

            // Tentukan brand preferen dari manufacturer laptop yang sedang dites
            string preferredBrand = !string.IsNullOrWhiteSpace(_cachedSystemManufacturer)
                && _cachedSystemManufacturer.Contains("Lenovo", StringComparison.OrdinalIgnoreCase)
                ? "Lenovo"
                : "Lenovo"; // default Lenovo first

            var result = await _erpSyncService.LookupPoUnitsAsync(PoNumber.Trim(), preferredBrand);
            if (result != null && result.Success && result.Data != null && result.Data.Units.Count > 0)
            {
                foreach (var u in result.Data.Units) PoUnits.Add(u);
                HasPoUnits = true;
                ErpSyncStatus = $"Ditemukan {PoUnits.Count} unit di PO '{PoNumber}'. Pilih unit lalu hubungkan.";
                ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                HasPoUnits = false;
                string msg = result?.Message ?? "Tidak ada unit ditemukan";
                ErpSyncStatus = msg;
                ErpSyncStatusColorBrush = System.Windows.Media.Brushes.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            ErpSyncStatus = $"Error: {ex.Message}";
            ErpSyncStatusColorBrush = System.Windows.Media.Brushes.Red;
            _logger.Error("Error looking up PO units", ex);
        }
        finally
        {
            IsLookingUpPo = false;
        }
    }

    // ===== NEW: Refresh Testers =====
    private async Task RefreshTestersAsync()
    {
        try
        {
            var result = await _erpSyncService.GetTestersAsync();
            Testers.Clear();
            if (result != null && result.Success && result.Data != null)
            {
                foreach (var t in result.Data) Testers.Add(t);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error refreshing testers", ex);
        }
    }

    // ===== NEW: Register Tester =====
    private async Task RegisterTesterAsync()
    {
        if (string.IsNullOrWhiteSpace(TesterName))
        {
            _feedback.ShowWarning("Isi nama tester terlebih dahulu.");
            return;
        }

        try
        {
            IsRegisteringTester = true;
            var result = await _erpSyncService.RegisterTesterAsync(TesterName.Trim());
            if (result != null && result.Success)
            {
                _audio.PlayAction();
                _feedback.ShowInfo($"Tester '{TesterName.Trim()}' berhasil terdaftar!");
                await RefreshTestersAsync();
                // Auto-select newly created tester (or existing matching)
                var match = Testers.FirstOrDefault(t => t.Name.Equals(TesterName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) SelectedTester = match;
                TesterName = string.Empty;
            }
            else
            {
                _feedback.ShowError($"Gagal register tester: {result?.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error registering tester", ex);
            _feedback.ShowError(ex.Message);
        }
        finally
        {
            IsRegisteringTester = false;
        }
    }

    // ===== NEW: Connect to selected unit =====
    private async Task ConnectAsync()
    {
        if (SelectedUnit == null)
        {
            _feedback.ShowWarning("Pilih unit dari daftar terlebih dahulu.");
            return;
        }

        if (SelectedTester == null)
        {
            _feedback.ShowWarning("Pilih tester terlebih dahulu sebelum Hubungkan.");
            return;
        }

        await Task.Delay(0);
        IsConnected = true;
        _audio.PlayAction();
        ErpSyncStatus = $"Terhubung ke {SelectedUnit.InternalId} ({SelectedUnit.Brand})\nTester: {SelectedTester.Name}\nSekarang hasil QC akan otomatis terikat ke unit ini.";
        ErpSyncStatusColorBrush = System.Windows.Media.Brushes.LightGreen;
        _feedback.ShowInfo($"Berhasil terhubung ke unit {SelectedUnit.InternalId}.\nSekarang hasil QC akan terikat ke unit ini di ERP.");
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);
}

public sealed record ReportCard(string Title, string Status, string[] IconFiles);

public sealed partial class QcChecklistItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string infoText = string.Empty;

    [ObservableProperty]
    private string statusText = "-";

    [ObservableProperty]
    private bool isChecked;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string iconPath = string.Empty;

    /// <summary>
    /// Result status: "PASS" (green), "SKIP" (neutral), "FAIL" (red), or "" (none).
    /// </summary>
    [ObservableProperty]
    private string resultStatus = string.Empty;

    /// <summary>
    /// Note entered by user when test result is FAIL.
    /// </summary>
    [ObservableProperty]
    private string resultNote = string.Empty;

    /// <summary>
    /// Result label shown in the colored box next to Run button.
    /// "OK", "-", "X", or "".
    /// </summary>
    [ObservableProperty]
    private string resultLabel = string.Empty;

    /// <summary>
    /// Background color hex for the result box.
    /// Green #1E7A4B, Red #B91C1C, Neutral #44444444, or transparent.
    /// </summary>
    [ObservableProperty]
    private string resultColor = "Transparent";

    /// <summary>
    /// Whether a result has been recorded (for showing the result box).
    /// </summary>
    [ObservableProperty]
    private bool hasResult;

    public QcChecklistKind Kind { get; init; }

    public IAsyncRelayCommand RunCommand { get; set; } = null!;

    /// <summary>
    /// Display title of the test, used for ERP sync payloads and reports.
    /// Maps to <see cref="Name"/>.
    /// </summary>
    public string Title => Name;

    /// <summary>
    /// Subtitle / detail line for the test, used for ERP sync payloads.
    /// Returns the current info text (state after the test ran).
    /// </summary>
    public string Subtitle => InfoText;

    /// <summary>
    /// Sets the result from a QcResultPopupWindow.QcResult value.
    /// Updates ResultLabel, ResultColor, ResultStatus, and HasResult.
    /// </summary>
    public void SetResult(Views.Windows.QcResultPopupWindow.QcResult result, string note = "")
    {
        ResultNote = note;
        switch (result)
        {
            case Views.Windows.QcResultPopupWindow.QcResult.Pass:
                ResultLabel = "OK";
                ResultColor = "#FF1E7A4B";
                ResultStatus = "PASS";
                HasResult = true;
                break;
            case Views.Windows.QcResultPopupWindow.QcResult.Skip:
                ResultLabel = "-";
                ResultColor = "#66444444";
                ResultStatus = "SKIP";
                HasResult = true;
                break;
            case Views.Windows.QcResultPopupWindow.QcResult.Fail:
                ResultLabel = "X";
                ResultColor = "#FFB91C1C";
                ResultStatus = "FAIL";
                HasResult = true;
                break;
            default:
                ResultLabel = string.Empty;
                ResultColor = "Transparent";
                ResultStatus = string.Empty;
                HasResult = false;
                break;
        }
    }

    /// <summary>
    /// Sets a percentage-based result with color thresholds.
    /// 0-50%: red (Fail), 60-70%: yellow (Skip), 70-100%: green (Pass).
    /// </summary>
    public void SetPercentageResult(int percent)
    {
        ResultLabel = $"{percent}%";
        if (percent <= 50)
        {
            ResultColor = "#FFB91C1C";
            ResultStatus = "FAIL";
        }
        else if (percent <= 70)
        {
            ResultColor = "#FFE0A800";
            ResultStatus = "SKIP";
        }
        else
        {
            ResultColor = "#FF1E7A4B";
            ResultStatus = "PASS";
        }
        HasResult = true;
    }
}

public sealed class BenchmarkOption
{
    public string Name { get; init; } = string.Empty;

    public string FolderPath { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);

    public override string ToString() => Name;
}

public enum QcChecklistKind
{
    LcdTest,
    StorageHealth,
    BatteryHealth,
    BatteryTest,
    KeyboardTest,
    AudioTest,
    Benchmark,
    RunningTest,
    Camera,
    TouchscreenTest,
    PortTest,
    Touchpad,
    Wifi,
    Backlight,
    Mic
}

public readonly record struct PortCheckResult(bool UsbConnected, bool AudioJackConnected, bool ChargerConnected)
{
    public bool AllOk => UsbConnected && AudioJackConnected && ChargerConnected;

    public string ToDetailText()
    {
        return $"USB={(UsbConnected ? "OK" : "CHECK")}, Jack Audio={(AudioJackConnected ? "OK" : "CHECK")}, Charger={(ChargerConnected ? "OK" : "CHECK")}";
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SystemPowerStatus
{
    public byte ACLineStatus;
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public int BatteryLifeTime;
    public int BatteryFullLifeTime;
}