namespace RyotaQC.App.Services;

public sealed class UiFeedbackService : IUiFeedbackService
{
    public event Action<bool, string>? LoadingStateChanged;

    public event Action<string, bool>? ToastRequested;

    public void ShowLoading(string message)
    {
        LoadingStateChanged?.Invoke(true, message);
    }

    public void HideLoading()
    {
        LoadingStateChanged?.Invoke(false, string.Empty);
    }

    public void ShowToast(string message, bool isSuccess)
    {
        ToastRequested?.Invoke(message, isSuccess);
    }

    public void ShowWarning(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "RyotaQC V5",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    public void ShowInfo(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "RyotaQC V5",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "RyotaQC V5",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }
}
