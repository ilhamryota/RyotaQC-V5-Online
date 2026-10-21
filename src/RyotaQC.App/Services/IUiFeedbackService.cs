namespace RyotaQC.App.Services;

public interface IUiFeedbackService
{
    event Action<bool, string>? LoadingStateChanged;

    event Action<string, bool>? ToastRequested;

    void ShowLoading(string message);

    void HideLoading();

    void ShowToast(string message, bool isSuccess);

    /// <summary>Shows a warning message in a MessageBox.</summary>
    void ShowWarning(string message);

    /// <summary>Shows an info message in a MessageBox.</summary>
    void ShowInfo(string message);

    /// <summary>Shows an error message in a MessageBox.</summary>
    void ShowError(string message);
}
