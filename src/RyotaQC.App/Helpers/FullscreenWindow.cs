using System.Windows;

namespace RyotaQC.App.Helpers;

/// <summary>
/// Memastikan window tes (LCD / Touchscreen / Touchpad) benar-benar tampil
/// full screen: menutupi seluruh layar termasuk taskbar, pada resolusi dan
/// aspect ratio apa pun.
///
/// Dipakai bersama <see cref="WindowDragBehavior"/> yang otomatis melewatkan
/// window full screen, sehingga ukuran dan posisinya tidak pernah diubah.
/// </summary>
public static class FullscreenWindow
{
    /// <summary>
    /// Pasang mode full screen pada window tes. Aman dipanggil dari constructor
    /// window: penyesuaian dilakukan setelah window selesai dimuat.
    /// </summary>
    public static void Apply(Window window)
    {
        window.Loaded += (_, _) => EnsureFullscreen(window);

        if (window.IsLoaded)
        {
            EnsureFullscreen(window);
        }
    }

    private static void EnsureFullscreen(Window window)
    {
        // Lepaskan batas ukuran apa pun supaya window bisa menutupi seluruh layar.
        window.MaxWidth = double.PositiveInfinity;
        window.MaxHeight = double.PositiveInfinity;

        if (window.WindowState != WindowState.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        window.Activate();
        window.Focus();
    }
}
