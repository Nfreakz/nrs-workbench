using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NRS.Workbench.App.Services;

public sealed record RunnerProcessSnapshot(
    Process? Listener,
    IReadOnlyList<Process> Workers,
    long RamBytes,
    DateTimeOffset? StartedAt,
    bool Unknown = false)
{
    public bool HasListener => Listener is not null;
    public bool HasWorker => Workers.Count > 0;
}

public sealed class RunnerProcessService
{
    public RunnerProcessSnapshot GetSnapshot(string runnerFolder)
    {
        // Runner 2.338+ updates by turning "bin" into a link to "bin.<version>", and Windows
        // reports the resolved image path (…\bin.2.338.2\Runner.Listener.exe). Match on the
        // runner folder plus the file name, not on the literal "bin" path.
        var folder = Normalize(runnerFolder) + Path.DirectorySeparatorChar;
        Process? listener = null;
        var workers = new List<Process>();
        long ram = 0;
        DateTimeOffset? startedAt = null;
        var unknown = false;

        foreach (var process in Process.GetProcessesByName("Runner.Listener"))
        {
            var belongs = BelongsTo(process, folder, "Runner.Listener.exe");
            if (belongs is null) unknown = true;
            if (belongs != true) { process.Dispose(); continue; }
            listener = process;
            try { ram += process.WorkingSet64; } catch { }
            try { startedAt = process.StartTime; } catch { }
            break;
        }

        foreach (var process in Process.GetProcessesByName("Runner.Worker"))
        {
            var belongs = BelongsTo(process, folder, "Runner.Worker.exe");
            if (belongs is null) unknown = true;
            if (belongs != true) { process.Dispose(); continue; }
            workers.Add(process);
            try { ram += process.WorkingSet64; } catch { }
        }

        return new RunnerProcessSnapshot(listener, workers, ram, startedAt, unknown);
    }

    // null = path could not be determined (e.g. a service runner under another account).
    private static bool? BelongsTo(Process process, string runnerFolderWithSeparator, string fileName)
    {
        var actual = GetImagePath(process);
        if (string.IsNullOrWhiteSpace(actual)) return null;
        var path = Normalize(actual);
        if (!string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)) return false;
        if (!path.StartsWith(runnerFolderWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;
        // Only <runner>\bin\X or <runner>\bin.<version>\X, never a nested runner folder.
        var relativeDir = Path.GetDirectoryName(path.Substring(runnerFolderWithSeparator.Length)) ?? string.Empty;
        return relativeDir.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               relativeDir.StartsWith("bin.", StringComparison.OrdinalIgnoreCase) && !relativeDir.Contains(Path.DirectorySeparatorChar);
    }

    private static string? GetImagePath(Process process)
    {
        try
        {
            using var handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
            if (!handle.IsInvalid)
            {
                var buffer = new StringBuilder(1024);
                var size = buffer.Capacity;
                if (QueryFullProcessImageName(handle, 0, buffer, ref size)) return buffer.ToString(0, size);
            }
        }
        catch { }

        try { return process.MainModule?.FileName; }
        catch { return null; }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder exeName, ref int size);

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }
}
