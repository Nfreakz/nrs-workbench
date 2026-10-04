using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class MaintenanceSmoke
{
    internal static async Task RunAsync()
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

            var runnerActive = false;
            var service = new MaintenanceService(
                settings,
                () => new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero),
                _ => runnerActive);
            var snapshot = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Stopped, Mode = RunnerMode.Interactive }], 30);
            var diagEntry = snapshot.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            var workEntry = snapshot.Entries.Single(x => x.Id.StartsWith("work:", StringComparison.Ordinal));

            Assert(diagEntry.CandidateFiles.Count == 2 && diagEntry.CandidateFiles.Any(x => x.EndsWith("Worker_old.log", StringComparison.OrdinalIgnoreCase)) && diagEntry.CandidateFiles.Any(x => x.EndsWith("Runner_old.log", StringComparison.OrdinalIgnoreCase)), "maintenance selects only old diagnostics while preserving newest logs");
            Assert(workEntry.ReclaimableBytes == 0 && !workEntry.IsCleanable && workEntry.TotalBytes >= 400, "maintenance measures _work without making it cleanable");

            diagEntry.IsSelected = true;
            var cleaned = await service.CleanupAsync([diagEntry]);
            Assert(cleaned.CleanedFiles == 2 && File.Exists(Path.Combine(diag, "Worker_new.log")) && File.Exists(Path.Combine(diag, "Runner_new.log")) && !File.Exists(Path.Combine(diag, "Worker_old.log")), "maintenance deletes only reviewed diagnostic candidates");

            File.WriteAllText(Path.Combine(diag, "Worker_old_again.log"), new string('a', 120));
            File.SetLastWriteTimeUtc(Path.Combine(diag, "Worker_old_again.log"), old);
            var staleScan = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Stopped, Mode = RunnerMode.Interactive }], 30);
            var staleEntry = staleScan.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            staleEntry.IsSelected = true;
            runnerActive = true;
            var skipped = await service.CleanupAsync([staleEntry]);
            Assert(skipped.SkippedEntries == 1 && File.Exists(Path.Combine(diag, "Worker_old_again.log")),
                "maintenance revalidates runner activity immediately before deleting diagnostics");
            runnerActive = false;

            File.WriteAllText(Path.Combine(diag, "Worker_candidate.log"), new string('c', 130));
            File.SetLastWriteTimeUtc(Path.Combine(diag, "Worker_candidate.log"), old);
            var newestGuardScan = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Stopped, Mode = RunnerMode.Interactive }], 30);
            var newestGuardEntry = newestGuardScan.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            newestGuardEntry.IsSelected = true;
            File.Delete(Path.Combine(diag, "Worker_new.log"));
            File.SetLastWriteTimeUtc(Path.Combine(diag, "Worker_candidate.log"), recent);
            var newestGuardResult = await service.CleanupAsync([newestGuardEntry]);
            Assert(File.Exists(Path.Combine(diag, "Worker_candidate.log")) && newestGuardResult.FailedFiles == 0,
                "maintenance preserves the newest Worker log again at cleanup time");

            var busy = await service.ScanAsync([new RunnerInfo { Alias = "maintenance", FolderPath = runner, State = RunnerState.Busy, Mode = RunnerMode.Interactive }], 7);
            var busyDiag = busy.Entries.Single(x => x.Id.StartsWith("diag:", StringComparison.Ordinal));
            Assert(!busyDiag.IsCleanable && busyDiag.ReclaimableBytes == 0, "maintenance blocks diagnostic cleanup while runner is BUSY");

            var unsafeRunner = Path.Combine(root, "runner-unsafe-work");
            var outsideWork = Path.Combine(root, "outside-work");
            Directory.CreateDirectory(unsafeRunner);
            Directory.CreateDirectory(outsideWork);
            File.WriteAllText(Path.Combine(outsideWork, "sensitive.tmp"), new string('s', 512));
            File.WriteAllText(
                Path.Combine(unsafeRunner, ".runner"),
                "{\"workFolder\":\"..\\\\outside-work\"}");
            var unsafeScan = await service.ScanAsync(
                [new RunnerInfo { Alias = "unsafe-work", FolderPath = unsafeRunner, State = RunnerState.Stopped, Mode = RunnerMode.Interactive }],
                30);
            var unsafeWork = unsafeScan.Entries.Single(x => x.Id.StartsWith("work:", StringComparison.Ordinal));
            Assert(unsafeWork.TotalBytes == 0 && unsafeWork.ReclaimableBytes == 0 && !unsafeWork.IsCleanable,
                "maintenance refuses to traverse workFolder paths that escape the runner directory");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Maintenance smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green; Console.Write("PASS "); Console.ResetColor(); Console.WriteLine(name);
    }
}
