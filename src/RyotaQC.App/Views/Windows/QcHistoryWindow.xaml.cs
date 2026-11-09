using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RyotaQC.App.Services;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// QC-LOG (20261106): popup yang menampilkan riwayat hasil QC sebuah unit
/// (maksimal 7 log terbaru) yang tersimpan di ERP.
///
/// Dipanggil dari QcErpSyncWindow lewat tombol "Log Hasil QC".
/// Setiap baris log bisa diklik untuk membuka detail (tester, status, sumber,
/// waktu, dan catatan).
/// </summary>
public partial class QcHistoryWindow : Window
{
    public QcHistoryWindow(string unitCode, IEnumerable<QcHistoryEntry> entries)
    {
        InitializeComponent();

        UnitText.Text = string.IsNullOrWhiteSpace(unitCode)
            ? string.Empty
            : $"Unit: {unitCode}";

        Populate(entries?.ToList() ?? new List<QcHistoryEntry>());

        // Audio cue: results/history popup appeared.
        Services.Sfx.Action();
    }

    private void Populate(List<QcHistoryEntry> entries)
    {
        HistoryList.Children.Clear();

        SummaryText.Text = entries.Count == 0
            ? "Belum ada log untuk unit ini."
            : $"{entries.Count} log terbaru (maks 7) — #1 adalah yang paling baru.";

        if (entries.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "Log akan otomatis tersimpan setiap kali hasil QC disimpan " +
                       "(dari web kanban ERP maupun dari aplikasi RyotaQC).",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x9D, 0xB5)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6, 2, 6)
            });
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var index = i + 1;
            HistoryList.Children.Add(BuildEntry(entry, index));
        }
    }

    private UIElement BuildEntry(QcHistoryEntry entry, int index)
    {
        var outer = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x15, 0x1E, 0x35)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x47, 0x7C, 0xFF)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();

        // ---- Header row (clickable to expand) ----
        var header = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 9, 12, 9)
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
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(idxText, 0);
        headerGrid.Children.Add(idxText);

        // Status badge
        var (badgeBg, badgeFg) = StatusColors(entry.StatusLabel);
        var badge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = badgeBg,
            Padding = new Thickness(9, 3, 9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        badge.Child = new TextBlock
        {
            Text = entry.StatusLabel,
            Foreground = badgeFg,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(badge, 1);
        headerGrid.Children.Add(badge);

        // Source badge
        var sourceBadge = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4C, 0x8C, 0xE4)),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        sourceBadge.Child = new TextBlock
        {
            Text = entry.SourceLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0xC1, 0xF7)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(sourceBadge, 2);
        headerGrid.Children.Add(sourceBadge);

        // Tester
        var testerText = new TextBlock
        {
            Text = entry.TesterLabel,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDB)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12.5,
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
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(timeText, 4);
        headerGrid.Children.Add(timeText);

        header.Content = headerGrid;

        // ---- Detail (collapsed by default) ----
        var detail = new Border
        {
            Visibility = Visibility.Collapsed,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x47, 0x7C, 0xFF)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 10, 12, 12),
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0x0B, 0x12, 0x22))
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
                Text = "Catatan",
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 2)
            });
            detailStack.Children.Add(new TextBlock
            {
                Text = entry.Notes,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC3, 0xCC, 0xDB)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x7C, 0x94)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12
        };
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE1, 0xE7, 0xF0)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return grid;
    }

    private static (Brush bg, Brush fg) StatusColors(string status)
    {
        var up = (status ?? string.Empty).ToUpperInvariant();

        if (up.Contains("SELESAI") || up.Contains("READY") || up.Contains("PASS"))
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
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
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
    public static void Show(string unitCode, IEnumerable<QcHistoryEntry> entries)
    {
        var popup = new QcHistoryWindow(unitCode, entries);

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
