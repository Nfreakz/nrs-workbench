using System.Diagnostics;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class RunnerControlService : IRunnerControlService
{
    private readonly RunnerProcessService _processService;
    private readonly WindowsServiceController _serviceController;

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
                if (snapshot.HasWorker) return false;
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

        var runCmd = Path.Combine(runner.FolderPath, "run.cmd");
        if (!File.Exists(runCmd)) throw new FileNotFoundException("No se encuentra run.cmd.", runCmd);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = runner.FolderPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("run.cmd");
        Process.Start(psi);
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
