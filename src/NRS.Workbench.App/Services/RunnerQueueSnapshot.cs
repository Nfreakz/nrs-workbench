namespace NRS.Workbench.App.Services;

public enum RunnerQueueHoldReason
{
    None,
    Disabled,
    Paused,
    Recovering,
    BusyCapacity,
    HighCpu,
    HighMemory,
    HighCpuAndMemory
}

public sealed record RunnerQueueSnapshot(
    bool Enabled,
    bool Paused,
    RunnerQueueHoldReason HoldReason,
    IReadOnlyList<string> RunningAliases,
    string NextAlias,
    IReadOnlyList<string> WaitingAliases,
    IReadOnlyList<string> ManualStoppedAliases,
    int Limit,
    double? CpuPercent,
    double? MemoryPercent,
    int CpuThreshold,
    int MemoryThreshold,
    int CpuReleaseThreshold,
    int MemoryReleaseThreshold,
    bool ResourceGuardEnabled,
    string StatusText)
{
    public static RunnerQueueSnapshot Disabled(string status = "") => new(
        false, false, RunnerQueueHoldReason.Disabled,
        [], string.Empty, [], [], 0, null, null, 0, 0, 0, 0, false, status);
}
