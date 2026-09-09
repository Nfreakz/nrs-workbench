namespace NRS.Workbench.Core.Models;

public sealed record RunnerStatisticsRow
{
    public required string RunnerAlias { get; init; }
    public string GitHubTarget { get; init; } = string.Empty;
    public int TotalRuns { get; init; }
    public int SucceededRuns { get; init; }
    public int FailedRuns { get; init; }
    public int CancelledRuns { get; init; }
    public int UnknownRuns { get; init; }
    public bool IsBusy { get; init; }
    public double SuccessRate { get; init; }
    public double ReliabilityRate { get; init; }
    public double CancellationRate { get; init; }
    public double WorkloadShare { get; init; }
    public double? Trend24h { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }

    public string SuccessRateDisplay => KnownRuns == 0 ? "—" : $"{SuccessRate:N1}%";
    public string ReliabilityDisplay => TechnicalRuns == 0 ? "—" : $"{ReliabilityRate:N1}%";
    public string CancellationRateDisplay => KnownRuns == 0 ? "—" : $"{CancellationRate:N1}%";
    public string WorkloadShareDisplay => TotalRuns == 0 ? "—" : $"{WorkloadShare:N1}%";
    public string ReliabilityLabel => TechnicalRuns < 5 ? "SIN DATOS" : ReliabilityRate >= 90d ? "ALTA" : ReliabilityRate >= 75d ? "MEDIA" : "BAJA";
    public string ReliabilityKind => TechnicalRuns < 5 ? "Unknown" : ReliabilityRate >= 90d ? "High" : ReliabilityRate >= 75d ? "Medium" : "Low";
    public string Trend24hDisplay => Trend24h is null ? "—" : Math.Abs(Trend24h.Value) < 0.05d ? "→ 0,0 pp" : Trend24h.Value > 0d ? $"↑ +{Trend24h.Value:N1} pp" : $"↓ {Trend24h.Value:N1} pp";
    public string Trend24hKind => Trend24h is null ? "Unknown" : Math.Abs(Trend24h.Value) < 0.05d ? "Flat" : Trend24h.Value > 0d ? "Up" : "Down";
    public string TotalDurationDisplay => JobRunRecord.FormatDuration(TotalDuration);
    public string AverageDurationDisplay => TotalRuns == 0 ? "—" : JobRunRecord.FormatDuration(AverageDuration);
    public string LastRunDisplay => LastRunAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string ActiveDisplay => IsBusy ? "BUSY" : "—";
    public int KnownRuns => SucceededRuns + FailedRuns + CancelledRuns;
    public int TechnicalRuns => SucceededRuns + FailedRuns;
}
