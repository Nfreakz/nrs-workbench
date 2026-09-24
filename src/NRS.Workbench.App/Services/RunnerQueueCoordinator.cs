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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _lastScheduledIndex = -1;
    private bool _queueWasEnabled;

    public RunnerQueueCoordinator(IRunnerControlService control) => _control = control;

    public async Task<string> ReconcileAsync(IReadOnlyList<RunnerInfo> runners, RunnerSettings settings,
        DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!settings.RunnerQueueEnabled)
            {
                if (!_queueWasEnabled) return string.Empty;
                _readySince.Clear();
                var stopped = runners.Where(runner => runner.State == RunnerState.Stopped).ToList();
                var outcomes = await Task.WhenAll(stopped.Select(async runner =>
                {
                    try { await _control.StartAsync(runner, cancellationToken); return true; }
                    catch (Exception ex) { AppLogger.Error($"Runner queue could not restore '{runner.Alias}'", ex); return false; }
                }));
                _queueWasEnabled = false;
                return outcomes.Any(success => !success)
                    ? UiLanguage.Choose("Cola desactivada · algunos runners no se pudieron iniciar.", "Queue disabled · some runners could not be started.")
                    : UiLanguage.Choose("Cola desactivada · runners reiniciados.", "Queue disabled · runners restarted.");
            }

            _queueWasEnabled = true;
            var timestamp = now ?? DateTimeOffset.UtcNow;
            var ordered = OrderRunners(runners, settings.RunnerDisplayOrder).ToList();
            var knownPaths = ordered.Select(runner => runner.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var stale in _readySince.Keys.Where(path => !knownPaths.Contains(path)).ToList())
                _readySince.Remove(stale);
            foreach (var stale in _pendingStarts.Keys.Where(path => !knownPaths.Contains(path)).ToList())
                _pendingStarts.Remove(stale);

            foreach (var runner in ordered)
            {
                if (runner.State != RunnerState.Stopped ||
                    (_pendingStarts.TryGetValue(runner.FolderPath, out var requestedAt) && timestamp - requestedAt >= TimeSpan.FromSeconds(30)))
                    _pendingStarts.Remove(runner.FolderPath);
                if (runner.State == RunnerState.Ready)
                    _readySince.TryAdd(runner.FolderPath, timestamp);
                else
                    _readySince.Remove(runner.FolderPath);
            }

            var limit = Math.Clamp(settings.RunnerQueueLimit, 1, 2);
            var active = ordered.Where(IsOnline).ToList();
            var pendingStartCount = _pendingStarts.Keys.Count(path => ordered.Any(runner =>
                string.Equals(runner.FolderPath, path, StringComparison.OrdinalIgnoreCase) && runner.State == RunnerState.Stopped));
            var busyCount = active.Count(runner => runner.State == RunnerState.Busy);
            var stopped = ordered.Where(runner => runner.State == RunnerState.Stopped && !_pendingStarts.ContainsKey(runner.FolderPath)).ToList();
            var managedActiveCount = active.Count + pendingStartCount;

            // When enabling the queue with more listeners already online, drain
            // idle listeners first. Busy jobs are never stopped by the queue.
            var excess = managedActiveCount - limit;
            if (excess > 0)
            {
                var idleToStop = ordered.Where(runner => runner.State == RunnerState.Ready)
                    .Reverse().Take(excess).ToList();
                if (idleToStop.Count == 0)
                    return Summary(active.Count, busyCount, stopped.Count, limit, waitingForCapacity: true);

                var stoppedResults = await Task.WhenAll(idleToStop.Select(StopIfStillIdleAsync));
                var failures = stoppedResults.Where(result => !result.Success && !result.BecameBusy).ToList();
                if (failures.Count > 0)
                    return UiLanguage.Choose("Cola: no se pudo detener un runner libre. Comprueba permisos de administrador.", "Queue: could not stop an idle runner. Check administrator permissions.");
                return Summary(active.Count, busyCount, stopped.Count, limit, changing: true);
            }

            // Rotate an idle slot after a grace period. This lets jobs for other
            // GitHub targets/labels progress without stopping a running job.
            if (stopped.Count > 0 && managedActiveCount >= limit)
            {
                var rotation = ordered.Where(runner => runner.State == RunnerState.Ready &&
                        _readySince.TryGetValue(runner.FolderPath, out var since) && timestamp - since >= IdleRotationDelay)
                    .OrderBy(runner => _readySince[runner.FolderPath])
                    .FirstOrDefault();
                if (rotation is not null)
                {
                    var result = await StopIfStillIdleAsync(rotation);
                    if (!result.Success && !result.BecameBusy)
                        return UiLanguage.Choose($"Cola: no se pudo rotar {rotation.Alias}. Comprueba permisos de administrador.", $"Queue: could not rotate {rotation.Alias}. Check administrator permissions.");
                    if (result.Success)
                    {
                        _lastScheduledIndex = ordered.IndexOf(rotation);
                        return Summary(active.Count, busyCount, stopped.Count, limit, changing: true);
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
                        _lastScheduledIndex = ordered.IndexOf(next);
                        _readySince.Remove(next.FolderPath);
                        return UiLanguage.Choose($"Cola: iniciando {next.Alias} · límite {limit}.", $"Queue: starting {next.Alias} · limit {limit}.");
                    }
                    catch (Exception ex)
                    {
                        _pendingStarts.Remove(next.FolderPath);
                        AppLogger.Error($"Runner queue could not start '{next.Alias}'", ex);
                        return UiLanguage.Choose($"Cola: no se pudo iniciar {next.Alias}. Comprueba permisos de administrador.", $"Queue: could not start {next.Alias}. Check administrator permissions.");
                    }
                }
            }

            return Summary(managedActiveCount, busyCount, stopped.Count, limit);
        }
        finally { _gate.Release(); }
    }

    private async Task<StopResult> StopIfStillIdleAsync(RunnerInfo runner)
    {
        try
        {
            var stopped = await _control.StopIfIdleAsync(runner);
            if (stopped) _readySince.Remove(runner.FolderPath);
            return new StopResult(stopped, !stopped);
        }
        catch (Exception ex)
        {
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
            if (ordered[index].State == RunnerState.Stopped) return ordered[index];
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
            return UiLanguage.Choose($"Cola activa · preparando {limit} runners a la vez", $"Queue active · preparing {limit} runners at a time");
        return UiLanguage.Choose($"Cola activa · {busy}/{limit} jobs · {waiting} runners en espera", $"Queue active · {busy}/{limit} jobs · {waiting} runners waiting");
    }

    private sealed record StopResult(bool Success, bool BecameBusy);
}
