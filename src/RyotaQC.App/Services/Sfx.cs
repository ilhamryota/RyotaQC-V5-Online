using Microsoft.Extensions.DependencyInjection;

namespace RyotaQC.App.Services;

/// <summary>
/// Lightweight static accessor for UI sound effects so that WPF windows
/// (popups, dialogs) can play cues without threading the service through
/// every constructor. All calls are best-effort and never throw.
/// </summary>
public static class Sfx
{
    public static void Launch() => WithService(s => s.PlayLaunch());

    public static void Close() => WithService(s => s.PlayClose());

    public static void Action() => WithService(s => s.PlayAction());

    public static void Enter() => WithService(s => s.PlayEnter());

    public static void Exit() => WithService(s => s.PlayExit());

    public static void Navigate() => WithService(s => s.PlayNavigate());

    private static void WithService(Action<IAudioFeedbackService> action)
    {
        try
        {
            var services = App.Services;
            if (services is null)
            {
                return;
            }

            var audio = services.GetService<IAudioFeedbackService>();
            if (audio is null)
            {
                return;
            }

            action(audio);
        }
        catch
        {
            // Audio is advisory only.
        }
    }
}
