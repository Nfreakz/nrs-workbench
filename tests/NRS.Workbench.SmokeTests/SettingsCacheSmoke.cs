using System.Text.Json;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class SettingsCacheSmoke
{
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-workbench-settings-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var missingRoot = Path.Combine(root, "unplugged-usb", "runners");
            var service = new SettingsService(root);
            File.WriteAllText(service.SettingsPath,
                JsonSerializer.Serialize(new RunnerSettings { RunnerRoots = [missingRoot], RefreshIntervalSeconds = 7 }));

            var first = service.Load();
            Assert(first.RefreshIntervalSeconds == 7, "first Load reads settings.json");

            // Change the file behind the service's back: the cache must still win.
            File.WriteAllText(service.SettingsPath,
                JsonSerializer.Serialize(new RunnerSettings { RunnerRoots = [missingRoot], RefreshIntervalSeconds = 11 }));
            var second = service.Load();
            Assert(second.RefreshIntervalSeconds == 7, "second Load is served from the cache and ignores the changed file");

            // Every instance for the same path shares the cache.
            var sibling = new SettingsService(root);
            Assert(sibling.Load().RefreshIntervalSeconds == 7, "a second SettingsService instance shares the cache");

            service.Invalidate();
            var reloaded = service.Load();
            Assert(reloaded.RefreshIntervalSeconds == 11, "Invalidate forces the next Load to re-read the file");

            // Mutating the returned object must not leak into the cache.
            reloaded.RefreshIntervalSeconds = 99;
            reloaded.RunnerRoots.Add(Path.Combine(root, "mutated"));
            var afterMutation = service.Load();
            Assert(afterMutation.RefreshIntervalSeconds == 11 && afterMutation.RunnerRoots.Count == 1,
                "mutating a loaded copy does not affect the cache");

            var toSave = service.Load();
            toSave.RefreshIntervalSeconds = 42;
            toSave.UpdateCheckEnabled = false;
            toSave.LastUpdateCheckUtc = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
            service.Save(toSave);
            var saved = sibling.Load();
            Assert(saved.RefreshIntervalSeconds == 42 && !saved.UpdateCheckEnabled &&
                   saved.LastUpdateCheckUtc == new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero),
                "Save then Load returns the saved values through the shared cache");
            Assert(File.ReadAllText(service.SettingsPath).Contains("\"RefreshIntervalSeconds\": 42", StringComparison.Ordinal),
                "Save still writes settings.json to disk");
        }
        finally
        {
            try { new SettingsService(root).Invalidate(); } catch { }
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Settings cache smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }
}
