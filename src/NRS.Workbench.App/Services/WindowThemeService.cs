using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NRS.Workbench.App.Services;

public static class WindowThemeService
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyDarkTitleBar(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            var enabled = 1;
            var result = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            if (result != 0)
                _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
        catch
        {
            // Cosmetic only. Never prevent the manager from starting because DWM
            // dark-title support differs between Windows builds.
        }
    }

    /// <summary>
    /// Clamps the window to the current work area so that dialogs stay reachable on
    /// small or high-DPI-scaled displays (e.g. 1366x768 at 150%).
    /// </summary>
    public static void FitToWorkArea(Window window)
    {
        try
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 24d;
            var availableWidth = Math.Max(480d, workArea.Width - margin);
            var availableHeight = Math.Max(400d, workArea.Height - margin);

            window.MaxWidth = availableWidth;
            window.MaxHeight = availableHeight;
            window.MinWidth = Math.Min(window.MinWidth, availableWidth);
            window.MinHeight = Math.Min(window.MinHeight, availableHeight);
            if (!double.IsNaN(window.Width)) window.Width = Math.Min(window.Width, availableWidth);
            if (!double.IsNaN(window.Height)) window.Height = Math.Min(window.Height, availableHeight);
        }
        catch
        {
            // Layout convenience only; never block a window from opening.
        }
    }
}
