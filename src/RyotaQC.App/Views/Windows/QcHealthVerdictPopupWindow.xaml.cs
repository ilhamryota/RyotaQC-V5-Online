using System.Windows;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// Verdict popup shown after a Storage/Battery health value is submitted.
/// Displays the submitted percentage and lets the user choose
/// OK/Pass, - Optional, or X Fail (which then requires a failure note).
/// </summary>
public partial class QcHealthVerdictPopupWindow : Window
{
    public QcResultPopupWindow.QcResult Result { get; private set; } = QcResultPopupWindow.QcResult.None;

    public QcHealthVerdictPopupWindow(string title, string valueText, string hintText)
    {
        InitializeComponent();
        TitleText.Text = title;
        ValueText.Text = valueText;
        if (!string.IsNullOrWhiteSpace(hintText))
        {
            VerdictHintText.Text = hintText;
        }
        Services.Sfx.Action();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Launch();
        Result = QcResultPopupWindow.QcResult.Pass;
        DialogResult = true;
        Close();
    }

    private void Optional_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Launch();
        Result = QcResultPopupWindow.QcResult.Skip;
        DialogResult = true;
        Close();
    }

    private void Fail_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Close();
        Result = QcResultPopupWindow.QcResult.Fail;
        DialogResult = true;
        Close();
    }

    public static QcResultPopupWindow.QcResult Show(string title, string valueText, string hintText = "")
    {
        var popup = new QcHealthVerdictPopupWindow(title, valueText, hintText);
        popup.ShowDialog();
        return popup.Result;
    }
}
