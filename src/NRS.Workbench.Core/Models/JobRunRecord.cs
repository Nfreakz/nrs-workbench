namespace NRS.Workbench.Core.Models;

public sealed record JobRunRecord
{
    public required string RunnerAlias { get; init; }
    public string GitHubTarget { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public JobRunOutcome Outcome { get; init; }
    public string SourceFile { get; init; } = string.Empty;

    // Stable local identity when the diagnostic stream exposes one. This is used
    // only for conservative duplicate protection. A missing identity never causes
    // two Worker logs to be collapsed.
    public string ExecutionId { get; init; } = string.Empty;
    public string IdentityKind { get; init; } = string.Empty;
    public int SourceLogCount { get; init; } = 1;

    public string StartedAtDisplay => StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
    public string JobDisplay => string.IsNullOrWhiteSpace(JobName) ? "Job de GitHub Actions" : JobName;
    public string OutcomeDisplay => Outcome switch
    {
        JobRunOutcome.Succeeded => "OK",
        JobRunOutcome.Failed => "FALLO",
        JobRunOutcome.Cancelled => "CANCELADO",
        _ => "SIN CLASIFICAR"
    };
    public string DurationDisplay => FormatDuration(Duration);
    public string IdentityDisplay => string.IsNullOrWhiteSpace(ExecutionId) ? "—" : ExecutionId;

    internal static string FormatDuration(TimeSpan duration)
    {
        var span = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{Math.Max(0, (int)Math.Round(span.TotalSeconds))}s";
    }
}
