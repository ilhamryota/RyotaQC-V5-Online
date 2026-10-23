using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RyotaQC.App.Helpers;

/// <summary>
/// Membuat seluruh window popup (WindowStyle=None + AllowsTransparency) bisa digeser
/// (drag) dengan klik-kiri pada area non-interaktif, sekaligus membatasi ukuran dan
/// posisinya agar tidak terpotong / keluar layar pada resolusi &amp; aspect ratio apa pun.
///
/// Aktifkan sekali di startup aplikasi melalui <see cref="EnableForPopupWindows"/>.
/// </summary>
public static class WindowDragBehavior
{
    private const double WorkAreaMargin = 24d;

    private static readonly HashSet<Window> Attached = new();
    private static bool _registered;
    private static bool _isDragging;

    /// <summary>
    /// Pasang perilaku drag + clamp otomatis ke semua window popup aplikasi.
    /// Panggil sekali saat startup (lihat App.xaml.cs).
    /// </summary>
    public static void EnableForPopupWindows()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnAnyWindowLoaded));
    }

    private static void OnAnyWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        // Jangan pasang ke jendela utama (punya title bar sendiri).
        if (window is RyotaQC.App.MainWindow)
        {
            return;
        }

        // Hanya window popup (tanpa chrome bawaan).
        if (window.WindowStyle != WindowStyle.None)
        {
            return;
        }

        Attach(window);
    }

    private static void Attach(Window window)
    {
        if (!Attached.Add(window))
        {
            return;
        }

        ClampSize(window);
        ClampPosition(window);

        window.MouseLeftButtonDown += Window_MouseLeftButtonDown;
        window.LocationChanged += (_, _) =>
        {
            // Saat sedang di-drag, jangan clamp supaya gerakan tidak melawan user.
            if (!_isDragging)
            {
                ClampPosition(window);
            }
        };
        window.SizeChanged += (_, _) =>
        {
            ClampSize(window);
            ClampPosition(window);
        };
        window.Closed += (_, _) => Attached.Remove(window);
    }

    private static void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        // Jangan drag ketika user mengklik kontrol interaktif (tombol, input, dsb).
        if (e.OriginalSource is DependencyObject source && IsInteractive(source))
        {
            return;
        }

        try
        {
            _isDragging = true;
            window.DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove melempar exception bila tombol sudah dilepas; abaikan.
        }
        finally
        {
            _isDragging = false;
        }

        ClampPosition(window);
    }

    /// <summary>Batasi ukuran maksimum window ke area kerja layar aktif.</summary>
    private static void ClampSize(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        var maxHeight = Math.Max(240d, workArea.Height - WorkAreaMargin);
        var maxWidth = Math.Max(320d, workArea.Width - WorkAreaMargin);

        if (double.IsNaN(window.MaxHeight) || window.MaxHeight > maxHeight)
        {
            window.MaxHeight = maxHeight;
        }

        if (double.IsNaN(window.MaxWidth) || window.MaxWidth > maxWidth)
        {
            window.MaxWidth = maxWidth;
        }
    }

    /// <summary>
    /// Jaga window tetap berada di dalam area kerja agar tidak "turun ke bawah"
    /// atau keluar layar setelah digeser.
    /// </summary>
    private static void ClampPosition(Window window)
    {
        var workArea = SystemParameters.WorkArea;
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return;
        }

        var left = window.Left;
        var top = window.Top;

        var maxLeft = workArea.Right - width;
        var maxTop = workArea.Bottom - height;

        if (left < workArea.Left) left = workArea.Left;
        if (top < workArea.Top) top = workArea.Top;
        if (left > maxLeft) left = Math.Max(workArea.Left, maxLeft);
        if (top > maxTop) top = Math.Max(workArea.Top, maxTop);

        if (Math.Abs(left - window.Left) > 0.5)
        {
            window.Left = left;
        }

        if (Math.Abs(top - window.Top) > 0.5)
        {
            window.Top = top;
        }
    }

    private static bool IsInteractive(DependencyObject source)
    {
        var current = source;

        while (current is not null)
        {
            if (current is ButtonBase or TextBoxBase or ComboBox or ScrollBar or Slider
                or PasswordBox or ListBox or ListBoxItem or CheckBox or RadioButton)
            {
                return true;
            }

            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }
}
