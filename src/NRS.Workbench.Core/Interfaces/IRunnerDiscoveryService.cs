using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Interfaces;

public interface IRunnerDiscoveryService
{
    IReadOnlyList<RunnerInfo> Discover();
}
