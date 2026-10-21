namespace RyotaQC.App.Services;

/// <summary>
/// Plays short UI sound effects (MP3 files from the tools/ folder) to give
/// the user audio feedback when navigating menus, running tests, etc.
/// </summary>
public interface IAudioFeedbackService
{
    /// <summary>Played when entering a menu (click tile or Enter).</summary>
    void PlayEnter();

    /// <summary>Played when going back / returning to the main menu.</summary>
    void PlayExit();

    /// <summary>Played when sliding left/right between menu tiles.</summary>
    void PlayNavigate();

    /// <summary>Played when a QC test finishes and shows the result popup.</summary>
    void PlayAction();

    /// <summary>
    /// Played when a QC test/tool is launched (button click, auto-play action).
    /// Uses the same short "action" cue as <see cref="PlayAction"/>.
    /// </summary>
    void PlayLaunch();

    /// <summary>
    /// Played when a QC tool / window is closed or the user quits (Esc / stop).
    /// Uses the "out" cue.
    /// </summary>
    void PlayClose();
}
