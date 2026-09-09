namespace NRS.Workbench.Core.Models;

public sealed record RunnerStatisticsSnapshot
{
    public StatisticsPeriod Period { get; init; }
    public int TotalRuns { get; init; }
    public int SucceededRuns { get; init; }
    public int FailedRuns { get; init; }
    public int CancelledRuns { get; init; }
    public int UnknownRuns { get; init; }
    public int ActiveRuns { get; init; }
    public double SuccessRate { get; init; }
    public double ReliabilityRate { get; init; }
    public double CancellationRate { get; init; }
    public double? Trend24h { get; init; }
    public string MostActiveRunnerLabel { get; init; } = "—";
    public TimeSpan TotalDuration { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public int RunnersWithActivity { get; init; }

    public DateTimeOffset? FirstRunAt { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public int RunsToday { get; init; }
    public int RunsLast24Hours { get; init; }
    public double JobsPerDay { get; init; }
    public string PeakDayLabel { get; init; } = "—";
    public int PeakDayRuns { get; init; }

    // Audit trail for the local statistics pipeline.
    public int WorkerLogsScanned { get; init; }
    public int ParsedWorkerLogs { get; init; }
    public int StableIdentityRuns { get; init; }
    public int DuplicateLogsDiscarded { get; init; }

    public IReadOnlyList<RunnerStatisticsRow> ByRunner { get; init; } = Array.Empty<RunnerStatisticsRow>();
    public IReadOnlyList<JobRunRecord> RecentRuns { get; init; } = Array.Empty<JobRunRecord>();
    public IReadOnlyList<DailyActivityPoint> DailyActivity { get; init; } = Array.Empty<DailyActivityPoint>();

    public string SuccessRateDisplay => KnownRuns == 0 ? "—" : $"{SuccessRate:N1}%";
    public string ReliabilityRateDisplay => TechnicalRuns == 0 ? "—" : $"{ReliabilityRate:N1}%";
    public string CancellationRateDisplay => KnownRuns == 0 ? "—" : $"{CancellationRate:N1}%";
    public string Trend24hDisplay => Trend24h is null ? "—" : Math.Abs(Trend24h.Value) < 0.05d ? "→ 0,0 pp" : Trend24h.Value > 0d ? $"↑ +{Trend24h.Value:N1} pp" : $"↓ {Trend24h.Value:N1} pp";
    public string Trend24hKind => Trend24h is null ? "Unknown" : Math.Abs(Trend24h.Value) < 0.05d ? "Flat" : Trend24h.Value > 0d ? "Up" : "Down";
    public string TotalDurationDisplay => JobRunRecord.FormatDuration(TotalDuration);
    public string AverageDurationDisplay => TotalRuns == 0 ? "—" : JobRunRecord.FormatDuration(AverageDuration);
    public string FirstRunDisplay => FirstRunAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string LastRunDisplay => LastRunAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public string CoverageDisplay
    {
        get
        {
            if (FirstRunAt is null || LastRunAt is null) return "—";
            var span = LastRunAt.Value - FirstRunAt.Value;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{Math.Max(0, (int)span.TotalMinutes)}m";
        }
    }
    public string JobsPerDayDisplay => TotalRuns == 0 ? "—" : $"{JobsPerDay:N1}";
    public string PeakDayDisplay => PeakDayRuns == 0 ? "—" : $"{PeakDayLabel} · {PeakDayRuns:N0} jobs";
    public int KnownRuns => SucceededRuns + FailedRuns + CancelledRuns;
    public int TechnicalRuns => SucceededRuns + FailedRuns;
}
