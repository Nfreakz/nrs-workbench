using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Estimates local job progress from the runner's own diagnostic Worker logs.
/// Numeric progress is always explicitly marked as an estimate. Step history is
/// preferred; when a runner version does not expose reliable step boundaries,
/// the service falls back to the median duration of completed local Worker runs.
/// </summary>
public sealed class RunnerProgressService : IRunnerProgressService
{
    private const int MaxHistoricalFiles = 20;

    private static readonly Regex TimestampRegex = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?Z)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StartingRegex = new(
        @"\bStarting:\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FinishingRegex = new(
        @"\bFinishing:\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RunningJobRegex = new(
        @"\bRunning job:\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex JobNameRegex = new(
        @"\bJob(?: display)? name:\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex JobCompletedRegex = new(
        @"\b(?:job (?:completed|finished)|job result|job request .+ processed|finishing job|job completed with result)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Newer runner builds do not always expose useful Starting:/Finishing: lines
    // in the same shape. ProcessInvokerWrapper gives us a second, trustworthy
    // activity signal and lets the UI stop saying "Preparando job" while a real
    // PowerShell/Node/Git process is already running.
    private static readonly Regex ScriptWhichRegex = new(
        @"\bScriptHandler\]\s+Which\d*:\s*'(?<exe>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ScriptLocationRegex = new(
        @"\bScriptHandler\]\s+Location:\s*'(?<exe>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessStartRegex = new(
        @"\bProcessInvokerWrapper\]\s+Starting process:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessFileRegex = new(
        @"\bProcessInvokerWrapper\]\s+File name:\s*'(?<file>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessArgumentsRegex = new(
        @"\bProcessInvokerWrapper\]\s+Arguments:\s*'(?<args>.*)'\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessWorkingDirectoryRegex = new(
        @"\bProcessInvokerWrapper\]\s+Working directory:\s*'(?<cwd>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessStillRunningRegex = new(
        @"\bProcessInvokerWrapper\]\s+Process still running after\s+(?<seconds>\d+)\s+seconds",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcessExitRegex = new(
        @"\bProcessInvokerWrapper\].*(?:process .*(?:finished|completed|exited)|exit code\s*[:=]?\s*-?\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex QueueUploadRegex = new(
        @"\bJobServerQueue\].*(?:upload|append).*(?:log|console|attachment)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ActionPreparationRegex = new(
        @"\b(?:ActionManager|ActionManifestManager|ActionManagerLegacy)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, HistoryCache> _historyCache = new(StringComparer.OrdinalIgnoreCase);

    public RunnerProgress GetProgress(string runnerFolder, RunnerState state)
    {
        if (state != RunnerState.Busy) return RunnerProgress.Inactive;

        try
        {
            var diag = Path.Combine(runnerFolder, "_diag");
            if (!Directory.Exists(diag)) return ActiveWithoutEstimate("Ejecutando job");

            var files = new DirectoryInfo(diag)
                .EnumerateFiles("Worker_*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .Take(MaxHistoricalFiles + 1)
                .ToList();

            if (files.Count == 0) return ActiveWithoutEstimate("Preparando job");

            var currentFile = files[0];
            var current = ParseRun(currentFile, assumeComplete: false);
            if (current is null) return ActiveWithoutEstimate("Ejecutando job");

            var historyFiles = files.Skip(1).Take(MaxHistoricalFiles).ToList();
            var history = GetHistory(diag, historyFiles);
            return BuildProgress(current, history);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not estimate progress for '{runnerFolder}'", ex);
            return ActiveWithoutEstimate("Ejecutando job");
        }
    }

    private IReadOnlyList<ParsedRun> GetHistory(string diag, IReadOnlyList<FileInfo> files)
    {
        var fingerprint = string.Join("|", files.Select(x => $"{x.FullName}:{x.Length}:{x.LastWriteTimeUtc.Ticks}"));
        if (_historyCache.TryGetValue(diag, out var cached) && cached.Fingerprint == fingerprint)
            return cached.Runs;

        var runs = files
            .Select(file => ParseRun(file, assumeComplete: true))
            .Where(run => run is { IsComplete: true } && run.Duration is { TotalSeconds: >= 3 and < 86400 })
            .Cast<ParsedRun>()
            .ToList();

        _historyCache[diag] = new HistoryCache(fingerprint, runs);
        return runs;
    }

    private static RunnerProgress BuildProgress(ParsedRun current, IReadOnlyList<ParsedRun> history)
    {
        var activeStep = current.Steps.LastOrDefault(x => x.FinishedAt is null);
        var phase = activeStep?.Name
                    ?? (!string.IsNullOrWhiteSpace(current.ActivityPhase) ? current.ActivityPhase : null)
                    ?? (current.Steps.Count > 0 ? "Finalizando job" : "Preparando job");

        TimeSpan? elapsed = current.StartedAt is null ? null : DateTimeOffset.UtcNow - current.StartedAt.Value;
        if (elapsed is { } elapsedValue && elapsedValue < TimeSpan.Zero) elapsed = TimeSpan.Zero;

        var completedSteps = current.Steps.Count(x => x.FinishedAt is not null);

        // First choice: step/process sequence history. This is the most useful
        // estimate because long phases are weighted by their historical duration.
        var stepEstimate = TryBuildStepEstimate(current, history, phase, elapsed, completedSteps);
        if (stepEstimate is not null) return stepEstimate;

        // Fallback for runner versions/log shapes where reliable step boundaries
        // are unavailable. Previous Worker files are closed local executions, so
        // their total durations are a useful low-confidence baseline. We still
        // label the result EST. and never let it reach 100% while BUSY.
        var runtimeEstimate = TryBuildRuntimeEstimate(current, history, phase, elapsed, completedSteps);
        if (runtimeEstimate is not null) return runtimeEstimate;

        return new RunnerProgress
        {
            IsActive = true,
            Phase = phase,
            JobName = current.JobName,
            CompletedSteps = completedSteps,
            Elapsed = elapsed
        };
    }

    private static RunnerProgress? TryBuildStepEstimate(
        ParsedRun current,
        IReadOnlyList<ParsedRun> history,
        string phase,
        TimeSpan? elapsed,
        int completedSteps)
    {
        if (current.Steps.Count == 0) return null;

        var currentNames = current.Steps.Select(x => NormalizeStepName(x.Name)).ToList();
        var candidates = history
            .Where(x => JobCompatible(current.JobName, x.JobName))
            .Where(x => x.Steps.Count >= currentNames.Count)
            .Where(x => PrefixMatches(currentNames, x.Steps))
            .ToList();

        // With no job name, one generic process is not enough evidence to claim
        // a step-sequence match. Runtime history can still provide a transparent
        // low-confidence estimate below.
        if (string.IsNullOrWhiteSpace(current.JobName) && currentNames.Count < 2)
            candidates.Clear();

        if (candidates.Count == 0) return null;

        var bestGroup = candidates
            .GroupBy(x => BuildSignature(x.Steps))
            .OrderByDescending(x => x.Count())
            .ThenByDescending(x => x.Max(run => run.LastWriteTimeUtc))
            .First();

        var comparableRuns = bestGroup.ToList();
        var template = comparableRuns[0];
        var expectedDurations = new List<double>(template.Steps.Count);

        for (var i = 0; i < template.Steps.Count; i++)
        {
            var durations = comparableRuns
                .Where(run => run.Steps.Count > i)
                .Select(run => run.Steps[i].Duration?.TotalSeconds ?? 0)
                .Where(seconds => seconds > 0.05 && seconds < 24 * 60 * 60)
                .OrderBy(seconds => seconds)
                .ToList();

            expectedDurations.Add(durations.Count == 0 ? 1 : Median(durations));
        }

        var totalExpected = expectedDurations.Sum();
        if (totalExpected <= 0.1) return null;

        double expectedDone = 0;
        var activeIndex = -1;
        for (var i = 0; i < Math.Min(current.Steps.Count, expectedDurations.Count); i++)
        {
            var step = current.Steps[i];
            if (step.FinishedAt is not null)
            {
                expectedDone += expectedDurations[i];
                continue;
            }

            activeIndex = i;
            var activeElapsed = step.StartedAt is null
                ? 0
                : Math.Max(0, (DateTimeOffset.UtcNow - step.StartedAt.Value).TotalSeconds);

            var effectiveExpected = Math.Max(expectedDurations[i], activeElapsed / 0.85);
            totalExpected += effectiveExpected - expectedDurations[i];
            expectedDone += Math.Min(activeElapsed, effectiveExpected * 0.92);
            break;
        }

        if (activeIndex < 0 && current.Steps.Count >= template.Steps.Count)
            expectedDone = Math.Min(totalExpected * 0.99, totalExpected);

        var percent = Math.Clamp(expectedDone / totalExpected * 100d, 1d, 99d);
        var remainingSeconds = Math.Max(0, totalExpected - expectedDone);

        return new RunnerProgress
        {
            IsActive = true,
            HasEstimate = true,
            Percent = percent,
            Phase = phase,
            JobName = current.JobName,
            CompletedSteps = completedSteps,
            TotalSteps = template.Steps.Count,
            Elapsed = elapsed,
            EstimatedRemaining = TimeSpan.FromSeconds(remainingSeconds),
            HistoricalRuns = comparableRuns.Count,
            EstimateMethod = "Historial de pasos"
        };
    }

    private static RunnerProgress? TryBuildRuntimeEstimate(
        ParsedRun current,
        IReadOnlyList<ParsedRun> history,
        string phase,
        TimeSpan? elapsed,
        int completedSteps)
    {
        if (elapsed is null || elapsed.Value.TotalSeconds < 3) return null;

        var candidates = history
            .Where(run => JobCompatible(current.JobName, run.JobName))
            .Where(run => run.Duration is { TotalSeconds: >= 3 and < 86400 })
            .ToList();

        // If the current log does not expose a job name, the runner-local history
        // is still useful because a self-hosted runner is commonly dedicated to a
        // small set of workflows. Keep this explicitly low-confidence in the UI.
        if (candidates.Count == 0 && string.IsNullOrWhiteSpace(current.JobName))
        {
            candidates = history
                .Where(run => run.Duration is { TotalSeconds: >= 3 and < 86400 })
                .ToList();
        }

        if (candidates.Count == 0) return null;

        var durations = candidates
            .Select(run => run.Duration!.Value.TotalSeconds)
            .OrderBy(seconds => seconds)
            .ToList();

        var expectedSeconds = Median(durations);
        if (expectedSeconds <= 1) return null;

        var elapsedSeconds = Math.Max(0, elapsed.Value.TotalSeconds);
        // Stretch a slow run instead of showing 99% for minutes. Runtime-only
        // estimates are intentionally capped at 95% because they have less
        // structural evidence than step-based estimates.
        var effectiveExpected = Math.Max(expectedSeconds, elapsedSeconds / 0.90);
        var percent = Math.Clamp(elapsedSeconds / effectiveExpected * 100d, 1d, 95d);
        var remainingSeconds = Math.Max(0, effectiveExpected - elapsedSeconds);

        return new RunnerProgress
        {
            IsActive = true,
            HasEstimate = true,
            Percent = percent,
            Phase = phase,
            JobName = current.JobName,
            CompletedSteps = completedSteps,
            Elapsed = elapsed,
            EstimatedRemaining = TimeSpan.FromSeconds(remainingSeconds),
            HistoricalRuns = candidates.Count,
            EstimateMethod = string.IsNullOrWhiteSpace(current.JobName)
                ? "Duración histórica del runner"
                : "Duración histórica del job"
        };
    }

    private static RunnerProgress ActiveWithoutEstimate(string phase) => new()
    {
        IsActive = true,
        Phase = phase
    };

    private static bool JobCompatible(string current, string historical)
    {
        if (string.IsNullOrWhiteSpace(current)) return true;
        return !string.IsNullOrWhiteSpace(historical)
               && string.Equals(current.Trim(), historical.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool PrefixMatches(IReadOnlyList<string> currentNames, IReadOnlyList<ParsedStep> historical)
    {
        for (var i = 0; i < currentNames.Count; i++)
        {
            if (!string.Equals(currentNames[i], NormalizeStepName(historical[i].Name), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static string BuildSignature(IReadOnlyList<ParsedStep> steps) =>
        string.Join("|", steps.Select(x => NormalizeStepName(x.Name)));

    private static string NormalizeStepName(string value)
    {
        var text = value.Trim();
        while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text.ToUpperInvariant();
    }

    private static double Median(IReadOnlyList<double> sorted)
    {
        if (sorted.Count == 0) return 0;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2d : sorted[middle];
    }

    private static ParsedRun? ParseRun(FileInfo file, bool assumeComplete)
    {
        try
        {
            var run = new ParsedRun(file.LastWriteTimeUtc);
            var processSteps = new List<ParsedStep>();
            DateTimeOffset? pendingProcessStart = null;
            ParsedStep? openProcess = null;
            string pendingProcessFile = string.Empty;
            string pendingArguments = string.Empty;

            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);

            while (reader.ReadLine() is { } line)
            {
                var timestamp = ParseTimestamp(line);
                if (timestamp is not null)
                {
                    run.FirstTimestamp ??= timestamp;
                    run.LastTimestamp = timestamp;
                }

                var job = RunningJobRegex.Match(line);
                if (job.Success)
                {
                    run.JobName = CleanName(job.Groups["name"].Value);
                    run.StartedAt ??= timestamp;
                }
                else if (string.IsNullOrWhiteSpace(run.JobName))
                {
                    var jobName = JobNameRegex.Match(line);
                    if (jobName.Success) run.JobName = CleanName(jobName.Groups["name"].Value);
                }

                if (JobCompletedRegex.IsMatch(line))
                {
                    run.CompletedAt = timestamp;
                    run.ActivityPhase = "Finalizando job";
                }

                var start = StartingRegex.Match(line);
                if (start.Success)
                {
                    var name = CleanName(start.Groups["name"].Value);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        run.StartedAt ??= timestamp;
                        run.Steps.Add(new ParsedStep(name, timestamp));
                        run.ActivityPhase = name;
                    }
                    continue;
                }

                var finish = FinishingRegex.Match(line);
                if (finish.Success)
                {
                    var name = NormalizeStepName(CleanName(finish.Groups["name"].Value));
                    for (var i = run.Steps.Count - 1; i >= 0; i--)
                    {
                        if (run.Steps[i].FinishedAt is null && NormalizeStepName(run.Steps[i].Name) == name)
                        {
                            run.Steps[i].FinishedAt = timestamp;
                            break;
                        }
                    }
                    continue;
                }

                var which = ScriptWhichRegex.Match(line);
                var location = ScriptLocationRegex.Match(line);
                if (openProcess is null && (which.Success || location.Success))
                {
                    var executable = which.Success ? which.Groups["exe"].Value : location.Groups["exe"].Value;
                    run.ActivityPhase = $"Preparando {FriendlyExecutableName(executable)}";
                }

                if (ProcessStartRegex.IsMatch(line))
                {
                    if (openProcess is not null && openProcess.FinishedAt is null && timestamp is not null)
                        openProcess.FinishedAt = timestamp;

                    pendingProcessStart = timestamp;
                    pendingProcessFile = string.Empty;
                    pendingArguments = string.Empty;
                    run.StartedAt ??= timestamp;
                    run.ActivityPhase = "Iniciando proceso";
                    continue;
                }

                var processFile = ProcessFileRegex.Match(line);
                if (processFile.Success)
                {
                    pendingProcessFile = processFile.Groups["file"].Value;
                    var processName = $"Ejecutando {FriendlyExecutableName(pendingProcessFile)}";
                    openProcess = new ParsedStep(processName, pendingProcessStart ?? timestamp);
                    processSteps.Add(openProcess);
                    run.ActivityPhase = processName;
                    run.StartedAt ??= pendingProcessStart ?? timestamp;
                    continue;
                }

                var processArgs = ProcessArgumentsRegex.Match(line);
                if (processArgs.Success)
                {
                    pendingArguments = processArgs.Groups["args"].Value;
                    if (openProcess is not null)
                    {
                        var refined = RefineProcessPhase(pendingProcessFile, pendingArguments);
                        if (!string.IsNullOrWhiteSpace(refined))
                        {
                            openProcess.Name = refined;
                            run.ActivityPhase = refined;
                        }
                    }
                    continue;
                }

                var workingDirectory = ProcessWorkingDirectoryRegex.Match(line);
                if (workingDirectory.Success)
                {
                    run.WorkingDirectory = workingDirectory.Groups["cwd"].Value;
                    continue;
                }

                if (ProcessStillRunningRegex.IsMatch(line) && openProcess is not null)
                {
                    run.ActivityPhase = openProcess.Name;
                    continue;
                }

                if (ProcessExitRegex.IsMatch(line))
                {
                    if (openProcess is not null && openProcess.FinishedAt is null)
                        openProcess.FinishedAt = timestamp;
                    openProcess = null;
                    pendingProcessStart = null;
                    run.ActivityPhase = "Procesando resultado";
                    continue;
                }

                if (QueueUploadRegex.IsMatch(line) && openProcess is null)
                {
                    run.ActivityPhase = "Sincronizando logs";
                }
                else if (ActionPreparationRegex.IsMatch(line) && openProcess is null && run.Steps.Count == 0)
                {
                    run.ActivityPhase = "Preparando acciones";
                }
            }

            // Prefer first-class step boundaries when the runner emitted them. If
            // not, ProcessInvokerWrapper executions become synthetic local steps.
            if (run.Steps.Count < 2 && processSteps.Count > 0)
            {
                run.Steps.Clear();
                run.Steps.AddRange(processSteps);
            }

            run.StartedAt ??= run.FirstTimestamp
                              ?? run.Steps.FirstOrDefault()?.StartedAt
                              ?? new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero);

            if (assumeComplete && run.CompletedAt is null)
            {
                // Historical Worker files are no longer the active BUSY file.
                // Their last diagnostic timestamp is therefore a safe local end
                // bound for total-duration learning even if no explicit terminal
                // marker was emitted by that runner version.
                run.CompletedAt = run.LastTimestamp
                                  ?? new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            }

            if (run.CompletedAt is not null)
            {
                var openSteps = run.Steps.Where(x => x.FinishedAt is null).ToList();
                if (openSteps.Count == 1 && openSteps[0].StartedAt is not null && run.CompletedAt >= openSteps[0].StartedAt)
                    openSteps[0].FinishedAt = run.CompletedAt;
            }

            run.IsComplete = run.StartedAt is not null
                             && run.CompletedAt is not null
                             && run.CompletedAt > run.StartedAt;
            return run;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseTimestamp(string line)
    {
        var match = TimestampRegex.Match(line);
        if (!match.Success) return null;
        var raw = match.Groups["ts"].Value.Replace(' ', 'T');
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
    }

    private static string FriendlyExecutableName(string executable)
    {
        var name = Path.GetFileNameWithoutExtension(executable.Trim().Trim('\'', '"')).ToLowerInvariant();
        return name switch
        {
            "powershell" or "pwsh" => "PowerShell",
            "node" => "Node.js",
            "cmd" => "CMD",
            "bash" or "sh" => "Bash",
            "git" => "Git",
            "dotnet" => ".NET",
            "python" or "python3" => "Python",
            "java" => "Java",
            "gradle" or "gradlew" => "Gradle",
            "npm" or "npm-cli" => "npm",
            _ when !string.IsNullOrWhiteSpace(name) => name,
            _ => "proceso"
        };
    }

    private static string RefineProcessPhase(string executable, string arguments)
    {
        var friendly = FriendlyExecutableName(executable);
        if (friendly == "PowerShell")
        {
            if (arguments.Contains("deploy", StringComparison.OrdinalIgnoreCase)) return "Ejecutando despliegue PowerShell";
            if (arguments.Contains("build", StringComparison.OrdinalIgnoreCase)) return "Ejecutando build PowerShell";
        }
        else if (friendly == "npm")
        {
            if (arguments.Contains("build", StringComparison.OrdinalIgnoreCase)) return "Ejecutando npm build";
            if (arguments.Contains("test", StringComparison.OrdinalIgnoreCase)) return "Ejecutando npm test";
        }
        else if (friendly == ".NET")
        {
            if (arguments.Contains("build", StringComparison.OrdinalIgnoreCase)) return "Compilando con .NET";
            if (arguments.Contains("test", StringComparison.OrdinalIgnoreCase)) return "Ejecutando tests .NET";
            if (arguments.Contains("publish", StringComparison.OrdinalIgnoreCase)) return "Publicando con .NET";
        }

        return $"Ejecutando {friendly}";
    }

    private static string CleanName(string value)
    {
        var text = value.Trim();
        return text.Length > 140 ? text[..140] + "…" : text;
    }

    private sealed record HistoryCache(string Fingerprint, IReadOnlyList<ParsedRun> Runs);

    private sealed class ParsedRun
    {
        public ParsedRun(DateTime lastWriteTimeUtc) => LastWriteTimeUtc = lastWriteTimeUtc;
        public DateTime LastWriteTimeUtc { get; }
        public string JobName { get; set; } = string.Empty;
        public string ActivityPhase { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public DateTimeOffset? FirstTimestamp { get; set; }
        public DateTimeOffset? LastTimestamp { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public List<ParsedStep> Steps { get; } = [];
        public bool IsComplete { get; set; }
        public TimeSpan? Duration => StartedAt is null || CompletedAt is null ? null : CompletedAt.Value - StartedAt.Value;
    }

    private sealed class ParsedStep
    {
        public ParsedStep(string name, DateTimeOffset? startedAt)
        {
            Name = name;
            StartedAt = startedAt;
        }

        public string Name { get; set; }
        public DateTimeOffset? StartedAt { get; }
        public DateTimeOffset? FinishedAt { get; set; }
        public TimeSpan? Duration => StartedAt is null || FinishedAt is null ? null : FinishedAt.Value - StartedAt.Value;
    }
}
