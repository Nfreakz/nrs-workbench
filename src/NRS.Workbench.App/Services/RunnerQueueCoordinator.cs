using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Keeps a bounded set of local runner listeners online. Idle listeners rotate
/// so jobs for runners with different repository targets can still be picked up.
/// GitHub keeps jobs queued while their matching runner is offline.
/// </summary>
public sealed class RunnerQueueCoordinator
{
    private static readonly TimeSpan IdleRotationDelay = TimeSpan.FromSeconds(45);
    private readonly IRunnerControlService _control;
    private readonly Dictionary<string, DateTimeOffset> _readySince = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _pendingStarts = new(StringComparer.OrdinalIgnoreCase);
    // Only restore runners this coordinator actually stopped. Users may have
    // intentionally left other runners stopped before enabling the queue.
    private readonly HashSet<string> _stoppedByQueue = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _eligiblePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _stopRequestedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recoveryPending = new(StringComparer.OrdinalIgnoreCase);
    private readonly IRunnerQueueStateStore? _stateStore;
    private bool _eligibleInitialized;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _lastScheduledIndex = -1;
    private bool _queueWasEnabled;

    public bool IsPaused { get; private set; }
    public RunnerQueueSnapshot Snapshot { get; private set; } = RunnerQueueSnapshot.Disabled();

    public bool TogglePaused()
    {
        IsPaused = !IsPaused;
        return IsPaused;
    }

    public RunnerQueueCoordinator(IRunnerControlService control, IRunnerQueueStateStore? stateStore = null)
    {
        _control = control;
        _stateStore = stateStore;
        if (stateStore is not null)
            _recoveryPending.UnionWith(stateStore.Load());
    }

    // Persisted entries are not trusted after an app restart: the user must
    // explicitly review and approve restoring the recorded runner folders.
    public IReadOnlyCollection<string> RecoveredQueuePaths => _recoveryPending.ToList();

    public void ApproveRecoveredQueueStops()
    {
        _stoppedByQueue.UnionWith(_recoveryPending);
        _recoveryPending.Clear();
    }

    public void DiscardRecoveredQueueStops()
    {
        _recoveryPending.Clear();
        _stateStore?.Save([]);
    }

    private void PersistOwnedStops() => _stateStore?.Save(_stoppedByQueue.ToList());

    public async Task<string> ReconcileAsync(IReadOnlyList<RunnerInfo> runners, RunnerSettings settings,
        DateTimeOffset? now = null, SystemResourceSnapshot? resources = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_recoveryPending.Count > 0)
            {
                var recoveryStatus = UiLanguage.Text("Hay runners pendientes de revisión tras reiniciar NRS Workbench.");
                Snapshot = BuildSnapshot(runners, settings, resources, RunnerQueueHoldReason.Recovering, recoveryStatus);
                return recoveryStatus;
            }

            if (!settings.RunnerQueueEnabled)
            {
                if (!_queueWasEnabled && _stoppedByQueue.Count == 0) return string.Empty;
                _readySince.Clear();
                _pendingStarts.Clear();
                _stopRequestedAt.Clear();
                _eligibleInitialized = false;
                _eligiblePaths.Clear();
                var runnersToRestore = runners.Where(runner =>
                    runner.State == RunnerState.Stopped && _stoppedByQueue.Contains(runner.FolderPath)).ToList();
                var outcomes = new List<bool>();
                foreach (var runner in runnersToRestore)
                {
                    try
                    {
                        await _control.StartAsync(runner, cancellationToken);
                        _stoppedByQueue.Remove(runner.FolderPath);
                        PersistOwnedStops();
                        outcomes.Add(true);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Runner queue could not restore '{runner.Alias}'", ex);
                        outcomes.Add(false);
                    }
                }
                // A stop may still be in flight when a snapshot says READY.
                // Retain the marker until the stopped state is observed and restored.
                _queueWasEnabled = false;
                IsPaused = false;
                var disabledStatus = outcomes.Any(success => !success)
                    ? UiLanguage.Choose("Cola desactivada · algunos runners gestionados no se pudieron iniciar.", "Queue disabled · some queue-managed runners could not be started.")
                    : UiLanguage.Choose("Cola desactivada · runners gestionados restaurados; los detenidos manualmente siguen detenidos.", "Queue disabled · queue-managed runners restored; manually stopped runners remain stopped.");
                Snapshot = BuildSnapshot(runners, settings, resources, RunnerQueueHoldReason.Disabled, disabledStatus);
                return disabledStatus;
            }

            _queueWasEnabled = true;
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var ordered = OrderRunners(runners, settings.RunnerDisplayOrder).ToList();
            var knownPaths = ordered.Select(runner => runner.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!_eligibleInitialized)
            {
                // Online at enable-time or explicitly approved from the journal:
                // an already-stopped runner is not an implicit queue candidate.
                _eligiblePaths.UnionWith(ordered.Where(IsOnline).Select(runner => runner.FolderPath));
                _eligiblePaths.UnionWith(_stoppedByQueue);
                _eligibleInitialized = true;
            }
            // A runner started outside the queue is an explicit new participant.
            _eligiblePaths.UnionWith(ordered.Where(IsOnline).Select(runner => runner.FolderPath));
            _eligiblePaths.RemoveWhere(path => !knownPaths.Contains(path));
            foreach (var stale in _stopRequestedAt.Keys.Where(path => !knownPaths.Contains(path)).ToList())
                _stopRequestedAt.Remove(stale);
            foreach (var stale in _readySince.Keys.Where(path => !knownPaths.Contains(path)).ToList())
                _readySince.Remove(stale);
            foreach (var stale in _pendingStarts.Keys.Where(path => !knownPaths.Contains(path)).ToList())
                _pendingStarts.Remove(stale);
            if (_stoppedByQueue.RemoveWhere(path => !knownPaths.Contains(path)) > 0)
                PersistOwnedStops();

            foreach (var runner in ordered)
            {
                if (runner.State != RunnerState.Stopped ||
                    (_pendingStarts.TryGetValue(runner.FolderPath, out var requestedAt) && timestamp - requestedAt >= TimeSpan.FromSeconds(30)))
                    _pendingStarts.Remove(runner.FolderPath);
                if (runner.State == RunnerState.Ready)
                    _readySince.TryAdd(runner.FolderPath, timestamp);
                else
                    _readySince.Remove(runner.FolderPath);
                // The first snapshot after StopIfIdleAsync may still report READY.
                // BUSY means a job was accepted and the stop did not complete.
                if (runner.State == RunnerState.Stopped)
                    _stopRequestedAt.Remove(runner.FolderPath);
                // READY immediately after StopIfIdleAsync may be a stale snapshot.
                // If it stays READY for 30 seconds, assume the stop did not
                // take effect or the user has subsequently restarted it.
                if (runner.State == RunnerState.Ready &&
                    _stoppedByQueue.Contains(runner.FolderPath) &&
                    (!_stopRequestedAt.TryGetValue(runner.FolderPath, out var stoppedAt) ||
                     timestamp - stoppedAt >= TimeSpan.FromSeconds(30)))
                {
                    _stopRequestedAt.Remove(runner.FolderPath);
                    _stoppedByQueue.Remove(runner.FolderPath);
                    PersistOwnedStops();
                }
                if (runner.State == RunnerState.Busy && _stoppedByQueue.Remove(runner.FolderPath))
                {
                    _stopRequestedAt.Remove(runner.FolderPath);
                    PersistOwnedStops();
                }
                // An eligible runner stopped outside the queue is now manually
                // stopped. Never reclaim it until the user starts it again.
                if (runner.State == RunnerState.Stopped &&
                    !_stoppedByQueue.Contains(runner.FolderPath) &&
                    !_pendingStarts.ContainsKey(runner.FolderPath))
                    _eligiblePaths.Remove(runner.FolderPath);
            }

            var limit = Math.Clamp(settings.RunnerQueueLimit, 1, 2);
            var active = ordered.Where(IsOnline).ToList();
            var pendingStartCount = _pendingStarts.Keys.Count(path => ordered.Any(runner =>
                string.Equals(runner.FolderPath, path, StringComparison.OrdinalIgnoreCase) && runner.State == RunnerState.Stopped));
            // A just-requested stop can still appear READY for one or more
            // discovery snapshots. Count that slot as already draining so the
            // queue never reacts to stale state by stopping another idle runner.
            var pendingStopCount = _stopRequestedAt.Keys.Count(path => ordered.Any(runner =>
                string.Equals(runner.FolderPath, path, StringComparison.OrdinalIgnoreCase) && IsOnline(runner)));
            var busyCount = active.Count(runner => runner.State == RunnerState.Busy);
            var stopped = ordered.Where(runner => runner.State == RunnerState.Stopped &&
                _eligiblePaths.Contains(runner.FolderPath) &&
                !_pendingStarts.ContainsKey(runner.FolderPath)).ToList();
            var managedActiveCount = Math.Max(0, active.Count - pendingStopCount) + pendingStartCount;

            if (IsPaused)
            {
                var pausedStatus = UiLanguage.Choose(
                    "Cola pausada · no se iniciarán ni rotarán runners hasta reanudarla.",
                    "Queue paused · runners will not start or rotate until resumed.",
                    "Cua pausada · no s'iniciaran ni es rotaran runners fins que es reprengui.");
                Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.Paused, pausedStatus);
                return pausedStatus;
            }

            // When enabling the queue with more listeners already online, drain
            // idle listeners first. Busy jobs are never stopped by the queue.
            var excess = managedActiveCount - limit;
            if (excess > 0)
            {
                var idleToStop = ordered.Where(runner => runner.State == RunnerState.Ready &&
                        !_stopRequestedAt.ContainsKey(runner.FolderPath))
                    .Reverse().Take(excess).ToList();
                if (idleToStop.Count == 0)
                {
                    var capacityStatus = Summary(active.Count, busyCount, stopped.Count, limit, waitingForCapacity: true);
                    Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.BusyCapacity, capacityStatus);
                    return capacityStatus;
                }

                var stoppedResults = new List<StopResult>();
                foreach (var runner in idleToStop)
                    stoppedResults.Add(await StopIfStillIdleAsync(runner, timestamp));
                var failures = stoppedResults.Where(result => !result.Success && !result.BecameBusy).ToList();
                if (failures.Count > 0)
                {
                    var stopError = UiLanguage.Choose("Cola: no se pudo detener un runner libre. Comprueba permisos de administrador.", "Queue: could not stop an idle runner. Check administrator permissions.");
                    Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, stopError);
                    return stopError;
                }
                var drainingStatus = Summary(active.Count, busyCount, stopped.Count, limit, changing: true);
                Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, drainingStatus);
                return drainingStatus;
            }

            var resourceHold = DetermineResourceHold(settings, resources);
            if (stopped.Count > 0 && resourceHold != RunnerQueueHoldReason.None)
            {
                var resourceStatus = ResourceHoldStatus(resourceHold, settings, resources);
                Snapshot = BuildSnapshot(ordered, settings, resources, resourceHold, resourceStatus);
                return resourceStatus;
            }

            // Rotate an idle slot after a grace period. This lets jobs for other
            // GitHub targets/labels progress without stopping a running job.
            if (stopped.Count > 0 && managedActiveCount >= limit)
            {
                var rotation = ordered.Where(runner => runner.State == RunnerState.Ready &&
                        !_stopRequestedAt.ContainsKey(runner.FolderPath) &&
                        _readySince.TryGetValue(runner.FolderPath, out var since) && timestamp - since >= IdleRotationDelay)
                    .OrderBy(runner => _readySince[runner.FolderPath])
                    .FirstOrDefault();
                if (rotation is not null)
                {
                    var result = await StopIfStillIdleAsync(rotation, timestamp);
                    if (!result.Success && !result.BecameBusy)
                    {
                        var rotateError = UiLanguage.Choose($"Cola: no se pudo rotar {rotation.Alias}. Comprueba permisos de administrador.", $"Queue: could not rotate {rotation.Alias}. Check administrator permissions.", $"Cua: no s\u0027ha pogut rotar {rotation.Alias}. Comprova els permisos d\u0027administrador.");
                        Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, rotateError);
                        return rotateError;
                    }
                    if (result.Success)
                    {
                        _lastScheduledIndex = ordered.IndexOf(rotation);
                        var rotatingStatus = Summary(active.Count, busyCount, stopped.Count, limit, changing: true);
                        Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, rotatingStatus);
                        return rotatingStatus;
                    }
                }
            }

            if (managedActiveCount < limit && stopped.Count > 0)
            {
                var next = FindNextStopped(ordered);
                if (next is not null)
                {
                    try
                    {
                        _pendingStarts[next.FolderPath] = timestamp;
                        await _control.StartAsync(next, cancellationToken);
                        _stopRequestedAt.Remove(next.FolderPath);
                        if (_stoppedByQueue.Remove(next.FolderPath)) PersistOwnedStops();
                        _lastScheduledIndex = ordered.IndexOf(next);
                        _readySince.Remove(next.FolderPath);
                        var startingStatus = UiLanguage.Choose($"Cola: iniciando {next.Alias} · límite {limit}.", $"Queue: starting {next.Alias} · limit {limit}.", $"Cua: iniciant {next.Alias} · límit {limit}.");
                        Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, startingStatus);
                        return startingStatus;
                    }
                    catch (Exception ex)
                    {
                        _pendingStarts.Remove(next.FolderPath);
                        AppLogger.Error($"Runner queue could not start '{next.Alias}'", ex);
                        var startError = UiLanguage.Choose($"Cola: no se pudo iniciar {next.Alias}. Comprueba permisos de administrador.", $"Queue: could not start {next.Alias}. Check administrator permissions.", $"Cua: no s\u0027ha pogut iniciar {next.Alias}. Comprova els permisos d\u0027administrador.");
                        Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, startError);
                        return startError;
                    }
                }
            }

            var summary = Summary(managedActiveCount, busyCount, stopped.Count, limit);
            Snapshot = BuildSnapshot(ordered, settings, resources, RunnerQueueHoldReason.None, summary);
            return summary;
        }
        finally { _gate.Release(); }
    }

    private RunnerQueueSnapshot BuildSnapshot(IReadOnlyList<RunnerInfo> runners, RunnerSettings settings,
        SystemResourceSnapshot? resources, RunnerQueueHoldReason reason, string status)
    {
        var ordered = OrderRunners(runners, settings.RunnerDisplayOrder);
        var running = ordered.Where(runner => runner.State == RunnerState.Busy)
            .Select(runner => runner.Alias).ToList();
        var waiting = ordered.Where(runner => runner.State == RunnerState.Stopped &&
                _eligiblePaths.Contains(runner.FolderPath) &&
                !_pendingStarts.ContainsKey(runner.FolderPath))
            .Select(runner => runner.Alias).ToList();
        var manual = ordered.Where(runner => runner.State == RunnerState.Stopped &&
                !_eligiblePaths.Contains(runner.FolderPath) &&
                !_stoppedByQueue.Contains(runner.FolderPath))
            .Select(runner => runner.Alias).ToList();
        var next = FindNextStopped(ordered)?.Alias ?? string.Empty;
        return new RunnerQueueSnapshot(
            settings.RunnerQueueEnabled,
            IsPaused,
            reason,
            running,
            next,
            waiting,
            manual,
            Math.Clamp(settings.RunnerQueueLimit, 1, 2),
            resources?.CpuPercent,
            MemoryPercent(resources),
            Math.Clamp(settings.RunnerQueueCpuStartThreshold, 50, 100),
            Math.Clamp(settings.RunnerQueueMemoryStartThreshold, 50, 100),
            settings.RunnerQueueResourceGuardEnabled,
            status);
    }

    private static RunnerQueueHoldReason DetermineResourceHold(RunnerSettings settings, SystemResourceSnapshot? resources)
    {
        if (!settings.RunnerQueueResourceGuardEnabled || resources is null)
            return RunnerQueueHoldReason.None;

        var cpuHigh = resources.CpuPercent is double cpu &&
            cpu >= Math.Clamp(settings.RunnerQueueCpuStartThreshold, 50, 100);
        var memoryPercent = MemoryPercent(resources);
        var memoryHigh = memoryPercent is double memory &&
            memory >= Math.Clamp(settings.RunnerQueueMemoryStartThreshold, 50, 100);

        if (cpuHigh && memoryHigh) return RunnerQueueHoldReason.HighCpuAndMemory;
        if (cpuHigh) return RunnerQueueHoldReason.HighCpu;
        if (memoryHigh) return RunnerQueueHoldReason.HighMemory;
        return RunnerQueueHoldReason.None;
    }

    private static double? MemoryPercent(SystemResourceSnapshot? resources)
    {
        if (resources?.UsedMemoryBytes is not ulong used ||
            resources.TotalMemoryBytes is not ulong total || total == 0)
            return null;
        return 100d * used / total;
    }

    private static string ResourceHoldStatus(RunnerQueueHoldReason reason, RunnerSettings settings,
        SystemResourceSnapshot? resources)
    {
        var cpu = resources?.CpuPercent is double cpuValue ? $"{cpuValue:N0}%" : "—";
        var memory = MemoryPercent(resources) is double memoryValue ? $"{memoryValue:N0}%" : "—";
        return reason switch
        {
            RunnerQueueHoldReason.HighCpu => UiLanguage.Choose(
                $"Cola en espera · CPU {cpu} (límite {settings.RunnerQueueCpuStartThreshold}%).",
                $"Queue waiting · CPU {cpu} (limit {settings.RunnerQueueCpuStartThreshold}%).",
                $"Cua en espera · CPU {cpu} (límit {settings.RunnerQueueCpuStartThreshold}%)."),
            RunnerQueueHoldReason.HighMemory => UiLanguage.Choose(
                $"Cola en espera · RAM {memory} (límite {settings.RunnerQueueMemoryStartThreshold}%).",
                $"Queue waiting · RAM {memory} (limit {settings.RunnerQueueMemoryStartThreshold}%).",
                $"Cua en espera · RAM {memory} (límit {settings.RunnerQueueMemoryStartThreshold}%)."),
            _ => UiLanguage.Choose(
                $"Cola en espera · CPU {cpu} / RAM {memory} por encima de los límites.",
                $"Queue waiting · CPU {cpu} / RAM {memory} above limits.",
                $"Cua en espera · CPU {cpu} / RAM {memory} per sobre dels límits.")
        };
    }

    private async Task<StopResult> StopIfStillIdleAsync(RunnerInfo runner, DateTimeOffset timestamp)
    {
        try
        {
            // Journal intent before stopping: a crash between the process stop
            // and the next refresh must never lose the recovery information.
            _stoppedByQueue.Add(runner.FolderPath);
            _stopRequestedAt[runner.FolderPath] = timestamp;
            PersistOwnedStops();
            var stopped = await _control.StopIfIdleAsync(runner);
            if (stopped)
                _readySince.Remove(runner.FolderPath);
            else
            {
                _stoppedByQueue.Remove(runner.FolderPath);
                _stopRequestedAt.Remove(runner.FolderPath);
                PersistOwnedStops();
            }
            return new StopResult(stopped, !stopped);
        }
        catch (Exception ex)
        {
            _stoppedByQueue.Remove(runner.FolderPath);
            _stopRequestedAt.Remove(runner.FolderPath);
            try { PersistOwnedStops(); }
            catch (Exception journalError) { AppLogger.Error("Runner queue journal cleanup failed", journalError); }
            AppLogger.Error($"Runner queue could not stop '{runner.Alias}'", ex);
            return new StopResult(false, false);
        }
    }

    private RunnerInfo? FindNextStopped(IReadOnlyList<RunnerInfo> ordered)
    {
        if (ordered.Count == 0) return null;
        for (var offset = 1; offset <= ordered.Count; offset++)
        {
            var index = (_lastScheduledIndex + offset) % ordered.Count;
            if (ordered[index].State == RunnerState.Stopped &&
                _eligiblePaths.Contains(ordered[index].FolderPath)) return ordered[index];
        }
        return null;
    }

    private static IReadOnlyList<RunnerInfo> OrderRunners(IReadOnlyList<RunnerInfo> runners, IReadOnlyList<string> preferredOrder)
    {
        var ranks = preferredOrder.Select((path, index) => (path, index))
            .ToDictionary(item => item.path, item => item.index, StringComparer.OrdinalIgnoreCase);
        return runners.OrderBy(runner => ranks.TryGetValue(runner.FolderPath, out var rank) ? rank : int.MaxValue)
            .ThenBy(runner => runner.Alias, StringComparer.OrdinalIgnoreCase)
            .ThenBy(runner => runner.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsOnline(RunnerInfo runner) => runner.State is
        RunnerState.Ready or RunnerState.Busy or RunnerState.Starting or RunnerState.Stopping;

    private static string Summary(int active, int busy, int waiting, int limit, bool changing = false, bool waitingForCapacity = false)
    {
        if (waitingForCapacity)
            return UiLanguage.Choose("Cola activa · dejando terminar los jobs activos antes de liberar puestos.", "Queue active · waiting for active jobs to finish before freeing slots.");
        if (changing)
            return UiLanguage.Choose($"Cola activa · preparando {limit} runners a la vez", $"Queue active · preparing {limit} runners at a time", $"Cua activa · preparant {limit} runners alhora");
        return UiLanguage.Choose($"Cola activa · {busy}/{limit} jobs · {waiting} runners en espera", $"Queue active · {busy}/{limit} jobs · {waiting} runners waiting", $"Cua activa · {busy}/{limit} jobs · {waiting} runners en espera");
    }

    private sealed record StopResult(bool Success, bool BecameBusy);
}
