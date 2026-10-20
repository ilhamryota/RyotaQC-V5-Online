using System;
using System.IO;
using System.Windows.Media;

namespace RyotaQC.App.Services;

/// <summary>
/// Plays UI sound effects from &lt;workspaceRoot&gt;/tools/ using the
/// WPF MediaPlayer. The player is fire-and-forget — sounds are non-blocking
/// and any failure (missing file, playback error) is silently swallowed so
/// that audio feedback never crashes the app.
/// </summary>
public sealed class AudioFeedbackService : IAudioFeedbackService
{
    private readonly string _toolsRoot;

    public AudioFeedbackService(IAppLogger logger)
    {
        _toolsRoot = ResolveToolsRoot();
        Logger = logger;
    }

    private IAppLogger Logger { get; }

    public void PlayEnter()
    {
        Play("In.mp3");
    }

    public void PlayExit()
    {
        Play("Out.mp3");
    }

    public void PlayNavigate()
    {
        Play("Left Right.mp3");
    }

    public void PlayAction()
    {
        Play("Action.mp3");
    }

    private void Play(string fileName)
    {
        try
        {
            var path = Path.Combine(_toolsRoot, fileName);
            if (!File.Exists(path))
            {
                Logger.Info($"AudioFeedback: file not found: {path}");
                return;
            }

            var uri = new Uri(path, UriKind.Absolute);
            var player = new MediaPlayer();
            player.Open(uri);
            player.Play();

            // Drop the reference once playback finishes — MediaPlayer keeps
            // playing after we stop referencing it as long as the media clock
            // is running. We just let the GC clean it up later.
        }
        catch (Exception ex)
        {
            // Audio is purely advisory. Never throw.
            Logger.Error($"AudioFeedback: failed to play {fileName}.", ex);
        }
    }

    private static string ResolveToolsRoot()
    {
        try
        {
            return WorkspacePathService.FindToolsRoot();
        }
        catch
        {
            // Fall back to a relative path; playback will just no-op if the
            // file is not found.
            return Path.Combine(AppContext.BaseDirectory, "tools");
        }
    }
}