using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace RyotaQC.App.Services;

/// <summary>
/// Plays UI sound effects from &lt;workspaceRoot&gt;/tools/ using the
/// WPF MediaPlayer. Playback is fire-and-forget so audio feedback never
/// blocks or crashes the app.
///
/// Navigation cues are "restartable": if the user rapidly slides through
/// menu items, each new cue restarts from position 0 on a pooled player
/// instead of queueing behind the previous cue. This makes left/right
/// navigation feel snappy and smooth (play → slide → play → slide ...)
/// rather than one long continuous sound.
/// </summary>
public sealed class AudioFeedbackService : IAudioFeedbackService, IDisposable
{
    private readonly string _toolsRoot;
    private readonly List<MediaPlayer> _transientPlayers = [];
    private readonly object _sync = new();
    private bool _disposed;

    public AudioFeedbackService(IAppLogger logger)
    {
        _toolsRoot = ResolveToolsRoot();
        Logger = logger;
    }

    private IAppLogger Logger { get; }

    public void PlayEnter() => Play("In.mp3");

    public void PlayExit() => Play("Out.mp3");

    public void PlayNavigate() => Play("Left Right.mp3");

    public void PlayAction() => Play("Action.mp3");

    public void PlayLaunch() => Play("Action.mp3");

    public void PlayClose() => Play("Out.mp3");

    /// <summary>
    /// Plays a sound effect. Every call starts a fresh cue from position 0 so
    /// spamming left/right navigation (or any action) responds instantly with a
    /// new sound rather than continuing the previous one.
    /// </summary>
    private void Play(string fileName)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_toolsRoot, fileName);
            if (!File.Exists(path))
            {
                Logger.Info($"AudioFeedback: file not found: {path}");
                return;
            }

            var player = new MediaPlayer();
            player.Open(new Uri(path, UriKind.Absolute));
            player.Volume = 1.0;
            player.Position = TimeSpan.Zero;
            player.Play();

            // Keep a short-lived reference so the player is not collected
            // before the cue has a chance to play, then release it.
            TrackTransientPlayer(player);
        }
        catch (Exception ex)
        {
            // Audio is purely advisory. Never throw.
            Logger.Error($"AudioFeedback: failed to play {fileName}.", ex);
        }
    }

    /// <summary>
    /// Holds transient players until their media finishes opening/playing,
    /// then disposes them so a spam of cues does not leak MediaPlayer objects.
    /// </summary>
    private void TrackTransientPlayer(MediaPlayer player)
    {
        lock (_sync)
        {
            _transientPlayers.Add(player);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // Media cues are short (< 1s). Give each one enough time to
                // finish, then close and release it.
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                lock (_sync)
                {
                    _transientPlayers.Remove(player);
                }

                try
                {
                    player.Stop();
                    player.Close();
                }
                catch
                {
                    // Ignore teardown failures.
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        List<MediaPlayer> toRelease;
        lock (_sync)
        {
            toRelease = [.. _transientPlayers];
            _transientPlayers.Clear();
        }

        foreach (var player in toRelease)
        {
            try
            {
                player.Stop();
                player.Close();
            }
            catch
            {
                // Ignore teardown failures.
            }
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
