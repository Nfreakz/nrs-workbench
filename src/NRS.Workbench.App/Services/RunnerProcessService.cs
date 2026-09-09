using System.Diagnostics;

namespace NRS.Workbench.App.Services;

public sealed record RunnerProcessSnapshot(
    Process? Listener,
    IReadOnlyList<Process> Workers,
    long RamBytes,
    DateTimeOffset? StartedAt)
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

        foreach (var process in Process.GetProcessesByName("Runner.Listener"))
        {
            if (!BelongsTo(process, expectedListener)) { process.Dispose(); continue; }
            listener = process;
            try { ram += process.WorkingSet64; } catch { }
            try { startedAt = process.StartTime; } catch { }
            break;
        }

        foreach (var process in Process.GetProcessesByName("Runner.Worker"))
        {
            if (!BelongsTo(process, expectedWorker)) { process.Dispose(); continue; }
            workers.Add(process);
            try { ram += process.WorkingSet64; } catch { }
        }

        return new RunnerProcessSnapshot(listener, workers, ram, startedAt);
    }

    private static bool BelongsTo(Process process, string expectedExecutable)
    {
        try
        {
            var actual = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(actual) &&
                   string.Equals(Normalize(actual), expectedExecutable, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    }
}
