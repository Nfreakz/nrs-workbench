using System.Runtime.CompilerServices;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class MaintenanceSmoke
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-maintenance-" + Guid.NewGuid().ToString("N"));
        var settings = Path.Combine(root, "settings");
        var runner = Path.Combine(root, "runner");
        var diag = Path.Combine(runner, "_diag");
        var work = Path.Combine(runner, "_work");
        Directory.CreateDirectory(Path.Combine(settings, "logs"));
        Directory.CreateDirectory(diag);
        Directory.CreateDirectory(work);

        try
        {
            File.WriteAllText(Path.Combine(settings, "logs", "nrs-workbench.log"), new string('x', 100));
            File.WriteAllText(Path.Combine(diag, "Worker_old.log"), new string('w', 200));
            File.WriteAllText(Path.Combine(diag, "Worker_new.log"), new string('n', 300));
            File.WriteAllText(Path.Combine(diag, "Runner_old.log"), new string('r', 150));
            File.WriteAllText(Path.Combine(diag, "Runner_new.log"), new string('q', 250));
            File.WriteAllText(Path.Combine(work, "checkout.tmp"), new string('z', 400));

            var old = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            var recent = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
            foreach (var name in new[] { "Worker_old.log", "Runner_old.log" }) File.SetLastWriteTimeUtc(Path.Combine(diag, name), old);
            foreach (var name in new[] { "Worker_new.log", "Runner_new.log" }) File.SetLastWriteTimeUtc(Path.Combine(diag, name), recent);

            var service = new MaintenanceService(settings, () => new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));
            var snapshot = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Stopped, Mode = RunnerMode.Interactive }], 30);
            var diagEntry = snapshot.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            var workEntry = snapshot.Entries.Single(x => x.Id.StartsWith("work:", StringComparison.Ordinal));

            Assert(diagEntry.CandidateFiles.Count == 2 && diagEntry.CandidateFiles.Any(x => x.EndsWith("Worker_old.log", StringComparison.OrdinalIgnoreCase)) && diagEntry.CandidateFiles.Any(x => x.EndsWith("Runner_old.log", StringComparison.OrdinalIgnoreCase)), "maintenance selects only old diagnostics while preserving newest logs");
            Assert(workEntry.ReclaimableBytes == 0 && !workEntry.IsCleanable && workEntry.TotalBytes >= 400, "maintenance measures _work without making it cleanable");

            diagEntry.IsSelected = true;
            var cleaned = await service.CleanupAsync([diagEntry]);
            Assert(cleaned.CleanedFiles == 2 && File.Exists(Path.Combine(diag, "Worker_new.log")) && File.Exists(Path.Combine(diag, "Runner_new.log")) && !File.Exists(Path.Combine(diag, "Worker_old.log")), "maintenance deletes only reviewed diagnostic candidates");

            var busy = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Busy, Mode = RunnerMode.Interactive }], 7);
            var busyDiag = busy.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            Assert(!busyDiag.IsCleanable && busyDiag.ReclaimableBytes == 0, "maintenance blocks diagnostic cleanup while runner is BUSY");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Maintenance smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green; Console.Write("PASS "); Console.ResetColor(); Console.WriteLine(name);
    }
}
