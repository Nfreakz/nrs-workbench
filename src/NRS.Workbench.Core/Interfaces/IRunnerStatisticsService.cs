using NRS.Workbench.Core.Models;

namespace NRS.Workbench.Core.Interfaces;

public interface IRunnerStatisticsService
{
    Task<RunnerStatisticsSnapshot> GetSnapshotAsync(
        IReadOnlyList<RunnerInfo> runners,
        StatisticsPeriod period,
        IProgress<StatisticsScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
