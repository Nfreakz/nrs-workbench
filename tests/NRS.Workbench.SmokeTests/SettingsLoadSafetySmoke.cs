using System.Text.Json;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class SettingsLoadSafetySmoke
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-workbench-settings-load-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            // A corrupt settings.json must be preserved, not overwritten with defaults.
            var corruptDir = Path.Combine(root, "corrupt");
            Directory.CreateDirectory(corruptDir);
            var corruptService = new SettingsService(corruptDir);
            const string corruptJson = "{ \"RunnerRoots\": [ broken";
            File.WriteAllText(corruptService.SettingsPath, corruptJson);
            corruptService.Load();
            var backups = Directory.GetFiles(corruptDir, "settings.json.corrupt-*");
            Assert(backups.Length == 1 && File.ReadAllText(backups[0]) == corruptJson,
                "corrupt settings.json is backed up before defaults are returned");

            // A configured root on a missing (unmounted) volume must not be replaced.
            var missingDir = Path.Combine(root, "missing");
            Directory.CreateDirectory(missingDir);
            var missingRoot = Path.Combine(root, "unplugged-usb", "runners");
            var missingService = new SettingsService(missingDir);
            File.WriteAllText(missingService.SettingsPath,
                JsonSerializer.Serialize(new RunnerSettings { RunnerRoots = [missingRoot], RepositoryPaths = [] }));
            var loaded = missingService.Load();
            Assert(loaded.RunnerRoots.Count == 1 && loaded.RunnerRoots[0] == missingRoot,
                "Load keeps RunnerRoots when a configured root does not exist");
            Assert(File.ReadAllText(missingService.SettingsPath).Contains("unplugged-usb", StringComparison.Ordinal),
                "Load does not rewrite settings.json for a missing root");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Settings load safety smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }
}
