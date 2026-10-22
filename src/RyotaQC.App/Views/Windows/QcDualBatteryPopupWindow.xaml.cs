using System.Windows;
using System.Windows.Input;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// Popup that captures Battery Health 1 (mandatory) and Battery Health 2
/// (optional, only when the unit has a second battery). Used by the Battery
/// Health QC test before the verdict popup.
/// </summary>
public partial class QcDualBatteryPopupWindow : Window
{
    /// <summary>Battery Health 1 (-1 when cancelled).</summary>
    public int Health1 { get; private set; } = -1;

    /// <summary>Battery Health 2 (0 when not provided).</summary>
    public int Health2 { get; private set; }

    public QcDualBatteryPopupWindow(
        string title = "Battery Health",
        string question = "Masukkan nilai Battery Health dari battery report:",
        int? prefillHealth1 = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        QuestionText.Text = question;

        if (prefillHealth1.HasValue && prefillHealth1.Value > 0)
        {
            Health1Box.Text = prefillHealth1.Value.ToString();
        }

        Health1Box.Focus();
        Services.Sfx.Action();
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Health1Box.Text, out var h1) || h1 <= 0)
        {
            HintText.Text = "Battery Health 1 wajib diisi dengan angka (contoh: 100).";
            HintText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            Health1Box.Focus();
            return;
        }

        Health1 = h1;
        Health2 = int.TryParse(Health2Box.Text, out var h2) && h2 > 0 ? h2 : 0;

        Services.Sfx.Launch();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Close();
        Health1 = -1;
        Health2 = 0;
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Shows the dual-battery popup. Returns (-1, 0) when cancelled.
    /// </summary>
    public static (int health1, int health2) Show(
        string title = "Battery Health",
        string question = "Masukkan nilai Battery Health dari battery report:",
        int? prefillHealth1 = null)
    {
        var popup = new QcDualBatteryPopupWindow(title, question, prefillHealth1);
        if (popup.ShowDialog() == true)
        {
            return (popup.Health1, popup.Health2);
        }
        return (-1, 0);
    }
}
