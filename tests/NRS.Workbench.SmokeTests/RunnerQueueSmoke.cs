using System.Runtime.CompilerServices;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.SmokeTests;

internal static class RunnerQueueSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        StartLimitQueuesExtraRunners();
        ExistingBusyJobsAreNeverStopped();
        ExistingBusyJobsMayTemporarilyExceedTheLimit();
        IdleSlotsRotateToReachOtherRunnerTargets();
    }

    private static void StartLimitQueuesExtraRunners()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 2 };
        var runners = new[] { Runner("a", RunnerState.Stopped), Runner("b", RunnerState.Stopped), Runner("c", RunnerState.Stopped) };

        queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch.AddSeconds(5)).GetAwaiter().GetResult();

        Assert(control.Started.SequenceEqual([runners[0].Alias, runners[1].Alias]) && control.Stopped.Count == 0,
            "runner queue starts only the configured number and leaves remaining runners queued");
    }

    private static void ExistingBusyJobsAreNeverStopped()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var runners = new[]
        {
            Runner("busy", RunnerState.Busy),
            Runner("idle", RunnerState.Ready),
            Runner("waiting", RunnerState.Stopped)
        };

        queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        Assert(control.Stopped.SequenceEqual(["idle"]) && control.Started.Count == 0,
            "runner queue drains idle listeners without interrupting busy jobs");
    }

    private static void IdleSlotsRotateToReachOtherRunnerTargets()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var idle = Runner("idle", RunnerState.Ready);
        var waiting = Runner("waiting", RunnerState.Stopped);
        var start = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([idle, waiting], settings, start).GetAwaiter().GetResult();
        queue.ReconcileAsync([idle, waiting], settings, start.AddSeconds(46)).GetAwaiter().GetResult();
        queue.ReconcileAsync([Runner("idle", RunnerState.Stopped), waiting], settings, start.AddSeconds(51)).GetAwaiter().GetResult();

        Assert(control.Stopped.SequenceEqual(["idle"]) && control.Started.SequenceEqual(["waiting"]),
            "runner queue rotates an idle slot to the next stopped runner");
    }

    private static void ExistingBusyJobsMayTemporarilyExceedTheLimit()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var runners = new[] { Runner("busy-a", RunnerState.Busy), Runner("busy-b", RunnerState.Busy) };

        var status = queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();

        Assert(control.Stopped.Count == 0 && control.Started.Count == 0 && !string.IsNullOrWhiteSpace(status),
            "runner queue leaves already-running jobs alone and waits for capacity before starting more runners");
    }

    private static RunnerInfo Runner(string name, RunnerState state) => new()
    {
        Alias = name,
        FolderPath = $@"C:\actions-runner-{name}",
        State = state
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeRunnerControl : IRunnerControlService
    {
        public List<string> Started { get; } = [];
        public List<string> Stopped { get; } = [];

        public Task StartAsync(RunnerInfo runner, CancellationToken cancellationToken = default)
        {
            Started.Add(runner.Alias);
            return Task.CompletedTask;
        }

        public Task StopAsync(RunnerInfo runner, CancellationToken cancellationToken = default)
        {
            Stopped.Add(runner.Alias);
            return Task.CompletedTask;
        }

        public Task<bool> StopIfIdleAsync(RunnerInfo runner, CancellationToken cancellationToken = default)
        {
            Stopped.Add(runner.Alias);
            return Task.FromResult(true);
        }

        public Task RestartAsync(RunnerInfo runner, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
