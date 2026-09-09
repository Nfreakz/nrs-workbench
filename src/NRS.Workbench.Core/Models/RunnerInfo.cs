namespace NRS.Workbench.Core.Models;

public sealed record RunnerInfo
{
    public required string Alias { get; init; }
    public required string FolderPath { get; init; }
    public string AgentName { get; init; } = string.Empty;
    public string GitHubTarget { get; init; } = string.Empty;
    public string GitHubUrl { get; init; } = string.Empty;
    public string Labels { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public RunnerMode Mode { get; init; }
    public RunnerState State { get; init; }
    public int? ListenerPid { get; init; }
    public IReadOnlyList<int> WorkerPids { get; init; } = Array.Empty<int>();
    public long RamBytes { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public string StatusDetail { get; init; } = string.Empty;
    public RunnerProgress Progress { get; init; } = RunnerProgress.Inactive;

    public string PidDisplay => ListenerPid?.ToString() ?? string.Empty;
    public string ModeDisplay => Mode switch
    {
        RunnerMode.Interactive => "Interactivo",
        RunnerMode.Service => "Servicio",
        _ => "Desconocido"
    };
    public string RamDisplay => RamBytes <= 0 ? string.Empty : $"{RamBytes / 1024d / 1024d:N0} MB";
    public string UptimeDisplay
    {
        get
        {
            if (StartedAt is null) return string.Empty;
            var span = DateTimeOffset.Now - StartedAt.Value;
            if (span.TotalSeconds < 0) return string.Empty;
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
            if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
            return $"{Math.Max(0, (int)span.TotalSeconds)}s";
        }
    }
}
