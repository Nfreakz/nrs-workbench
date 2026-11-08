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

    public string PhaseDisplay => string.IsNullOrWhiteSpace(Phase) ? DisplayText.Choose("Ejecutando job", "Running job", "Executant el job") : Phase;
    public string JobDisplay => string.IsNullOrWhiteSpace(JobName) ? DisplayText.Choose("Job de GitHub Actions", "GitHub Actions job", "Job de GitHub Actions") : JobName;
    public string PercentDisplay => HasEstimate ? $"{Percent:N0}% EST." : DisplayText.Choose("EN CURSO", "IN PROGRESS", "EN CURS");

    public string ConfidenceDisplay => !HasEstimate ? string.Empty : HistoricalRuns switch
    {
        >= 4 => DisplayText.Choose("ALTA", "HIGH", "ALTA"),
        >= 2 => DisplayText.Choose("MEDIA", "AVERAGE", "MITJANA"),
        _ => DisplayText.Choose("BAJA", "LOW", "BAIXA")
    };

    public string StepsDisplay
    {
        get
        {
            if (TotalSteps is > 0)
            {
                var done = $"{Math.Min(CompletedSteps, TotalSteps.Value)}/{TotalSteps.Value}";
                return DisplayText.Choose($"{done} pasos", $"{done} steps", $"{done} passos");
            }
            if (CompletedSteps == 1) return DisplayText.Choose("1 paso completado", "1 step completed", "1 pas completat");
            if (CompletedSteps > 1) return DisplayText.Choose($"{CompletedSteps} pasos completados", $"{CompletedSteps} steps completed", $"{CompletedSteps} passos completats");
            return DisplayText.Choose("Analizando actividad", "Analyzing activity", "Analitzant l'activitat");
        }
    }

    public string ElapsedDisplay => FormatDuration(Elapsed);
    public string RemainingDisplay => EstimatedRemaining is null ? string.Empty : DisplayText.Choose($"~{FormatDuration(EstimatedRemaining)} restante", $"~{FormatDuration(EstimatedRemaining)} remaining", $"~{FormatDuration(EstimatedRemaining)} restant");

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
            else if (CompletedSteps > 0) parts.Add(CompletedSteps == 1
                ? DisplayText.Choose("1 proceso", "1 process", "1 procés")
                : DisplayText.Choose($"{CompletedSteps} procesos", $"{CompletedSteps} processes", $"{CompletedSteps} processos"));
            if (!string.IsNullOrWhiteSpace(RemainingDisplay)) parts.Add(RemainingDisplay);
            else if (!string.IsNullOrWhiteSpace(ElapsedDisplay)) parts.Add(ElapsedDisplay);
            return parts.Count == 0 ? DisplayText.Choose("Analizando", "Analyzing", "Analitzant") : string.Join(" · ", parts);
        }
    }

    public string EstimateSourceDisplay
    {
        get
        {
            if (!HasEstimate) return DisplayText.Choose("Sin historial comparable todavía", "No comparable history yet", "Encara no hi ha historial comparable");
            var source = string.IsNullOrWhiteSpace(EstimateMethod) ? DisplayText.Choose("Estimación local", "Local estimate", "Estimació local") : EstimateMethod;
            var runs = HistoricalRuns == 1
                ? DisplayText.Choose("1 ejecución comparable", "1 comparable run", "1 execució comparable")
                : DisplayText.Choose($"{HistoricalRuns} ejecuciones comparables", $"{HistoricalRuns} comparable runs", $"{HistoricalRuns} execucions comparables");
            return $"{source} · {DisplayText.Choose("confianza", "confidence", "confiança")} {ConfidenceDisplay} · {runs}";
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
