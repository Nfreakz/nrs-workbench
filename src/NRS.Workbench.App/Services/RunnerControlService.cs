using System.Diagnostics;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class RunnerControlService : IRunnerControlService
{
    private readonly RunnerProcessService _processService;
    private readonly WindowsServiceController _serviceController;

    // run.cmd needs several seconds (file unblock + listener start) before Runner.Listener.exe
    // is visible, so a second start in that window would not see the first one. Remember
    // recent interactive starts per folder and refuse duplicates: two listeners with the same
    // registration make GitHub answer "A session for this runner already exists".
    private static readonly TimeSpan StartGracePeriod = TimeSpan.FromSeconds(60);
    private readonly Dictionary<string, DateTime> _recentStarts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _startLock = new();

    public RunnerControlService(RunnerProcessService processService, WindowsServiceController serviceController)
    {
        _processService = processService;
        _serviceController = serviceController;
    }

    public Task StartAsync(RunnerInfo runner, CancellationToken cancellationToken = default) =>
        Task.Run(() => Start(runner), cancellationToken);

    public Task StopAsync(RunnerInfo runner, CancellationToken cancellationToken = default) =>
        Task.Run(() => Stop(runner), cancellationToken);

    public Task<bool> StopIfIdleAsync(RunnerInfo runner, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            // Read the process snapshot immediately before issuing the stop. The
            // dashboard snapshot may already be stale by the time reconciliation
            // reaches this runner.
            var snapshot = _processService.GetSnapshot(runner.FolderPath);
            try
            {
                if (snapshot.HasWorker || snapshot.Unknown) return false;
                if (runner.Mode == RunnerMode.Service && !string.IsNullOrWhiteSpace(runner.ServiceName))
                {
                    _serviceController.Stop(runner.ServiceName);
                    return true;
                }

                if (snapshot.Listener is null) return true;
                // Do not kill the listener's process tree here. A worker that
                // starts in the small interval after this check must be allowed
                // to finish instead of being terminated with its parent.
                snapshot.Listener.Kill(entireProcessTree: false);
                snapshot.Listener.WaitForExit(5000);
                return true;
            }
            finally
            {
                snapshot.Listener?.Dispose();
                foreach (var worker in snapshot.Workers) worker.Dispose();
            }
        }, cancellationToken);

    public async Task RestartAsync(RunnerInfo runner, CancellationToken cancellationToken = default)
    {
        await StopAsync(runner, cancellationToken);
        await Task.Delay(600, cancellationToken);
        await StartAsync(runner, cancellationToken);
    }

    private void Start(RunnerInfo runner)
    {
        if (runner.Mode == RunnerMode.Service && !string.IsNullOrWhiteSpace(runner.ServiceName))
        {
            _serviceController.Start(runner.ServiceName);
            return;
        }

        // run.cmd (not Runner.Listener.exe) keeps the runner's own self-update loop working.
        // Anything in the runner folder is writable by jobs, so starting it from an elevated
        // Workbench runs job-controlled code elevated; prefer running Workbench unelevated.
        var runCmd = Path.Combine(runner.FolderPath, "run.cmd");
        if (!File.Exists(runCmd)) throw new FileNotFoundException("No se encuentra run.cmd.", runCmd);

        lock (_startLock)
        {
            var now = DateTime.UtcNow;
            if (_recentStarts.TryGetValue(runner.FolderPath, out var startedAt) && now - startedAt < StartGracePeriod)
            {
                AppLogger.Info($"Start ignored: {runner.Alias} was started {(now - startedAt).TotalSeconds:0} s ago.");
                return;
            }

            var snapshot = _processService.GetSnapshot(runner.FolderPath);
            var alreadyRunning = snapshot.HasListener;
            snapshot.Listener?.Dispose();
            foreach (var worker in snapshot.Workers) worker.Dispose();
            if (alreadyRunning)
            {
                AppLogger.Info($"Start ignored: {runner.Alias} already has a running listener.");
                return;
            }

            _recentStarts[runner.FolderPath] = now;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = runner.FolderPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("run.cmd");
        using var _ = Process.Start(psi);
    }

    private void Stop(RunnerInfo runner)
    {
        if (runner.Mode == RunnerMode.Service && !string.IsNullOrWhiteSpace(runner.ServiceName))
        {
            _serviceController.Stop(runner.ServiceName);
            return;
        }

        var snapshot = _processService.GetSnapshot(runner.FolderPath);
        try
        {
            if (snapshot.Listener is not null)
            {
                snapshot.Listener.Kill(entireProcessTree: true);
                snapshot.Listener.WaitForExit(5000);
            }
            else
            {
                foreach (var worker in snapshot.Workers)
                {
                    try { worker.Kill(entireProcessTree: true); } catch { }
                }
            }
        }
        finally
        {
            snapshot.Listener?.Dispose();
            foreach (var worker in snapshot.Workers) worker.Dispose();
        }
    }
}
