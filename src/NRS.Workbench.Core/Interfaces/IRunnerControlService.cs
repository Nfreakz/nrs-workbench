using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Interfaces;

public interface IRunnerControlService
{
    Task StartAsync(RunnerInfo runner, CancellationToken cancellationToken = default);
    Task StopAsync(RunnerInfo runner, CancellationToken cancellationToken = default);
    Task RestartAsync(RunnerInfo runner, CancellationToken cancellationToken = default);
}
