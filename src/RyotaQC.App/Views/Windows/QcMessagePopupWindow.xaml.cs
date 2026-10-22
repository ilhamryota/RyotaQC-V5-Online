using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RyotaQC.App.Views.Windows;

public enum QcMessageTone
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Themed information/error popup used for QC validation results (for example
/// "GAGAL Kirim ke ERP" when mandatory fields are missing) and tester
/// registration confirmations.
///
/// Shortcuts: Enter or Esc dismisses the popup.
/// </summary>
public partial class QcMessagePopupWindow : Window
{
    public QcMessagePopupWindow(string title, string message, QcMessageTone tone = QcMessageTone.Info)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ApplyTone(tone);
        Services.Sfx.Action();
    }

    private void ApplyTone(QcMessageTone tone)
    {
        var (accent, icon, glyph) = tone switch
        {
            QcMessageTone.Success => ("#FF1E7A4B", "#FF4ADE80", "\uE73E"),
            QcMessageTone.Warning => ("#FFB45309", "#FFFBBF24", "\uE7BA"),
            QcMessageTone.Error => ("#FFB91C1C", "#FFF87171", "\uEA39"),
            _ => ("#FF2563EB", "#FF60A5FA", "\uE946")
        };

        var accentBrush = Brush(accent);
        var iconBrush = Brush(icon);

        AccentBorder.BorderBrush = accentBrush;
        IconBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B));
        IconBorder.Background = new SolidColorBrush(Color.FromArgb(0x33, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B));
        IconText.Foreground = iconBrush;
        IconText.Text = glyph;
    }

    private static SolidColorBrush Brush(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    public static void Show(string title, string message, QcMessageTone tone = QcMessageTone.Info)
    {
        var popup = new QcMessagePopupWindow(title, message, tone);

        // Prefer the currently active dialog (e.g. the ERP connect popup) so
        // the message is not hidden behind it.
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
