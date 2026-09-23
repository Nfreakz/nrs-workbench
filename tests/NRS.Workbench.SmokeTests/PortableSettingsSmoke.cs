using System.Runtime.CompilerServices;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class PortableSettingsSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-workbench-portable-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var runnerRoot = Path.Combine(root, "runners");
            var runnerFolder = Path.Combine(runnerRoot, "actions-runner-test");
            var repositoryPath = Path.Combine(root, "repo");
            Directory.CreateDirectory(runnerFolder);
            Directory.CreateDirectory(repositoryPath);

            const string syntheticRunnerSecret = "synthetic-runner-secret-never-export";
            File.WriteAllText(Path.Combine(runnerFolder, ".credentials"), syntheticRunnerSecret);

            var settings = new RunnerSettings
            {
                RunnerRoots = [runnerRoot, "  " + runnerRoot + "  "],
                FolderPattern = "actions-runner*",
                RefreshIntervalSeconds = 999,
                ConfirmStopBusy = false,
                KeepInTray = false,
                NotificationsEnabled = true,
                NotifyJobStarted = false,
                NotifyJobCompleted = true,
                NotifyRunnerIssues = false,
                NotifyOnlyWhenHidden = false,
                RepositoryPaths = [repositoryPath, repositoryPath]
            };

            var service = new SettingsService();
            var exportPath = Path.Combine(root, "portable.json");
            service.ExportPortableSettings(settings, exportPath);

            var json = File.ReadAllText(exportPath);
            Assert(!json.Contains(syntheticRunnerSecret, StringComparison.Ordinal),
                "portable settings export does not read runner credentials");

            var imported = service.ReadPortableSettings(exportPath);
            Assert(imported.RunnerRoots.Count == 1 && imported.RunnerRoots[0] == runnerRoot,
                "portable settings import normalizes duplicate runner roots");
            Assert(imported.RepositoryPaths.Count == 1 && imported.RepositoryPaths[0] == repositoryPath,
                "portable settings import normalizes duplicate repository paths");
            Assert(imported.RefreshIntervalSeconds == 300 &&
                   !imported.ConfirmStopBusy &&
                   !imported.KeepInTray &&
                   imported.NotificationsEnabled &&
                   !imported.NotifyJobStarted &&
                   imported.NotifyJobCompleted &&
                   !imported.NotifyRunnerIssues &&
                   !imported.NotifyOnlyWhenHidden,
                "portable settings round-trip preserves allowed preferences and clamps refresh interval");

            var beforePreview = settings.RepositoryPaths.ToList();
            var previewOnly = service.ReadPortableSettings(exportPath);
            Assert(settings.RepositoryPaths.SequenceEqual(beforePreview) &&
                   File.ReadAllText(exportPath) == json,
                   "reading import for review leaves the current settings object and JSON untouched");

            var missingRoot = Path.Combine(root, "missing-runner-root");
            var importedRunnerRoots = new RunnerSettings { RunnerRoots = [missingRoot] };
            Assert(PortableRepositoryPaths.RepairRunnerRoots(importedRunnerRoots, [runnerRoot]) &&
                   importedRunnerRoots.RunnerRoots.Single() == runnerRoot,
                   "missing runner roots can be replaced by detected local roots");
            var keepValid = new RunnerSettings { RunnerRoots = [runnerRoot, missingRoot] };
            Assert(!PortableRepositoryPaths.RepairRunnerRoots(keepValid, [root]) &&
                   keepValid.RunnerRoots.Count == 2,
                   "runner auto-detection does not overwrite partially valid roots");
            var noDetected = new RunnerSettings { RunnerRoots = [missingRoot] };
            Assert(!PortableRepositoryPaths.RepairRunnerRoots(noDetected, []) &&
                   noDetected.RunnerRoots.Single() == missingRoot,
                   "missing runner roots remain when detection finds nothing");

            var existingPath = Path.Combine(root, "existing");
            Assert(PortableRepositoryPaths.PreserveExisting(previewOnly, [existingPath]) == 1 &&
                   PortableRepositoryPaths.PreserveExisting(previewOnly, [existingPath]) == 0 &&
                   previewOnly.RepositoryPaths.Contains(existingPath),
                   "keeping local registrations merges without duplicates");

            var oldRoot = Path.Combine(root, "old-root");
            var targetRoot = Path.Combine(root, "target-root");
            var otherRoot = Path.Combine(root, "other-root");
            var absentPath = Path.Combine(oldRoot, "projects", "repo");
            var targetPath = Path.Combine(targetRoot, "projects", "repo");
            Directory.CreateDirectory(Path.Combine(targetPath, ".git"));
            var remap = new RunnerSettings { RepositoryPaths = [absentPath] };
            Assert(PortableRepositoryPaths.Repair(remap, [targetRoot], _ => oldRoot) == 1 &&
                   remap.RepositoryPaths.Single() == targetPath,
                   "unique destination with .git folder is repaired");

            var oldWorktree = Path.Combine(oldRoot, "projects", "worktree");
            var newWorktree = Path.Combine(targetRoot, "projects", "worktree");
            Directory.CreateDirectory(newWorktree);
            File.WriteAllText(Path.Combine(newWorktree, ".git"), "gitdir: elsewhere");
            var worktree = new RunnerSettings { RepositoryPaths = [oldWorktree] };
            Assert(PortableRepositoryPaths.Repair(worktree, [targetRoot], _ => oldRoot) == 1 &&
                   worktree.RepositoryPaths.Single() == newWorktree,
                   "worktree with .git file is repaired");

            Directory.CreateDirectory(Path.Combine(otherRoot, "projects", "repo", ".git"));
            var ambiguous = new RunnerSettings { RepositoryPaths = [absentPath] };
            Assert(PortableRepositoryPaths.Repair(ambiguous, [targetRoot, otherRoot], _ => oldRoot) == 0 &&
                   ambiguous.RepositoryPaths.Single() == absentPath,
                   "ambiguous destination is not selected");

            var nonGit = new RunnerSettings { RepositoryPaths = [Path.Combine(oldRoot, "projects", "ordinary")] };
            Directory.CreateDirectory(Path.Combine(targetRoot, "projects", "ordinary"));
            Assert(PortableRepositoryPaths.Repair(nonGit, [targetRoot], _ => oldRoot) == 0,
                   "non-Git destination is not selected");

            var missing = new RunnerSettings { RepositoryPaths = [Path.Combine(oldRoot, "projects", "unknown")] };
            Assert(PortableRepositoryPaths.Repair(missing, [targetRoot], _ => oldRoot) == 0 &&
                   missing.RepositoryPaths.Count == 1,
                   "unresolved import remains available for manual repair");

            var relative = new RunnerSettings { RepositoryPaths = ["projects\\\\repo"] };
            Assert(PortableRepositoryPaths.Repair(relative, [targetRoot], _ => oldRoot) == 0,
                   "relative path cannot be silently relocated");

            var wrongProductPath = Path.Combine(root, "wrong-product.json");
            File.WriteAllText(wrongProductPath,
                json.Replace("\"Product\": \"NRS Workbench\"", "\"Product\": \"Other Product\"", StringComparison.Ordinal));
            AssertThrows<InvalidDataException>(() => service.ReadPortableSettings(wrongProductPath),
                "portable settings import rejects exports from another product");

            var wrongSchemaPath = Path.Combine(root, "wrong-schema.json");
            File.WriteAllText(wrongSchemaPath,
                json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 999", StringComparison.Ordinal));
            AssertThrows<InvalidDataException>(() => service.ReadPortableSettings(wrongSchemaPath),
                "portable settings import rejects unsupported schema versions");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Portable settings smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }

    private static void AssertThrows<TException>(Action action, string name) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            Assert(true, name);
            return;
        }

        throw new InvalidOperationException("Portable settings smoke test failed: " + name);
    }
}
