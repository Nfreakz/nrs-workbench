using System.Runtime.CompilerServices;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.SmokeTests;

internal static class RunnerFolderDiscoverySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-runner-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var misnamedRunner = Path.Combine(root, "runner-lespedreres-typo");
            var emptyPrefixedFolder = Path.Combine(root, "actions-runner-empty");
            var nestedGroup = Path.Combine(root, "nested");
            var nestedRunner = Path.Combine(nestedGroup, "whatever-name");

            Directory.CreateDirectory(misnamedRunner);
            Directory.CreateDirectory(emptyPrefixedFolder);
            Directory.CreateDirectory(nestedRunner);

            File.WriteAllText(Path.Combine(misnamedRunner, "run.cmd"), "@echo off");
            File.WriteAllText(Path.Combine(nestedRunner, ".runner"), "{}");

            var detected = RunnerFolderDiscovery.FindRunnerFolders(root);

            Assert(detected.Contains(Path.GetFullPath(misnamedRunner), StringComparer.OrdinalIgnoreCase),
                "runner detection accepts a valid installation with a non-standard folder name");
            Assert(!detected.Contains(Path.GetFullPath(emptyPrefixedFolder), StringComparer.OrdinalIgnoreCase),
                "runner detection ignores an empty actions-runner-prefixed folder");
            Assert(!detected.Contains(Path.GetFullPath(nestedRunner), StringComparer.OrdinalIgnoreCase),
                "runner root scanning remains shallow");
            Assert(RunnerFolderDiscovery.FindRunnerFolders(misnamedRunner)
                    .Contains(Path.GetFullPath(misnamedRunner), StringComparer.OrdinalIgnoreCase),
                "an explicitly configured runner folder is accepted as its own root");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Runner discovery smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }
}
