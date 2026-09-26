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
        ManuallyStoppedRunnersAreNotStartedAutomatically();
        QueueRotatesOnlyRunnersItStopped();
        RecoveryRequiresExplicitApproval();
        DeclinedRecoveryPreservesStoppedRunners();
        ExternallyStoppedRunnersLeaveTheQueue();
        LocalJournalSurvivesRestartAndIsLocalOnly();
        ExistingBusyJobsAreNeverStopped();
        ExistingBusyJobsMayTemporarilyExceedTheLimit();
        IdleSlotsRotateToReachOtherRunnerTargets();
        DisablingQueuePreservesManuallyStoppedRunners();
        DisablingQueueRestoresOnlyRunnersStoppedByQueue();
        StaleReadySnapshotDoesNotLoseQueueStopOwnership();
    }

    private static void ManuallyStoppedRunnersAreNotStartedAutomatically()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 2 };
        var runners = new[] { Runner("a", RunnerState.Stopped), Runner("b", RunnerState.Stopped), Runner("c", RunnerState.Stopped) };

        queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        queue.ReconcileAsync(runners, settings, DateTimeOffset.UnixEpoch.AddSeconds(46)).GetAwaiter().GetResult();

        Assert(control.Started.Count == 0 && control.Stopped.Count == 0,
            "runners stopped before the queue was enabled remain stopped");
    }

    private static void QueueRotatesOnlyRunnersItStopped()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var initiallyStopped = Runner("manual", RunnerState.Stopped);
        var first = Runner("first", RunnerState.Ready);
        var second = Runner("second", RunnerState.Ready);
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([first, second, initiallyStopped], settings, now).GetAwaiter().GetResult();
        queue.ReconcileAsync([first, Runner("second", RunnerState.Stopped), initiallyStopped],
            settings, now.AddSeconds(46)).GetAwaiter().GetResult();
        queue.ReconcileAsync([Runner("first", RunnerState.Stopped), Runner("second", RunnerState.Stopped), initiallyStopped],
            settings, now.AddSeconds(51)).GetAwaiter().GetResult();

        Assert(control.Stopped.SequenceEqual(["second", "first"]) &&
            control.Started.SequenceEqual(["second"]),
            "the queue rotates owned runners without starting a manually stopped runner");
    }

    private static void RecoveryRequiresExplicitApproval()
    {
        var store = new FakeQueueStateStore([Runner("owned", RunnerState.Stopped).FolderPath]);
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control, store);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var owned = Runner("owned", RunnerState.Stopped);
        var manual = Runner("manual", RunnerState.Stopped);

        queue.ReconcileAsync([owned, manual], settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        Assert(queue.RecoveredQueuePaths.Count == 1 && control.Started.Count == 0,
            "a persisted queue journal never starts a runner without confirmation");
        queue.ApproveRecoveredQueueStops();
        queue.ReconcileAsync([owned, manual], settings, DateTimeOffset.UnixEpoch.AddSeconds(5)).GetAwaiter().GetResult();
        Assert(control.Started.SequenceEqual(["owned"]) && store.Paths.Count == 0,
            "approved recovery starts only the previously managed runner and clears its journal entry");
    }

    private static void DeclinedRecoveryPreservesStoppedRunners()
    {
        var store = new FakeQueueStateStore([Runner("owned", RunnerState.Stopped).FolderPath]);
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control, store);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };

        queue.DiscardRecoveredQueueStops();
        queue.ReconcileAsync([Runner("owned", RunnerState.Stopped)], settings,
            DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        Assert(control.Started.Count == 0 && store.Paths.Count == 0,
            "declining recovery leaves previous queue-managed runners stopped");
    }

    private static void ExternallyStoppedRunnersLeaveTheQueue()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 2 };
        queue.ReconcileAsync([Runner("first", RunnerState.Ready)], settings,
            DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        queue.ReconcileAsync([Runner("first", RunnerState.Stopped)], settings,
            DateTimeOffset.UnixEpoch.AddSeconds(5)).GetAwaiter().GetResult();
        Assert(control.Started.Count == 0,
            "a runner stopped outside the queue is no longer auto-started");
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
        var waiting = Runner("waiting", RunnerState.Ready);
        var start = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([idle, waiting], settings, start).GetAwaiter().GetResult();
        queue.ReconcileAsync([idle, Runner("waiting", RunnerState.Stopped)], settings, start.AddSeconds(46)).GetAwaiter().GetResult();
        queue.ReconcileAsync([Runner("idle", RunnerState.Stopped), Runner("waiting", RunnerState.Stopped)],
            settings, start.AddSeconds(51)).GetAwaiter().GetResult();

        Assert(control.Stopped.SequenceEqual(["waiting", "idle"]) && control.Started.SequenceEqual(["waiting"]),
            "runner queue rotates an idle slot to a runner previously stopped by the queue");
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

    private static void DisablingQueuePreservesManuallyStoppedRunners()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var busy = Runner("busy", RunnerState.Busy);
        var manuallyStopped = Runner("manually-stopped", RunnerState.Stopped);

        queue.ReconcileAsync([busy, manuallyStopped], settings, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        settings.RunnerQueueEnabled = false;
        queue.ReconcileAsync([busy, manuallyStopped], settings, DateTimeOffset.UnixEpoch.AddSeconds(5)).GetAwaiter().GetResult();

        Assert(control.Started.Count == 0 && control.Stopped.Count == 0,
            "disabling the queue does not start a runner that the user had manually stopped");
    }

    private static void DisablingQueueRestoresOnlyRunnersStoppedByQueue()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var first = Runner("first", RunnerState.Ready);
        var queued = Runner("queued", RunnerState.Ready);
        var manuallyStopped = Runner("manually-stopped", RunnerState.Stopped);
        var start = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([first, queued, manuallyStopped], settings, start).GetAwaiter().GetResult();
        settings.RunnerQueueEnabled = false;
        queue.ReconcileAsync([first, Runner("queued", RunnerState.Stopped), manuallyStopped],
            settings, start.AddSeconds(5)).GetAwaiter().GetResult();

        Assert(control.Stopped.SequenceEqual(["queued"]) && control.Started.SequenceEqual(["queued"]),
            "disabling the queue restores only runners that the queue stopped");
    }

    private static void StaleReadySnapshotDoesNotLoseQueueStopOwnership()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var first = Runner("first", RunnerState.Ready);
        var second = Runner("second", RunnerState.Ready);
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([first, second], settings, now).GetAwaiter().GetResult();
        // The next scan can return the old READY state while stopping completes.
        queue.ReconcileAsync([first, second], settings, now.AddSeconds(1)).GetAwaiter().GetResult();
        settings.RunnerQueueEnabled = false;
        queue.ReconcileAsync([first, Runner("second", RunnerState.Stopped)],
            settings, now.AddSeconds(2)).GetAwaiter().GetResult();

        Assert(control.Started.SequenceEqual(["second"]),
            "a stale READY snapshot after an asynchronous stop does not lose queue stop ownership");
    }

    private static void LocalJournalSurvivesRestartAndIsLocalOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nrs-queue-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Runner("persisted", RunnerState.Stopped).FolderPath;
            var first = new FileRunnerQueueStateStore(directory);
            first.Save([path]);
            var reopened = new FileRunnerQueueStateStore(directory);
            Assert(reopened.Load().SequenceEqual([path]),
                "local runner queue ownership journal survives a new application session");
            reopened.Save([]);
            Assert(reopened.Load().Count == 0 &&
                !File.Exists(Path.Combine(directory, "runner-queue-state.json")),
                "clearing the queue journal deletes its local state file");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
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

    private sealed class FakeQueueStateStore : IRunnerQueueStateStore
    {
        public List<string> Paths { get; private set; }
        public FakeQueueStateStore(IEnumerable<string> paths) => Paths = paths.ToList();
        public IReadOnlyCollection<string> Load() => Paths.ToList();
        public void Save(IReadOnlyCollection<string> paths) => Paths = paths.ToList();
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
