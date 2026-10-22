using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// Reusable popup dialog for QC test results. Shows three options:
/// OK (green), Optional (neutral), Fail (red). Returns the selected result.
/// Used after each QC test tool is closed to capture pass/fail/skip.
///
/// Keyboard shortcuts (per QC workflow):
///   Enter -> OK / Pass
///   Space -> - Optional
///   Esc   -> X Fail
/// </summary>
public partial class QcResultPopupWindow : Window
{
    public enum QcResult
    {
        None,
        Pass,    // OK (green)
        Skip,    // - Optional (neutral)
        Fail     // X Fail (red)
    }

    public QcResult Result { get; private set; } = QcResult.None;

    public QcResultPopupWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        // Audio: result popup appeared (auto action cue).
        Services.Sfx.Action();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Make sure key presses reach the window handler even when no button
        // has keyboard focus yet.
        Focus();
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                Select(QcResult.Pass);
                break;
            case Key.Space:
                e.Handled = true;
                Select(QcResult.Skip);
                break;
            case Key.Escape:
                e.Handled = true;
                Select(QcResult.Fail);
                break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Select(QcResult.Pass);

    private void Skip_Click(object sender, RoutedEventArgs e) => Select(QcResult.Skip);

    private void Fail_Click(object sender, RoutedEventArgs e) => Select(QcResult.Fail);

    private void Select(QcResult result)
    {
        if (!IsVisible)
        {
            return;
        }

        if (result == QcResult.Fail)
        {
            Services.Sfx.Close();
        }
        else
        {
            Services.Sfx.Launch();
        }

        Result = result;
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Shows the popup and waits for the user to select a result.
    /// Returns Pass, Skip, or Fail.
    /// </summary>
    public static QcResult Show(string title, string message)
    {
        var popup = new QcResultPopupWindow(title, message);

        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsVisible && w.IsActive)
            ?? Application.Current?.MainWindow;

        if (owner is not null && owner.IsVisible)
        {
            popup.Owner = owner;
        }

        popup.ShowDialog();
        return popup.Result;
    }
}
