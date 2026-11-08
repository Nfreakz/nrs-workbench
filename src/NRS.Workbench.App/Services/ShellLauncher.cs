using System.Diagnostics;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Opens files, folders and URLs through the Windows shell without letting a
/// missing handler or a malformed target crash the UI thread.
/// </summary>
public static class ShellLauncher
{
    public static bool TryOpen(string target, string? workingDir = null)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        try
        {
            var info = new ProcessStartInfo(target) { UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(workingDir)) info.WorkingDirectory = workingDir;
            Process.Start(info);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not open '{target}' through the shell", ex);
            return false;
        }
    }
}
