using System.Windows;
using System.Windows.Controls;
using RyotaQC.App.Services;
using RyotaQC.App.ViewModels.Pages;
using RyotaQC.App.Views.Windows;

namespace RyotaQC.App.Views.Pages;

public partial class QualityControlPage : Page
{
    private readonly QualityControlPageViewModel _viewModel;
    private bool _erpGateShown;

    public QualityControlPage(QualityControlPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Show the EsteraOne ERP Sync popup ONCE, right before the Quality
        // Control menu becomes usable. After that the menu runs with the
        // selected unit bound so QC results write back to the ERP Kanban.
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_erpGateShown)
        {
            return;
        }

        _erpGateShown = true;

        // Defer until the page is fully rendered so the modal popup has an owner.
        Dispatcher.BeginInvoke(new System.Action(ShowErpSyncGate), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ShowErpSyncGate()
    {
        try
        {
            var owner = Window.GetWindow(this);
            var result = QcErpSyncWindow.Show(_viewModel.ErpSyncService, owner);
            _viewModel.ApplyErpConnection(result.unit, result.poNumber, result.connected, result.tester, result.testerName);
        }
        catch
        {
            // The gate is best-effort; never block the QC menu on a popup error.
        }
    }
}
