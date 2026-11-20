using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// QC-LOG: popup yang menampilkan hasil QC lengkap sebuah unit
/// (identitas, spesifikasi hardware, pengujian fungsi, catatan, dan riwayat log maks 7).
/// </summary>
public partial class QcHistoryWindow : Window
{
    public QcHistoryWindow(string unitCode, IEnumerable<QcHistoryEntry>? entries, QcUnitDetails? unitInfo = null)
    {
        InitializeComponent();

        var entryList = entries?.ToList() ?? new List<QcHistoryEntry>();
        PopulateView(unitCode, entryList, unitInfo);

        // Audio cue
        Services.Sfx.Action();
    }

    private void PopulateView(string unitCode, List<QcHistoryEntry> entries, QcUnitDetails? info)
    {
        var latestEntry = entries.FirstOrDefault();
        JsonElement? snap = latestEntry?.Snapshot;

        // 1. Header Information
        var internalId = !string.IsNullOrWhiteSpace(info?.InternalId) ? info.InternalId : unitCode;
        UnitText.Text = $"Unit: {internalId}";

        var dest = info?.DestinationOutletName;
        if (!string.IsNullOrWhiteSpace(dest))
        {
            DestinationText.Text = $"·  Tujuan: {dest}";
            DestinationText.Visibility = Visibility.Visible;
        }
        else
        {
            DestinationText.Visibility = Visibility.Collapsed;
        }

        var overallStatus = latestEntry?.StatusLabel
                            ?? latestEntry?.OverallStatus
                            ?? (entries.Count > 0 ? entries[0].StatusLabel : string.Empty);
        if (!string.IsNullOrWhiteSpace(overallStatus) && overallStatus != "-")
        {
            StatusBadge.Visibility = Visibility.Visible;
            StatusBadgeText.Text = overallStatus.ToUpperInvariant();
            var (bg, fg) = StatusColors(overallStatus);
            StatusBadge.Background = bg;
            StatusBadgeText.Foreground = fg;
        }

        // 2. Hardware Specs
        var brand = PickVal(info?.Brand, GetSnapStr(snap, "brand"), "-");
        var model = PickVal(info?.Model, GetSnapStr(snap, "type"), "-");
        var sn = PickVal(info?.SerialNumber, GetSnapStr(snap, "serialNumber"), "-");

        var cpu = PickVal(info?.Specs?.Processor, GetSnapSpec(snap, "processor"), "-");
        var ram = PickVal(info?.Specs?.Ram, GetSnapSpec(snap, "ram"), "-");
        var ssd = PickVal(info?.Specs?.Ssd, GetSnapSpec(snap, "storageSsd", "ssd", "storage"), "-");
        var hdd = PickVal(info?.Specs?.Hdd, GetSnapSpec(snap, "storageHdd", "hdd"), "-");
        var vga = PickVal(info?.Specs?.Vga, GetSnapSpec(snap, "vga", "vgaDedicated"), "-");
        var display = PickVal(info?.Specs?.Display, GetSnapSpec(snap, "display"), "-");

        var physVal = GetNumericVal(info?.NumericTests, snap, "physicalCondition");
        var phys = physVal.HasValue ? $"{physVal.Value:0}%" : "-";

        BrandText.Text = brand;
        ModelText.Text = model;
        SnText.Text = sn;
        CpuText.Text = cpu;
        RamText.Text = ram;
        SsdText.Text = ssd;
        HddText.Text = hdd;
        VgaText.Text = vga;
        DisplayText.Text = display;
        PhysicalText.Text = phys;

        // 3. Function & Running Tests
        BuildFunctionTests(info, snap, latestEntry);

        // 4. Notes
        var notes = PickVal(info?.Notes, latestEntry?.Notes, GetSnapStr(snap, "notes"), "");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            NotesText.Text = notes;
            NotesText.Foreground = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
        }
        else
        {
            NotesText.Text = "Tidak ada catatan tambahan untuk unit ini.";
            NotesText.Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x9D, 0xB5));
        }

        // 5. History List
        BuildHistoryList(entries);
    }

    private void BuildFunctionTests(QcUnitDetails? info, JsonElement? snap, QcHistoryEntry? latest)
    {
        FunctionTestsPanel.Children.Clear();

        // Extract numeric metrics
        var hddHealth = GetNumericVal(info?.NumericTests, snap, "hddSsdHealth")
                        ?? info?.StorageHealth;
        var batHealth = GetNumericVal(info?.NumericTests, snap, "batteryHealth")
                        ?? info?.BatteryHealth;
        var batLifeMin = GetNumericVal(info?.NumericTests, snap, "batteryLifetime")
                         ?? info?.BatteryLifetimeMinutes;

        var remLife = PickVal(info?.RemainingLifetime, GetSnapStr(snap, "remainingLifetime"), "");
        var lifeWrites = PickVal(info?.LifetimeWrites, GetSnapStr(snap, "lifetimeWrites"), "");

        // Battery life string with hours
        string batLifeDesc = "-";
        if (batLifeMin.HasValue && batLifeMin.Value > 0)
        {
            var hours = batLifeMin.Value / 60.0;
            batLifeDesc = $"{batLifeMin.Value:0} Menit (~{hours:0.1} Jam running test)";
        }

        // Storage health string
        string ssdHealthDesc = hddHealth.HasValue ? $"{hddHealth.Value:0}% Health" : "-";
        string batHealthDesc = batHealth.HasValue ? $"{batHealth.Value:0}% Health" : "-";

        var testsJson = info?.Tests ?? (snap.HasValue && snap.Value.TryGetProperty("tests", out var tp) ? tp : (JsonElement?)null);

        // List of all items requested by user
        var items = new List<(string name, string status, string extra)>
        {
            ("LCD Test", GetTestStatus(testsJson, "lcdTest"), GetTestNotes(testsJson, "lcdTest")),
            ("Kinerja SSD/HDD", hddHealth.HasValue ? (hddHealth.Value >= 80 ? "PASS" : (hddHealth.Value >= 50 ? "ISSUE" : "FAIL")) : GetTestStatus(testsJson, "storageTest"), ssdHealthDesc),
            ("Remaining Lifetime", !string.IsNullOrWhiteSpace(remLife) ? "INFO" : "-", remLife),
            ("Lifetime Writes", !string.IsNullOrWhiteSpace(lifeWrites) ? "INFO" : "-", lifeWrites),
            ("Battery Test", batHealth.HasValue ? (batHealth.Value >= 70 ? "PASS" : (batHealth.Value >= 40 ? "ISSUE" : "FAIL")) : GetTestStatus(testsJson, "batteryTest"), batHealthDesc),
            ("Battery Life Time", batLifeMin.HasValue ? "INFO" : "-", batLifeDesc),
            ("Keyboard", GetTestStatus(testsJson, "keyboard"), GetTestNotes(testsJson, "keyboard")),
            ("Backlight", GetTestStatus(testsJson, "backlight"), GetTestNotes(testsJson, "backlight")),
            ("Camera", GetTestStatus(testsJson, "camera"), GetTestNotes(testsJson, "camera")),
            ("Microphone", GetTestStatus(testsJson, "microphone"), GetTestNotes(testsJson, "microphone")),
            ("Fingerprint", GetTestStatus(testsJson, "fingerprint"), GetTestNotes(testsJson, "fingerprint")),
            ("Wifi / Bluetooth", GetTestStatus(testsJson, "wifiTest"), GetTestNotes(testsJson, "wifiTest")),
            ("Audio / Speaker", GetTestStatus(testsJson, "audioSpeaker"), GetTestNotes(testsJson, "audioSpeaker")),
            ("Port", GetTestStatus(testsJson, "port"), GetTestNotes(testsJson, "port")),
            ("Touchpad", GetTestStatus(testsJson, "touchpad"), GetTestNotes(testsJson, "touchpad")),
            ("Touchscreen", GetTestStatus(testsJson, "touchscreen"), GetTestNotes(testsJson, "touchscreen")),
            ("Stress Test", GetTestStatus(testsJson, "stressTest"), GetTestNotes(testsJson, "stressTest"))
        };

        foreach (var item in items)
        {
            FunctionTestsPanel.Children.Add(CreateTestBadge(item.name, item.status, item.extra));
        }
    }

    private static UIElement CreateTestBadge(string name, string status, string extra)
    {
        var border = new Border
        {
            Width = 236,
            Margin = new Thickness(4),
            Padding = new Thickness(10, 7, 10, 7),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x16, 0x24, 0x3B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x3B, 0x82, 0xF6)),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header row: Test Name + Status Badge
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.FromRgb(0xDB, 0xE4, 0xF0)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(title, 0);
        topGrid.Children.Add(title);

        var (bg, fg) = TestStatusColors(status);
        var badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = bg,
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        badge.Child = new TextBlock
        {
            Text = CleanStatusLabel(status),
            Foreground = fg,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(badge, 1);
        topGrid.Children.Add(badge);

        Grid.SetRow(topGrid, 0);
        grid.Children.Add(topGrid);

        // Extra details / notes row
        if (!string.IsNullOrWhiteSpace(extra) && extra != "-")
        {
            var extraText = new TextBlock
            {
                Text = extra,
                Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(extraText, 1);
            grid.Children.Add(extraText);
        }

        border.Child = grid;
        return border;
    }

    private void BuildHistoryList(List<QcHistoryEntry> entries)
    {
        HistoryList.Children.Clear();
        HistoryHeaderTitle.Text = $"RIWAYAT LOG QC SEBELUMNYA ({entries.Count} LOG)";
        SummaryText.Text = entries.Count == 0
            ? "Belum ada log tersimpan untuk unit ini."
            : $"{entries.Count} log QC tersimpan di ERP (maks 7 riwayat FIFO).";

        if (entries.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "Log otomatis tersimpan setiap kali hasil pengujian unit disimpan ke ERP.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(2, 4, 2, 4)
            });
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            HistoryList.Children.Add(BuildHistoryItem(entries[i], i + 1));
        }
    }

    private UIElement BuildHistoryItem(QcHistoryEntry entry, int index)
    {
        var outer = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0x1E, 0x29, 0x3B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x2A, 0x47, 0x7C, 0xFF)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var stack = new StackPanel();

        // Header button
        var header = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 8, 10, 8)
        };
        header.Template = BuildFlatButtonTemplate();

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // #index
        var idxText = new TextBlock
        {
            Text = $"#{index}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(idxText, 0);
        headerGrid.Children.Add(idxText);

        // Status badge
        var (badgeBg, badgeFg) = StatusColors(entry.StatusLabel);
        var badge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = badgeBg,
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        badge.Child = new TextBlock
        {
            Text = entry.StatusLabel,
            Foreground = badgeFg,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10.5,
            FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(badge, 1);
        headerGrid.Children.Add(badge);

        // Source badge
        var sourceBadge = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x3B, 0x82, 0xF6)),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        sourceBadge.Child = new TextBlock
        {
            Text = entry.SourceLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(sourceBadge, 2);
        headerGrid.Children.Add(sourceBadge);

        // Tester
        var testerText = new TextBlock
        {
            Text = entry.TesterLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(testerText, 3);
        headerGrid.Children.Add(testerText);

        // Time
        var timeText = new TextBlock
        {
            Text = entry.TimeLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x9D, 0xB5)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(timeText, 4);
        headerGrid.Children.Add(timeText);

        header.Content = headerGrid;

        // Collapsed detail pane
        var detail = new Border
        {
            Visibility = Visibility.Collapsed,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x3B, 0x82, 0xF6)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromArgb(0x10, 0x0B, 0x12, 0x22))
        };

        var detailStack = new StackPanel();
        detailStack.Children.Add(DetailLine("Tester", entry.TesterLabel));
        detailStack.Children.Add(DetailLine("Status Kanban", string.IsNullOrWhiteSpace(entry.QcStatusName) ? "-" : entry.QcStatusName!));
        detailStack.Children.Add(DetailLine("Hasil Akhir", string.IsNullOrWhiteSpace(entry.OverallStatus) ? "-" : entry.OverallStatus!));
        detailStack.Children.Add(DetailLine("Sumber", entry.SourceLabel));
        detailStack.Children.Add(DetailLine("Waktu", entry.TimeLabel));

        if (!string.IsNullOrWhiteSpace(entry.Notes))
        {
            detailStack.Children.Add(new TextBlock
            {
                Text = "Catatan:",
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 2)
            });
            detailStack.Children.Add(new TextBlock
            {
                Text = entry.Notes,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            });
        }

        detail.Child = detailStack;

        header.Click += (_, _) =>
        {
            detail.Visibility = detail.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        };

        stack.Children.Add(header);
        stack.Children.Add(detail);
        outer.Child = stack;
        return outer;
    }

    private static UIElement DetailLine(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11.5
        };
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE1, 0xE7, 0xF0)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return grid;
    }

    // Helper functions for Json parsing & colors
    private static string PickVal(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v) && v != "-")
            {
                return v.Trim();
            }
        }
        return "-";
    }

    private static string GetSnapStr(JsonElement? snap, string prop)
    {
        if (!snap.HasValue) return string.Empty;
        if (snap.Value.TryGetProperty(prop, out var el))
        {
            return el.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static string GetSnapSpec(JsonElement? snap, params string[] props)
    {
        if (!snap.HasValue) return string.Empty;
        if (snap.Value.TryGetProperty("specs", out var specs) && specs.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in props)
            {
                if (specs.TryGetProperty(p, out var el))
                {
                    var val = el.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }
        return string.Empty;
    }

    private static double? GetNumericVal(JsonElement? numTests, JsonElement? snap, string prop)
    {
        if (numTests.HasValue && numTests.Value.ValueKind == JsonValueKind.Object)
        {
            if (numTests.Value.TryGetProperty(prop, out var el) && el.TryGetDouble(out var d))
            {
                return d;
            }
        }
        if (snap.HasValue && snap.Value.TryGetProperty("numericTests", out var snt) && snt.ValueKind == JsonValueKind.Object)
        {
            if (snt.TryGetProperty(prop, out var el) && el.TryGetDouble(out var d))
            {
                return d;
            }
        }
        if (snap.HasValue && snap.Value.TryGetProperty(prop, out var direct) && direct.TryGetDouble(out var directD))
        {
            return directD;
        }
        return null;
    }

    private static string GetTestStatus(JsonElement? tests, string testKey)
    {
        if (!tests.HasValue || tests.Value.ValueKind != JsonValueKind.Object) return "-";
        if (tests.Value.TryGetProperty(testKey, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            if (obj.TryGetProperty("status", out var st))
            {
                var s = st.GetString();
                return string.IsNullOrWhiteSpace(s) ? "-" : s.Trim().ToUpperInvariant();
            }
        }
        return "-";
    }

    private static string GetTestNotes(JsonElement? tests, string testKey)
    {
        if (!tests.HasValue || tests.Value.ValueKind != JsonValueKind.Object) return string.Empty;
        if (tests.Value.TryGetProperty(testKey, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            if (obj.TryGetProperty("notes", out var nt))
            {
                return nt.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string CleanStatusLabel(string status)
    {
        var up = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (up == "PASS" || up == "OK") return "PASS / OK";
        if (up == "FAIL") return "FAIL";
        if (up == "ISSUE") return "ISSUE";
        if (up == "INFO") return "TERDATA";
        if (up == "-") return "-";
        return status;
    }

    private static (Brush bg, Brush fg) TestStatusColors(string status)
    {
        var up = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (up.Contains("PASS") || up.Contains("OK"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0x10, 0xB9, 0x81)),
                    new SolidColorBrush(Color.FromRgb(0x6E, 0xEE, 0xB8)));
        }
        if (up.Contains("FAIL") || up.Contains("REJECT"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0xEF, 0x44, 0x44)),
                    new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5)));
        }
        if (up.Contains("ISSUE"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0xF5, 0x9E, 0x0B)),
                    new SolidColorBrush(Color.FromRgb(0xFC, 0xD3, 0x4D)));
        }
        if (up.Contains("INFO"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x33, 0x3B, 0x82, 0xF6)),
                    new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)));
        }
        return (new SolidColorBrush(Color.FromArgb(0x22, 0x64, 0x74, 0x8B)),
                new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));
    }

    private static (Brush bg, Brush fg) StatusColors(string status)
    {
        var up = (status ?? string.Empty).ToUpperInvariant();

        if (up.Contains("SELESAI") || up.Contains("READY") || up.Contains("PASS") || up.Contains("LULUS"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0x22, 0xC5, 0x5E)),
                    new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC)));
        }

        if (up.Contains("RETUR") || up.Contains("FAIL") || up.Contains("ISSUE") || up.Contains("REJECT"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0xEF, 0x44, 0x44)),
                    new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5)));
        }

        if (up.Contains("RUNNING") || up.Contains("PROSES"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0x3B, 0x82, 0xF6)),
                    new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)));
        }

        if (up.Contains("SERVIS") || up.Contains("SERVICE"))
        {
            return (new SolidColorBrush(Color.FromArgb(0x44, 0xF5, 0x9E, 0x0B)),
                    new SolidColorBrush(Color.FromRgb(0xFC, 0xD3, 0x4D)));
        }

        return (new SolidColorBrush(Color.FromArgb(0x33, 0x94, 0xA3, 0xB8)),
                new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));
    }

    private static ControlTemplate BuildFlatButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.Name = "Bd";

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        template.VisualTree = border;

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x22, 0x47, 0x7C, 0xFF)), "Bd"));
        template.Triggers.Add(hover);

        return template;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Close();
        Close();
    }

    /// <summary>
    /// Tampilkan popup log hasil QC untuk sebuah unit.
    /// </summary>
    public static void Show(string unitCode, IEnumerable<QcHistoryEntry>? entries, QcUnitDetails? unitInfo = null)
    {
        var popup = new QcHistoryWindow(unitCode, entries, unitInfo);

        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsVisible && w.IsActive)
            ?? Application.Current?.MainWindow;

        if (owner is not null && owner.IsVisible)
        {
            popup.Owner = owner;
        }

        popup.ShowDialog();
    }
}
