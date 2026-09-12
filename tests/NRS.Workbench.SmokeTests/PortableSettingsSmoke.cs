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
