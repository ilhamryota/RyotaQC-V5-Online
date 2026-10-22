using System.Windows;
using System.Windows.Controls;
using RyotaQC.App.ViewModels.Pages;
using RyotaQC.App.Views.Windows;

namespace RyotaQC.App.Views.Pages;

public partial class QualityControlPage : Page
{
    private readonly QualityControlPageViewModel _viewModel;
    private bool _gateOpen;

    public QualityControlPage(QualityControlPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Unloaded += (_, _) => _gateOpen = false;
    }

    /// <summary>
    /// Shows the "Hubungkan ke ERP" popup EVERY time the Quality Control menu
    /// is entered. The page is a singleton, but it is detached when the user
    /// navigates away, so Loaded fires again on each re-entry.
    ///
    /// The popup always offers "Tanpa ERP", so this never blocks the QC menu.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire more than once for the same visual tree attachment
        // (e.g. when the page template is re-applied). Guard so the user never
        // gets two stacked ERP popups.
        if (_gateOpen)
        {
            return;
        }

        _gateOpen = true;

        // Defer until the page is fully rendered so the modal popup has an owner.
        Dispatcher.BeginInvoke(new System.Action(ShowErpSyncGate), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ShowErpSyncGate()
    {
        try
        {
            var owner = Window.GetWindow(this);
            var current = _viewModel.CurrentErpConnection;
            var result = QcErpSyncWindow.Show(_viewModel.ErpSyncService, owner, current);
            _viewModel.ApplyErpConnection(result);
        }
        catch
        {
            // The gate is best-effort; never block the QC menu on a popup error.
        }
    }
}
