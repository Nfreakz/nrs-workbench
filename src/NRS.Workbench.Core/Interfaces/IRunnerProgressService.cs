using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Interfaces;

public interface IRunnerProgressService
{
    RunnerProgress GetProgress(string runnerFolder, RunnerState state);
}
