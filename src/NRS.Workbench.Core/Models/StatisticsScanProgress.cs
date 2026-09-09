namespace NRS.Workbench.Core.Models;

/// <summary>
/// Progress reported while the local Worker log history is being scanned.
/// </summary>
public sealed record StatisticsScanProgress
{
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public string RunnerAlias { get; init; } = string.Empty;

    public double Percent => TotalFiles <= 0 ? 100d : Math.Clamp(ProcessedFiles * 100d / TotalFiles, 0d, 100d);
}
