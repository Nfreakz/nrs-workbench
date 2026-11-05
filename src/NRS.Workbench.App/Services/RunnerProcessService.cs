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
        var expectedListener = Normalize(Path.Combine(runnerFolder, "bin", "Runner.Listener.exe"));
        var expectedWorker = Normalize(Path.Combine(runnerFolder, "bin", "Runner.Worker.exe"));
        Process? listener = null;
        var workers = new List<Process>();
        long ram = 0;
        DateTimeOffset? startedAt = null;
        var unknown = false;

        foreach (var process in Process.GetProcessesByName("Runner.Listener"))
        {
            var belongs = BelongsTo(process, expectedListener);
            if (belongs is null) unknown = true;
            if (belongs != true) { process.Dispose(); continue; }
            listener = process;
            try { ram += process.WorkingSet64; } catch { }
            try { startedAt = process.StartTime; } catch { }
            break;
        }

        foreach (var process in Process.GetProcessesByName("Runner.Worker"))
        {
            var belongs = BelongsTo(process, expectedWorker);
            if (belongs is null) unknown = true;
            if (belongs != true) { process.Dispose(); continue; }
            workers.Add(process);
            try { ram += process.WorkingSet64; } catch { }
        }

        return new RunnerProcessSnapshot(listener, workers, ram, startedAt, unknown);
    }

    // null = path could not be determined (e.g. a service runner under another account).
    private static bool? BelongsTo(Process process, string expectedExecutable)
    {
        var actual = GetImagePath(process);
        if (string.IsNullOrWhiteSpace(actual)) return null;
        return string.Equals(Normalize(actual), expectedExecutable, StringComparison.OrdinalIgnoreCase);
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
