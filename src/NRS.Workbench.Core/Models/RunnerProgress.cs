namespace NRS.Workbench.Core.Models;

public sealed record RunnerProgress
{
    public static RunnerProgress Inactive { get; } = new();

    public bool IsActive { get; init; }
    public bool HasEstimate { get; init; }
    public double Percent { get; init; }
    public string Phase { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public int CompletedSteps { get; init; }
    public int? TotalSteps { get; init; }
    public TimeSpan? Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public int HistoricalRuns { get; init; }
    public string EstimateMethod { get; init; } = string.Empty;

    public string PhaseDisplay => string.IsNullOrWhiteSpace(Phase) ? "Ejecutando job" : Phase;
    public string JobDisplay => string.IsNullOrWhiteSpace(JobName) ? "Job de GitHub Actions" : JobName;
    public string PercentDisplay => HasEstimate ? $"{Percent:N0}% EST." : "EN CURSO";

    public string ConfidenceDisplay => !HasEstimate ? string.Empty : HistoricalRuns switch
    {
        >= 4 => "ALTA",
        >= 2 => "MEDIA",
        _ => "BAJA"
    };

    public string StepsDisplay
    {
        get
        {
            if (TotalSteps is > 0) return $"{Math.Min(CompletedSteps, TotalSteps.Value)}/{TotalSteps.Value} pasos";
            if (CompletedSteps == 1) return "1 paso completado";
            if (CompletedSteps > 1) return $"{CompletedSteps} pasos completados";
            return "Analizando actividad";
        }
    }

    public string ElapsedDisplay => FormatDuration(Elapsed);
    public string RemainingDisplay => EstimatedRemaining is null ? string.Empty : $"~{FormatDuration(EstimatedRemaining)} restante";

    public string MetaDisplay
    {
        get
        {
            var parts = new List<string> { StepsDisplay };
            if (!string.IsNullOrWhiteSpace(ElapsedDisplay)) parts.Add(ElapsedDisplay);
            if (!string.IsNullOrWhiteSpace(RemainingDisplay)) parts.Add(RemainingDisplay);
            return string.Join(" · ", parts);
        }
    }

    public string CompactMetaDisplay
    {
        get
        {
            var parts = new List<string>();
            if (TotalSteps is > 0) parts.Add($"{Math.Min(CompletedSteps, TotalSteps.Value)}/{TotalSteps.Value}");
            else if (CompletedSteps > 0) parts.Add($"{CompletedSteps} proceso{(CompletedSteps == 1 ? "" : "s")}");
            if (!string.IsNullOrWhiteSpace(RemainingDisplay)) parts.Add(RemainingDisplay);
            else if (!string.IsNullOrWhiteSpace(ElapsedDisplay)) parts.Add(ElapsedDisplay);
            return parts.Count == 0 ? "Analizando" : string.Join(" · ", parts);
        }
    }

    public string EstimateSourceDisplay
    {
        get
        {
            if (!HasEstimate) return "Sin historial comparable todavía";
            var source = string.IsNullOrWhiteSpace(EstimateMethod) ? "Estimación local" : EstimateMethod;
            return $"{source} · confianza {ConfidenceDisplay} · {HistoricalRuns} ejecución{(HistoricalRuns == 1 ? "" : "es")} comparable{(HistoricalRuns == 1 ? "" : "s")}";
        }
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null) return string.Empty;
        var span = duration.Value < TimeSpan.Zero ? TimeSpan.Zero : duration.Value;
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{Math.Max(0, (int)Math.Round(span.TotalSeconds))}s";
    }
}
