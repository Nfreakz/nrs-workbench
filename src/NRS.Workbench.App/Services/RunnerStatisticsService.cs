using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Builds local job statistics exclusively from GitHub Runner Worker logs.
///
/// GitHub Runner writes one Worker diagnostic stream for each processed job. The
/// service therefore treats each parsed Worker log as one job by default. It only
/// collapses records when the diagnostics expose the same stable execution identity
/// and the timestamps are close enough to prove they are duplicate copies of the
/// same execution. Missing IDs never cause heuristic time/name deduplication.
///
/// The service never reads runner credentials and never treats arbitrary ERROR lines
/// as a failed job. Failure/success is counted only when a terminal job result can be
/// identified; otherwise the run remains Unknown.
/// </summary>
public sealed class RunnerStatisticsService : IRunnerStatisticsService
{
    private const string GuidPattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
    private static readonly TimeSpan DuplicateStartTolerance = TimeSpan.FromSeconds(90);

    private static readonly Regex TimestampRegex = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?Z)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JobNameRegex = new(
        @"\b(?:Running job|Job(?: display)? name|JobDisplayName)\s*[:=]\s*['""]?(?<name>.+?)['""]?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JsonJobNameRegex = new(
        @"[""'](?:jobDisplayName|jobName)[""']\s*:\s*[""'](?<name>[^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JobIdRegex = new(
        $@"\b(?:jobId|job_id|job\s+id)\b[""']?\s*[:=]\s*[""']?(?<id>{GuidPattern})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PlanIdRegex = new(
        $@"\b(?:planId|plan_id|plan\s+id)\b[""']?\s*[:=]\s*[""']?(?<id>{GuidPattern})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TimelineIdRegex = new(
        $@"\b(?:timelineId|timeline_id|timeline\s+id)\b[""']?\s*[:=]\s*[""']?(?<id>{GuidPattern})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RequestIdRegex = new(
        $@"\b(?:runnerRequestId|requestId|request_id|request\s+id)\b[""']?\s*[:=]\s*[""']?(?<id>{GuidPattern})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ForJobIdRegex = new(
        $@"\b(?:for\s+job|job\s+request)\s+['""]?(?<id>{GuidPattern})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ResultRegex = new(
        @"\b(?:JobRunner|Worker|JobDispatcher|StepsRunner|JobExtension)\].*?\b(?:job result(?: after .*?)?|job completed with result|worker execution has finished with result|finished with result|completed with result)\s*[:=]?\s*['""]?(?<result>SucceededWithIssues|Succeeded|Failed|Canceled|Cancelled)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex JobRunnerResultRegex = new(
        @"\b(?:JobRunner|Worker|JobDispatcher|StepsRunner|JobExtension)\].*?\bresult(?: after .*?)?\s*[:=]\s*['""]?(?<result>SucceededWithIssues|Succeeded|Failed|Canceled|Cancelled)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitCancelledRegex = new(
        @"\b(?:job (?:was )?cancelled|job (?:was )?canceled|job cancellation requested)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, ParsedFileCache> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<RunnerStatisticsSnapshot> GetSnapshotAsync(
        IReadOnlyList<RunnerInfo> runners,
        StatisticsPeriod period,
        IProgress<StatisticsScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => BuildSnapshot(runners, period, progress, cancellationToken), cancellationToken);
    }

    private RunnerStatisticsSnapshot BuildSnapshot(
        IReadOnlyList<RunnerInfo> runners,
        StatisticsPeriod period,
        IProgress<StatisticsScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var allRuns = new ConcurrentBag<JobRunRecord>();
        var candidates = new List<LogCandidate>();
        var busyFolders = new HashSet<string>(
            runners.Where(x => x.State == RunnerState.Busy).Select(x => NormalizePath(x.FolderPath)),
            StringComparer.OrdinalIgnoreCase);
        var cutoff = GetCutoff(period);

        foreach (var runner in runners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diag = Path.Combine(runner.FolderPath, "_diag");
            if (!Directory.Exists(diag)) continue;

            List<FileInfo> files;
            try
            {
                files = new DirectoryInfo(diag)
                    .EnumerateFiles("Worker_*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(x => x.LastWriteTimeUtc)
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLogger.Error($"Could not enumerate statistics logs for '{runner.FolderPath}'", ex);
                continue;
            }

            // The newest Worker file of a BUSY runner is the current job. It is
            // represented by ActiveRuns, not by completed-history counters.
            var activeFile = busyFolders.Contains(NormalizePath(runner.FolderPath))
                ? files.FirstOrDefault()?.FullName
                : null;

            foreach (var file in files)
            {
                if (activeFile is not null && string.Equals(file.FullName, activeFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (cutoff is not null && file.LastWriteTimeUtc < cutoff.Value.UtcDateTime)
                    continue;

                candidates.Add(new LogCandidate(runner, file));
            }
        }

        progress?.Report(new StatisticsScanProgress
        {
            ProcessedFiles = 0,
            TotalFiles = candidates.Count
        });

        var processed = 0;
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount))
        };

        Parallel.ForEach(candidates, options, candidate =>
        {
            var parsed = ParseFile(candidate.File);
            if (parsed is not null)
            {
                allRuns.Add(new JobRunRecord
                {
                    RunnerAlias = candidate.Runner.Alias,
                    GitHubTarget = candidate.Runner.GitHubTarget,
                    JobName = parsed.JobName,
                    StartedAt = parsed.StartedAt,
                    CompletedAt = parsed.CompletedAt,
                    Duration = parsed.Duration,
                    Outcome = parsed.Outcome,
                    SourceFile = candidate.File.FullName,
                    ExecutionId = parsed.ExecutionId,
                    IdentityKind = parsed.IdentityKind,
                    SourceLogCount = 1
                });
            }

            var done = Interlocked.Increment(ref processed);
            progress?.Report(new StatisticsScanProgress
            {
                ProcessedFiles = done,
                TotalFiles = candidates.Count,
                RunnerAlias = candidate.Runner.Alias
            });
        });

        var rawFiltered = allRuns
            .Where(x => cutoff is null || x.StartedAt >= cutoff.Value)
            .OrderByDescending(x => x.StartedAt)
            .ToList();

        var filtered = DeduplicateRuns(rawFiltered, out var duplicateLogsDiscarded)
            .OrderByDescending(x => x.StartedAt)
            .ToList();

        var succeeded = filtered.Count(x => x.Outcome == JobRunOutcome.Succeeded);
        var failed = filtered.Count(x => x.Outcome == JobRunOutcome.Failed);
        var cancelled = filtered.Count(x => x.Outcome == JobRunOutcome.Cancelled);
        var unknown = filtered.Count(x => x.Outcome == JobRunOutcome.Unknown);
        var known = succeeded + failed + cancelled;
        var technicalKnown = succeeded + failed;
        var reliabilityRate = technicalKnown == 0 ? 0d : succeeded * 100d / technicalKnown;
        var cancellationRate = known == 0 ? 0d : cancelled * 100d / known;
        var totalDuration = TimeSpan.FromTicks(filtered.Sum(x => Math.Max(0, x.Duration.Ticks)));
        var averageDuration = filtered.Count == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(totalDuration.Ticks / filtered.Count);

        var now = DateTimeOffset.Now;
        var trend24h = CalculateReliabilityTrend24h(filtered, now);
        var todayStart = new DateTimeOffset(now.Date, now.Offset);
        var runsToday = filtered.Count(x => x.StartedAt.ToLocalTime() >= todayStart);
        var runsLast24Hours = filtered.Count(x => x.StartedAt >= now.AddHours(-24));
        var firstRunAt = filtered.Count == 0 ? (DateTimeOffset?)null : filtered.Min(x => x.StartedAt);
        var lastRunAt = filtered.Count == 0 ? (DateTimeOffset?)null : filtered.Max(x => x.StartedAt);

        var coverageDays = 0d;
        if (firstRunAt is not null && lastRunAt is not null)
        {
            coverageDays = Math.Max(1d, (lastRunAt.Value.ToLocalTime().Date - firstRunAt.Value.ToLocalTime().Date).TotalDays + 1d);
        }
        var jobsPerDay = filtered.Count == 0 ? 0d : filtered.Count / coverageDays;

        var dailyGroups = filtered
            .GroupBy(x => x.StartedAt.ToLocalTime().Date)
            .OrderBy(x => x.Key)
            .Select(group => new
            {
                Date = group.Key,
                Total = group.Count(),
                Ok = group.Count(x => x.Outcome == JobRunOutcome.Succeeded),
                Failed = group.Count(x => x.Outcome == JobRunOutcome.Failed),
                Cancelled = group.Count(x => x.Outcome == JobRunOutcome.Cancelled),
                Unknown = group.Count(x => x.Outcome == JobRunOutcome.Unknown)
            })
            .ToList();

        var peakDay = dailyGroups.OrderByDescending(x => x.Total).ThenByDescending(x => x.Date).FirstOrDefault();
        var chartDays = BuildDailyActivity(filtered, now);

        var byRunner = runners
            .Select(runner =>
            {
                var runs = filtered
                    .Where(x => string.Equals(x.RunnerAlias, runner.Alias, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var ok = runs.Count(x => x.Outcome == JobRunOutcome.Succeeded);
                var ko = runs.Count(x => x.Outcome == JobRunOutcome.Failed);
                var cancel = runs.Count(x => x.Outcome == JobRunOutcome.Cancelled);
                var noResult = runs.Count(x => x.Outcome == JobRunOutcome.Unknown);
                var knownRuns = ok + ko + cancel;
                var technicalRuns = ok + ko;
                var runnerDuration = TimeSpan.FromTicks(runs.Sum(x => Math.Max(0, x.Duration.Ticks)));
                var runnerAverage = runs.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(runnerDuration.Ticks / runs.Count);
                var runnerTrend24h = CalculateReliabilityTrend24h(runs, now);

                return new RunnerStatisticsRow
                {
                    RunnerAlias = runner.Alias,
                    GitHubTarget = runner.GitHubTarget,
                    TotalRuns = runs.Count,
                    SucceededRuns = ok,
                    FailedRuns = ko,
                    CancelledRuns = cancel,
                    UnknownRuns = noResult,
                    IsBusy = runner.State == RunnerState.Busy,
                    SuccessRate = knownRuns == 0 ? 0 : ok * 100d / knownRuns,
                    ReliabilityRate = technicalRuns == 0 ? 0 : ok * 100d / technicalRuns,
                    CancellationRate = knownRuns == 0 ? 0 : cancel * 100d / knownRuns,
                    WorkloadShare = filtered.Count == 0 ? 0 : runs.Count * 100d / filtered.Count,
                    Trend24h = runnerTrend24h,
                    TotalDuration = runnerDuration,
                    AverageDuration = runnerAverage,
                    LastRunAt = runs.FirstOrDefault()?.StartedAt
                };
            })
            .OrderByDescending(x => x.TotalRuns)
            .ThenBy(x => x.RunnerAlias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mostActiveRunner = byRunner.FirstOrDefault(x => x.TotalRuns > 0);
        var mostActiveRunnerLabel = mostActiveRunner is null
            ? "—"
            : $"{mostActiveRunner.RunnerAlias} · {mostActiveRunner.WorkloadShare:N1}%";

        return new RunnerStatisticsSnapshot
        {
            Period = period,
            TotalRuns = filtered.Count,
            SucceededRuns = succeeded,
            FailedRuns = failed,
            CancelledRuns = cancelled,
            UnknownRuns = unknown,
            ActiveRuns = runners.Count(x => x.State == RunnerState.Busy),
            SuccessRate = known == 0 ? 0 : succeeded * 100d / known,
            ReliabilityRate = reliabilityRate,
            CancellationRate = cancellationRate,
            Trend24h = trend24h,
            MostActiveRunnerLabel = mostActiveRunnerLabel,
            TotalDuration = totalDuration,
            AverageDuration = averageDuration,
            RunnersWithActivity = byRunner.Count(x => x.TotalRuns > 0 || x.IsBusy),
            FirstRunAt = firstRunAt,
            LastRunAt = lastRunAt,
            RunsToday = runsToday,
            RunsLast24Hours = runsLast24Hours,
            JobsPerDay = jobsPerDay,
            PeakDayLabel = peakDay?.Date.ToString("dd/MM/yyyy") ?? "—",
            PeakDayRuns = peakDay?.Total ?? 0,
            WorkerLogsScanned = candidates.Count,
            ParsedWorkerLogs = rawFiltered.Count,
            StableIdentityRuns = filtered.Count(x => !string.IsNullOrWhiteSpace(x.ExecutionId)),
            DuplicateLogsDiscarded = duplicateLogsDiscarded,
            ByRunner = byRunner,
            RecentRuns = filtered.Take(500).ToList(),
            DailyActivity = chartDays
        };
    }

    private static double? CalculateReliabilityTrend24h(
        IReadOnlyList<JobRunRecord> runs,
        DateTimeOffset now)
    {
        var recentStart = now.AddHours(-24);
        var previousStart = now.AddHours(-48);

        var recent = runs
            .Where(x => x.StartedAt >= recentStart && x.StartedAt <= now &&
                        x.Outcome is JobRunOutcome.Succeeded or JobRunOutcome.Failed)
            .ToList();
        var previous = runs
            .Where(x => x.StartedAt >= previousStart && x.StartedAt < recentStart &&
                        x.Outcome is JobRunOutcome.Succeeded or JobRunOutcome.Failed)
            .ToList();

        // A tiny sample creates dramatic but meaningless swings. Require at least
        // three technically classified jobs in each 24 h window.
        if (recent.Count < 3 || previous.Count < 3) return null;

        var recentRate = recent.Count(x => x.Outcome == JobRunOutcome.Succeeded) * 100d / recent.Count;
        var previousRate = previous.Count(x => x.Outcome == JobRunOutcome.Succeeded) * 100d / previous.Count;
        return recentRate - previousRate;
    }

    private static IReadOnlyList<DailyActivityPoint> BuildDailyActivity(
        IReadOnlyList<JobRunRecord> runs,
        DateTimeOffset now)
    {
        const int daysToShow = 14;
        var localToday = now.ToLocalTime().Date;
        var firstDay = localToday.AddDays(-(daysToShow - 1));

        var lookup = runs
            .Where(x => x.StartedAt.ToLocalTime().Date >= firstDay && x.StartedAt.ToLocalTime().Date <= localToday)
            .GroupBy(x => x.StartedAt.ToLocalTime().Date)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Total = group.Count(),
                    Ok = group.Count(x => x.Outcome == JobRunOutcome.Succeeded),
                    Failed = group.Count(x => x.Outcome == JobRunOutcome.Failed),
                    Cancelled = group.Count(x => x.Outcome == JobRunOutcome.Cancelled),
                    Unknown = group.Count(x => x.Outcome == JobRunOutcome.Unknown)
                });

        var maxRuns = lookup.Count == 0 ? 0 : lookup.Values.Max(x => x.Total);
        var points = new List<DailyActivityPoint>(daysToShow);

        for (var index = 0; index < daysToShow; index++)
        {
            var day = firstDay.AddDays(index);
            lookup.TryGetValue(day, out var data);
            var total = data?.Total ?? 0;
            points.Add(new DailyActivityPoint
            {
                Date = day,
                TotalRuns = total,
                SucceededRuns = data?.Ok ?? 0,
                FailedRuns = data?.Failed ?? 0,
                CancelledRuns = data?.Cancelled ?? 0,
                UnknownRuns = data?.Unknown ?? 0,
                RelativeHeight = maxRuns == 0 ? 0 : Math.Max(4d, total * 100d / maxRuns)
            });
        }

        return points;
    }

    private ParsedRun? ParseFile(FileInfo file)
    {
        var key = file.FullName;
        var fingerprint = $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
        if (_cache.TryGetValue(key, out var cached) && cached.Fingerprint == fingerprint)
            return cached.Run;

        ParsedRun? result;
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

            DateTimeOffset? first = null;
            DateTimeOffset? last = null;
            DateTimeOffset? terminalAt = null;
            var jobName = string.Empty;
            var outcome = JobRunOutcome.Unknown;
            var jobId = string.Empty;
            var planId = string.Empty;
            var timelineId = string.Empty;
            var requestId = string.Empty;

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var timestamp = ParseTimestamp(line);
                if (timestamp is not null)
                {
                    first ??= timestamp;
                    last = timestamp;
                }

                if (string.IsNullOrWhiteSpace(jobName))
                {
                    var jsonJob = JsonJobNameRegex.Match(line);
                    if (jsonJob.Success)
                    {
                        var candidate = Clean(jsonJob.Groups["name"].Value);
                        if (!string.IsNullOrWhiteSpace(candidate)) jobName = candidate;
                    }
                }

                var job = JobNameRegex.Match(line);
                if (job.Success)
                {
                    var candidate = Clean(job.Groups["name"].Value);
                    if (!string.IsNullOrWhiteSpace(candidate)) jobName = candidate;
                }

                jobId = FirstId(jobId, JobIdRegex, line);
                if (string.IsNullOrWhiteSpace(jobId)) jobId = FirstId(jobId, ForJobIdRegex, line);
                planId = FirstId(planId, PlanIdRegex, line);
                timelineId = FirstId(timelineId, TimelineIdRegex, line);
                requestId = FirstId(requestId, RequestIdRegex, line);

                var terminal = ResultRegex.Match(line);
                if (!terminal.Success) terminal = JobRunnerResultRegex.Match(line);
                if (terminal.Success)
                {
                    outcome = ParseOutcome(terminal.Groups["result"].Value);
                    terminalAt = timestamp ?? terminalAt;
                }
                else if (outcome == JobRunOutcome.Unknown && ExplicitCancelledRegex.IsMatch(line))
                {
                    outcome = JobRunOutcome.Cancelled;
                    terminalAt = timestamp ?? terminalAt;
                }
            }

            var started = first ?? new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero);
            var completed = terminalAt ?? last ?? new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            if (completed < started) completed = started;

            var (executionId, identityKind) = BuildExecutionIdentity(jobId, planId, requestId, timelineId);
            result = new ParsedRun(
                jobName,
                started,
                completed,
                completed - started,
                outcome,
                executionId,
                identityKind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLogger.Error($"Could not read statistics log '{file.FullName}'", ex);
            result = null;
        }

        _cache[key] = new ParsedFileCache(fingerprint, result);
        return result;
    }

    private static List<JobRunRecord> DeduplicateRuns(
        IReadOnlyList<JobRunRecord> runs,
        out int duplicateLogsDiscarded)
    {
        duplicateLogsDiscarded = 0;
        var result = new List<JobRunRecord>(runs.Count);

        // No stable ID means no deduplication. Worker logs are per-job diagnostics,
        // so collapsing by timestamp/name alone would under-count short sequential jobs.
        result.AddRange(runs.Where(x => string.IsNullOrWhiteSpace(x.ExecutionId)));

        var identifiedGroups = runs
            .Where(x => !string.IsNullOrWhiteSpace(x.ExecutionId))
            .GroupBy(x => $"{x.RunnerAlias}\u001f{x.ExecutionId}", StringComparer.OrdinalIgnoreCase);

        foreach (var group in identifiedGroups)
        {
            var ordered = group.OrderBy(x => x.StartedAt).ToList();
            var cluster = new List<JobRunRecord>();

            foreach (var run in ordered)
            {
                if (cluster.Count == 0)
                {
                    cluster.Add(run);
                    continue;
                }

                if (IsProvableDuplicate(cluster[0], run))
                {
                    cluster.Add(run);
                    continue;
                }

                AddCluster(result, cluster, ref duplicateLogsDiscarded);
                cluster = [run];
            }

            AddCluster(result, cluster, ref duplicateLogsDiscarded);
        }

        return result;
    }

    private static bool IsProvableDuplicate(JobRunRecord first, JobRunRecord candidate)
    {
        var startDelta = (candidate.StartedAt - first.StartedAt).Duration();
        if (startDelta > DuplicateStartTolerance) return false;

        // Conflicting terminal results are evidence that these should not be silently
        // collapsed even if a broken upstream system reused an identifier.
        if (IsKnown(first.Outcome) && IsKnown(candidate.Outcome) && first.Outcome != candidate.Outcome)
            return false;

        return true;
    }

    private static void AddCluster(
        List<JobRunRecord> destination,
        IReadOnlyList<JobRunRecord> cluster,
        ref int duplicateLogsDiscarded)
    {
        if (cluster.Count == 0) return;
        if (cluster.Count == 1)
        {
            destination.Add(cluster[0]);
            return;
        }

        duplicateLogsDiscarded += cluster.Count - 1;

        var canonical = cluster
            .OrderByDescending(x => IsKnown(x.Outcome))
            .ThenByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.Duration)
            .First();

        var started = cluster.Min(x => x.StartedAt);
        var completed = cluster.Max(x => x.CompletedAt);
        var jobName = cluster
            .Select(x => x.JobName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault() ?? canonical.JobName;

        destination.Add(canonical with
        {
            JobName = jobName,
            StartedAt = started,
            CompletedAt = completed,
            Duration = completed >= started ? completed - started : TimeSpan.Zero,
            SourceLogCount = cluster.Sum(x => Math.Max(1, x.SourceLogCount))
        });
    }

    private static bool IsKnown(JobRunOutcome outcome) => outcome != JobRunOutcome.Unknown;

    private static string FirstId(string current, Regex regex, string line)
    {
        if (!string.IsNullOrWhiteSpace(current)) return current;
        var match = regex.Match(line);
        return match.Success ? match.Groups["id"].Value.ToLowerInvariant() : string.Empty;
    }

    private static (string Id, string Kind) BuildExecutionIdentity(
        string jobId,
        string planId,
        string requestId,
        string timelineId)
    {
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            return !string.IsNullOrWhiteSpace(planId)
                ? ($"plan:{planId}|job:{jobId}", "Plan + Job ID")
                : ($"job:{jobId}", "Job ID");
        }

        if (!string.IsNullOrWhiteSpace(requestId))
            return ($"request:{requestId}", "Request ID");

        if (!string.IsNullOrWhiteSpace(timelineId))
            return ($"timeline:{timelineId}", "Timeline ID");

        return (string.Empty, string.Empty);
    }

    private static JobRunOutcome ParseOutcome(string value)
    {
        var normalized = value.Trim().Trim('\'', '"').ToLowerInvariant();
        return normalized switch
        {
            "succeeded" or "succeededwithissues" => JobRunOutcome.Succeeded,
            "failed" => JobRunOutcome.Failed,
            "canceled" or "cancelled" => JobRunOutcome.Cancelled,
            _ => JobRunOutcome.Unknown
        };
    }

    private static DateTimeOffset? ParseTimestamp(string line)
    {
        var match = TimestampRegex.Match(line);
        if (!match.Success) return null;
        var raw = match.Groups["ts"].Value.Replace(' ', 'T');
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
            ? timestamp
            : null;
    }

    private static DateTimeOffset? GetCutoff(StatisticsPeriod period)
    {
        var now = DateTimeOffset.Now;
        return period switch
        {
            StatisticsPeriod.Today => new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).ToUniversalTime(),
            StatisticsPeriod.Last7Days => now.AddDays(-7).ToUniversalTime(),
            StatisticsPeriod.Last30Days => now.AddDays(-30).ToUniversalTime(),
            _ => null
        };
    }

    private static string Clean(string text)
    {
        var result = text.Trim().Trim('\'', '"');
        return result.Length > 140 ? result[..140] + "…" : result;
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record LogCandidate(RunnerInfo Runner, FileInfo File);

    private sealed record ParsedRun(
        string JobName,
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        TimeSpan Duration,
        JobRunOutcome Outcome,
        string ExecutionId,
        string IdentityKind);

    private sealed record ParsedFileCache(string Fingerprint, ParsedRun? Run);
}
