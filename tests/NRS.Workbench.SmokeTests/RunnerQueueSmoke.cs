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
        RecoveryOnDisableRestoresOnlyApprovedRunners();
        ExternallyStoppedRunnersLeaveTheQueue();
        LocalJournalSurvivesRestartAndIsLocalOnly();
        ExistingBusyJobsAreNeverStopped();
        ExistingBusyJobsMayTemporarilyExceedTheLimit();
        IdleSlotsRotateToReachOtherRunnerTargets();
        DisablingQueuePreservesManuallyStoppedRunners();
        DisablingQueueRestoresOnlyRunnersStoppedByQueue();
        StaleReadySnapshotDoesNotLoseQueueStopOwnership();
        ResourceGuardBlocksStartsUntilCpuRecovers();
        ResourceGuardBlocksStartsUntilMemoryRecovers();
        QueuePauseFreezesRotationWithoutTouchingBusyJobs();
        QueueSnapshotDistinguishesWaitingAndManualStops();
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

    private static void RecoveryOnDisableRestoresOnlyApprovedRunners()
    {
        var owned = Runner("owned", RunnerState.Stopped);
        var manual = Runner("manual", RunnerState.Stopped);
        var store = new FakeQueueStateStore([owned.FolderPath]);
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control, store);
        var disabled = new RunnerSettings { RunnerQueueEnabled = false };

        queue.ReconcileAsync([owned, manual], disabled, DateTimeOffset.UnixEpoch).GetAwaiter().GetResult();
        Assert(control.Started.Count == 0,
            "disabling the queue on startup never restores journal entries before confirmation");
        queue.ApproveRecoveredQueueStops();
        queue.ReconcileAsync([owned, manual], disabled,
            DateTimeOffset.UnixEpoch.AddSeconds(5)).GetAwaiter().GetResult();
        Assert(control.Started.SequenceEqual(["owned"]) && store.Paths.Count == 0,
            "disabling the queue after approval restores only journal-owned stopped runners");
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

        Assert(control.Stopped.SequenceEqual(["second"]) && control.Started.SequenceEqual(["second"]),
            "a stale READY snapshot does not repeat the stop or lose queue stop ownership");
    }

    private static void ResourceGuardBlocksStartsUntilCpuRecovers()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings
        {
            RunnerQueueEnabled = true,
            RunnerQueueLimit = 1,
            RunnerQueueResourceGuardEnabled = true,
            RunnerQueueCpuStartThreshold = 80,
            RunnerQueueMemoryStartThreshold = 90
        };
        var first = Runner("first", RunnerState.Ready);
        var second = Runner("second", RunnerState.Ready);
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([first, second], settings, now).GetAwaiter().GetResult();
        queue.ReconcileAsync(
            [Runner("first", RunnerState.Stopped), Runner("second", RunnerState.Stopped)],
            settings, now.AddSeconds(5), Resources(cpu: 95, memoryPercent: 40)).GetAwaiter().GetResult();

        Assert(control.Started.Count == 0 &&
               queue.Snapshot.HoldReason == RunnerQueueHoldReason.HighCpu &&
               queue.Snapshot.NextAlias == "second",
            "smart queue blocks a queued runner start while CPU is above the configured threshold");

        queue.ReconcileAsync(
            [Runner("first", RunnerState.Stopped), Runner("second", RunnerState.Stopped)],
            settings, now.AddSeconds(10), Resources(cpu: 25, memoryPercent: 40)).GetAwaiter().GetResult();

        Assert(control.Started.SequenceEqual(["second"]),
            "smart queue starts the same queued runner after CPU returns below the threshold");
    }

    private static void ResourceGuardBlocksStartsUntilMemoryRecovers()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings
        {
            RunnerQueueEnabled = true,
            RunnerQueueLimit = 1,
            RunnerQueueResourceGuardEnabled = true,
            RunnerQueueCpuStartThreshold = 95,
            RunnerQueueMemoryStartThreshold = 75
        };
        var first = Runner("first", RunnerState.Ready);
        var second = Runner("second", RunnerState.Ready);
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync([first, second], settings, now).GetAwaiter().GetResult();
        queue.ReconcileAsync(
            [Runner("first", RunnerState.Stopped), Runner("second", RunnerState.Stopped)],
            settings, now.AddSeconds(5), Resources(cpu: 20, memoryPercent: 88)).GetAwaiter().GetResult();

        Assert(control.Started.Count == 0 &&
               queue.Snapshot.HoldReason == RunnerQueueHoldReason.HighMemory,
            "smart queue blocks new starts while RAM is above the configured threshold");

        queue.ReconcileAsync(
            [Runner("first", RunnerState.Stopped), Runner("second", RunnerState.Stopped)],
            settings, now.AddSeconds(10), Resources(cpu: 20, memoryPercent: 50)).GetAwaiter().GetResult();

        Assert(control.Started.SequenceEqual(["second"]),
            "smart queue resumes starts after RAM returns below the threshold");
    }

    private static void QueuePauseFreezesRotationWithoutTouchingBusyJobs()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync(
            [Runner("busy", RunnerState.Busy), Runner("idle", RunnerState.Ready)],
            settings, now).GetAwaiter().GetResult();
        var stopCountBeforePause = control.Stopped.Count;
        queue.TogglePaused();

        queue.ReconcileAsync(
            [Runner("busy", RunnerState.Busy), Runner("idle", RunnerState.Stopped)],
            settings, now.AddSeconds(60), Resources(cpu: 10, memoryPercent: 20)).GetAwaiter().GetResult();

        Assert(queue.Snapshot.Paused &&
               control.Started.Count == 0 &&
               control.Stopped.Count == stopCountBeforePause,
            "pausing the smart queue freezes scheduling and leaves an existing BUSY job untouched");
    }

    private static void QueueSnapshotDistinguishesWaitingAndManualStops()
    {
        var control = new FakeRunnerControl();
        var queue = new RunnerQueueCoordinator(control);
        var settings = new RunnerSettings { RunnerQueueEnabled = true, RunnerQueueLimit = 1 };
        var now = DateTimeOffset.UnixEpoch;

        queue.ReconcileAsync(
            [Runner("active", RunnerState.Ready), Runner("queued", RunnerState.Ready), Runner("manual", RunnerState.Stopped)],
            settings, now).GetAwaiter().GetResult();
        queue.ReconcileAsync(
            [Runner("active", RunnerState.Ready), Runner("queued", RunnerState.Stopped), Runner("manual", RunnerState.Stopped)],
            settings, now.AddSeconds(5), Resources(cpu: 10, memoryPercent: 20)).GetAwaiter().GetResult();

        Assert(queue.Snapshot.WaitingAliases.SequenceEqual(["queued"]) &&
               queue.Snapshot.ManualStoppedAliases.SequenceEqual(["manual"]) &&
               queue.Snapshot.NextAlias == "queued",
            "queue snapshot separates queue-owned waiting runners from manually stopped runners");
    }

    private static SystemResourceSnapshot Resources(double cpu, double memoryPercent)
    {
        const ulong total = 16UL * 1024 * 1024 * 1024;
        var used = (ulong)(total * Math.Clamp(memoryPercent, 0, 100) / 100d);
        return new SystemResourceSnapshot(cpu, 4, used, total, null, null);
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
