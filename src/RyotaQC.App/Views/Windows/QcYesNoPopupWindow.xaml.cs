using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace RyotaQC.App.Views.Windows;

/// <summary>
/// Small themed Yes/No confirmation popup used by QC flows (for example the
/// "Apakah unit ini memiliki Touchscreen?" question).
///
/// Shortcuts: Enter = Ya, Esc = Tidak.
/// </summary>
public partial class QcYesNoPopupWindow : Window
{
    public bool Result { get; private set; }

    public QcYesNoPopupWindow(string title, string message, string yesText = "Ya", string noText = "Tidak")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        YesButton.Tag = yesText;
        NoButton.Tag = noText;
        Services.Sfx.Action();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                Yes_Click(sender, e);
                break;
            case Key.Escape:
                e.Handled = true;
                No_Click(sender, e);
                break;
        }
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Launch();
        Result = true;
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Services.Sfx.Close();
        Result = false;
        DialogResult = true;
        Close();
    }

    public static bool Show(string title, string message, string yesText = "Ya", string noText = "Tidak")
    {
        var popup = new QcYesNoPopupWindow(title, message, yesText, noText);

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
