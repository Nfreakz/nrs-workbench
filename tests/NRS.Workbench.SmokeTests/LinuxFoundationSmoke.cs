using System.Runtime.CompilerServices;
using NRS.Workbench.Platform.Linux;

namespace NRS.Workbench.SmokeTests;

internal static class LinuxFoundationSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunnerLayout();
        ProcParsing();
        XdgPaths();
        SystemdMapping();
    }

    private static void RunnerLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "nrs-linux-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var linuxRunner = Path.Combine(root, "runner-linux");
            var windowsOnly = Path.Combine(root, "runner-windows-only");
            var nested = Path.Combine(root, "group", "nested-runner");

            Directory.CreateDirectory(Path.Combine(linuxRunner, "bin"));
            Directory.CreateDirectory(windowsOnly);
            Directory.CreateDirectory(nested);

            File.WriteAllText(Path.Combine(linuxRunner, "run.sh"), "#!/usr/bin/env bash");
            File.WriteAllText(Path.Combine(linuxRunner, "bin", "Runner.Listener"), "test");
            File.WriteAllText(Path.Combine(linuxRunner, ".service"), "actions.runner.demo.service");
            File.WriteAllText(Path.Combine(windowsOnly, "run.cmd"), "@echo off");
            File.WriteAllText(Path.Combine(nested, ".runner"), "{}");

            var detected = LinuxRunnerLayout.FindRunnerFolders(root);
            Assert(detected.Contains(Path.GetFullPath(linuxRunner), StringComparer.Ordinal),
                "Linux runner detection accepts run.sh / Runner.Listener");
            Assert(!detected.Contains(Path.GetFullPath(windowsOnly), StringComparer.Ordinal),
                "Linux runner detection ignores Windows-only run.cmd marker");
            Assert(!detected.Contains(Path.GetFullPath(nested), StringComparer.Ordinal),
                "Linux runner root scanning remains shallow");
            Assert(LinuxRunnerLayout.ReadServiceName(linuxRunner) == "actions.runner.demo.service",
                "Linux runner reads the local .service marker without credentials");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void ProcParsing()
    {
        var previous = LinuxSystemResourceProbe.ParseCpuTimes(
            "cpu  100 0 50 850 0 0 0 0 0 0\n");
        var current = LinuxSystemResourceProbe.ParseCpuTimes(
            "cpu  150 0 70 880 0 0 0 0 0 0\n");
        var cpu = LinuxSystemResourceProbe.CalculateCpuPercent(previous, current);
        Assert(cpu is not null && Math.Abs(cpu.Value - 70d) < 0.001,
            "Linux /proc/stat CPU parser calculates aggregate utilization");

        var memory = LinuxSystemResourceProbe.ParseMemory(
            "MemTotal:       1000 kB\nMemAvailable:    400 kB\n");
        Assert(memory.TotalBytes == 1_024_000UL && memory.UsedBytes == 614_400UL,
            "Linux /proc/meminfo parser calculates used physical memory");
    }

    private static void XdgPaths()
    {
        var home = Path.Combine(Path.GetTempPath(), "linux-home");
        var xdgConfig = Path.Combine(Path.GetTempPath(), "xdg-config");
        var xdgData = Path.Combine(Path.GetTempPath(), "xdg-data");

        Assert(
            LinuxAppDataPaths.GetConfigDirectory(home, xdgConfig) ==
            Path.GetFullPath(Path.Combine(xdgConfig, "nrs-workbench")),
            "Linux config path honors XDG_CONFIG_HOME");
        Assert(
            LinuxAppDataPaths.GetDataDirectory(home, xdgData, preview: true) ==
            Path.GetFullPath(Path.Combine(xdgData, "nrs-workbench-preview")),
            "Linux preview data path is isolated under XDG_DATA_HOME");
        Assert(
            LinuxAppDataPaths.GetConfigDirectory(home, null) ==
            Path.GetFullPath(Path.Combine(home, ".config", "nrs-workbench")),
            "Linux config path falls back to ~/.config");
    }

    private static void SystemdMapping()
    {
        var commands = new FakeLinuxCommandRunner();
        commands.Enqueue(new LinuxCommandResult(0, "loaded", ""));
        commands.Enqueue(new LinuxCommandResult(0, "active", ""));
        commands.Enqueue(new LinuxCommandResult(0, "", ""));
        commands.Enqueue(new LinuxCommandResult(0, "", ""));

        var controller = new SystemdServiceController(commands);
        Assert(controller.Exists("actions.runner.demo.service"),
            "systemd controller recognizes a loaded runner service");
        Assert(controller.GetState("actions.runner.demo.service") == SystemdServiceState.Active,
            "systemd controller maps ActiveState=active");
        controller.Start("actions.runner.demo.service");
        controller.Stop("actions.runner.demo.service");

        Assert(commands.Calls.Count == 4 &&
               commands.Calls[0].Arguments.SequenceEqual(["show", "--property=LoadState", "--value", "--", "actions.runner.demo.service"]) &&
               commands.Calls[2].Arguments.SequenceEqual(["start", "--", "actions.runner.demo.service"]) &&
               commands.Calls[3].Arguments.SequenceEqual(["stop", "--", "actions.runner.demo.service"]),
            "systemd control uses argument lists and never invokes sudo or a shell");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Linux foundation smoke test failed: " + name);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("PASS ");
        Console.ResetColor();
        Console.WriteLine(name);
    }

    private sealed class FakeLinuxCommandRunner : ILinuxCommandRunner
    {
        private readonly Queue<LinuxCommandResult> _results = new();
        public List<(string FileName, string[] Arguments)> Calls { get; } = [];

        public void Enqueue(LinuxCommandResult result) => _results.Enqueue(result);

        public LinuxCommandResult Run(string fileName, params string[] arguments)
        {
            Calls.Add((fileName, arguments));
            return _results.Count > 0
                ? _results.Dequeue()
                : new LinuxCommandResult(1, string.Empty, "No fake result configured.");
        }
    }
}
