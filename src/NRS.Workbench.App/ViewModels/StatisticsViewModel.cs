using System.Collections.ObjectModel;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.ViewModels;

public sealed class StatisticsViewModel : ObservableObject
{
    private readonly IRunnerStatisticsService _statistics;
    private readonly Func<IReadOnlyList<RunnerInfo>> _runnersProvider;
    private readonly IGitService _git;
    private readonly SettingsService _settings;
    private StatisticsPeriodOption _selectedPeriod;
    private bool _isLoading;
    private string _statusText = "Preparando estadísticas...";
    private int _totalRuns;
    private int _succeededRuns;
    private int _failedRuns;
    private int _cancelledRuns;
    private int _unknownRuns;
    private int _activeRuns;
    private string _successRate = "—";
    private string _reliabilityRate = "—";
    private string _cancellationRate = "—";
    private string _trend24h = "—";
    private string _trend24hKind = "Unknown";
    private string _mostActiveRunner = "—";
    private string _totalDuration = "—";
    private string _averageDuration = "—";
    private int _runnersWithActivity;
    private string _firstRun = "—";
    private string _lastRun = "—";
    private string _coverage = "—";
    private int _runsToday;
    private int _runsLast24Hours;
    private string _jobsPerDay = "—";
    private string _peakDay = "—";
    private double _scanProgress;
    private string _scanDetail = string.Empty;
    private string _auditText = "Worker logs: — · Jobs únicos: — · Duplicados: — · Con ID: —";

    private int _repositoryTotalCount;
    private int _repositoryCleanCount;
    private int _repositoryChangesCount;
    private int _repositoryAheadCount;
    private int _repositoryBehindCount;
    private int _repositoryAttentionCount;
    private string _repositoryCleanRate = "—";
    private string _repositorySyncRate = "—";
    private string _repositoryLastCommit = "—";
    private string _repositoryMostChanged = "—";
    private string _repositoryStatusText = "Preparando inventario Git local...";

    public ObservableCollection<RunnerStatisticsRow> ByRunner { get; } = [];
    public ObservableCollection<JobRunRecord> RecentRuns { get; } = [];
    public ObservableCollection<DailyActivityPoint> DailyActivity { get; } = [];
    public ObservableCollection<GitRepositoryInfo> RepositoryRows { get; } = [];

    public IReadOnlyList<StatisticsPeriodOption> PeriodOptions { get; } =
    [
        new("Todo", StatisticsPeriod.All),
        new("Hoy", StatisticsPeriod.Today),
        new("Últimos 7 días", StatisticsPeriod.Last7Days),
        new("Últimos 30 días", StatisticsPeriod.Last30Days)
    ];

    public StatisticsPeriodOption SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public int TotalRuns { get => _totalRuns; private set => SetProperty(ref _totalRuns, value); }
    public int SucceededRuns { get => _succeededRuns; private set => SetProperty(ref _succeededRuns, value); }
    public int FailedRuns { get => _failedRuns; private set => SetProperty(ref _failedRuns, value); }
    public int CancelledRuns { get => _cancelledRuns; private set => SetProperty(ref _cancelledRuns, value); }
    public int UnknownRuns { get => _unknownRuns; private set => SetProperty(ref _unknownRuns, value); }
    public int ActiveRuns { get => _activeRuns; private set => SetProperty(ref _activeRuns, value); }
    public string SuccessRate { get => _successRate; private set => SetProperty(ref _successRate, value); }
    public string ReliabilityRate { get => _reliabilityRate; private set => SetProperty(ref _reliabilityRate, value); }
    public string CancellationRate { get => _cancellationRate; private set => SetProperty(ref _cancellationRate, value); }
    public string Trend24h { get => _trend24h; private set => SetProperty(ref _trend24h, value); }
    public string Trend24hKind { get => _trend24hKind; private set => SetProperty(ref _trend24hKind, value); }
    public string MostActiveRunner { get => _mostActiveRunner; private set => SetProperty(ref _mostActiveRunner, value); }
    public string TotalDuration { get => _totalDuration; private set => SetProperty(ref _totalDuration, value); }
    public string AverageDuration { get => _averageDuration; private set => SetProperty(ref _averageDuration, value); }
    public int RunnersWithActivity { get => _runnersWithActivity; private set => SetProperty(ref _runnersWithActivity, value); }
    public string FirstRun { get => _firstRun; private set => SetProperty(ref _firstRun, value); }
    public string LastRun { get => _lastRun; private set => SetProperty(ref _lastRun, value); }
    public string Coverage { get => _coverage; private set => SetProperty(ref _coverage, value); }
    public int RunsToday { get => _runsToday; private set => SetProperty(ref _runsToday, value); }
    public int RunsLast24Hours { get => _runsLast24Hours; private set => SetProperty(ref _runsLast24Hours, value); }
    public string JobsPerDay { get => _jobsPerDay; private set => SetProperty(ref _jobsPerDay, value); }
    public string PeakDay { get => _peakDay; private set => SetProperty(ref _peakDay, value); }
    public double ScanProgress { get => _scanProgress; private set => SetProperty(ref _scanProgress, value); }
    public string ScanDetail { get => _scanDetail; private set => SetProperty(ref _scanDetail, value); }
    public string AuditText { get => _auditText; private set => SetProperty(ref _auditText, value); }

    public int RepositoryTotalCount { get => _repositoryTotalCount; private set => SetProperty(ref _repositoryTotalCount, value); }
    public int RepositoryCleanCount { get => _repositoryCleanCount; private set => SetProperty(ref _repositoryCleanCount, value); }
    public int RepositoryChangesCount { get => _repositoryChangesCount; private set => SetProperty(ref _repositoryChangesCount, value); }
    public int RepositoryAheadCount { get => _repositoryAheadCount; private set => SetProperty(ref _repositoryAheadCount, value); }
    public int RepositoryBehindCount { get => _repositoryBehindCount; private set => SetProperty(ref _repositoryBehindCount, value); }
    public int RepositoryAttentionCount { get => _repositoryAttentionCount; private set => SetProperty(ref _repositoryAttentionCount, value); }
    public string RepositoryCleanRate { get => _repositoryCleanRate; private set => SetProperty(ref _repositoryCleanRate, value); }
    public string RepositorySyncRate { get => _repositorySyncRate; private set => SetProperty(ref _repositorySyncRate, value); }
    public string RepositoryLastCommit { get => _repositoryLastCommit; private set => SetProperty(ref _repositoryLastCommit, value); }
    public string RepositoryMostChanged { get => _repositoryMostChanged; private set => SetProperty(ref _repositoryMostChanged, value); }
    public string RepositoryStatusText { get => _repositoryStatusText; private set => SetProperty(ref _repositoryStatusText, value); }

    public AsyncRelayCommand RefreshCommand { get; }

    public StatisticsViewModel(
        IRunnerStatisticsService statistics,
        Func<IReadOnlyList<RunnerInfo>> runnersProvider,
        IGitService git,
        SettingsService settings)
    {
        _statistics = statistics;
        _runnersProvider = runnersProvider;
        _git = git;
        _settings = settings;
        _selectedPeriod = PeriodOptions[0];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ScanProgress = 0;
        ScanDetail = "Preparando historial local...";
        StatusText = "Analizando historial local de Worker logs...";
        RepositoryStatusText = "Actualizando inventario Git local...";

        try
        {
            var runners = _runnersProvider();

            // Live state is known immediately. Do not show EN CURSO = 0 while the
            // historical scan is still reading files.
            ActiveRuns = runners.Count(x => x.State == RunnerState.Busy);
            RunnersWithActivity = ActiveRuns;

            // Repository status is an independent current-state snapshot. It does
            // not inherit the runner-history period filter and runs in parallel.
            var repositoryRefresh = RefreshRepositorySnapshotAsync();

            var progress = new Progress<StatisticsScanProgress>(value =>
            {
                ScanProgress = value.Percent;
                if (value.TotalFiles <= 0)
                {
                    ScanDetail = "No hay Worker logs históricos que analizar.";
                    return;
                }

                var runner = string.IsNullOrWhiteSpace(value.RunnerAlias) ? string.Empty : $" · {value.RunnerAlias}";
                ScanDetail = $"Analizando {value.ProcessedFiles:N0}/{value.TotalFiles:N0} logs{runner}";
                StatusText = ScanDetail;
            });

            var snapshot = await _statistics.GetSnapshotAsync(runners, SelectedPeriod.Period, progress);

            TotalRuns = snapshot.TotalRuns;
            SucceededRuns = snapshot.SucceededRuns;
            FailedRuns = snapshot.FailedRuns;
            CancelledRuns = snapshot.CancelledRuns;
            UnknownRuns = snapshot.UnknownRuns;
            ActiveRuns = snapshot.ActiveRuns;
            SuccessRate = snapshot.SuccessRateDisplay;
            ReliabilityRate = snapshot.ReliabilityRateDisplay;
            CancellationRate = snapshot.CancellationRateDisplay;
            Trend24h = snapshot.Trend24hDisplay;
            Trend24hKind = snapshot.Trend24hKind;
            MostActiveRunner = snapshot.MostActiveRunnerLabel;
            TotalDuration = snapshot.TotalDurationDisplay;
            AverageDuration = snapshot.AverageDurationDisplay;
            RunnersWithActivity = snapshot.RunnersWithActivity;
            FirstRun = snapshot.FirstRunDisplay;
            LastRun = snapshot.LastRunDisplay;
            Coverage = snapshot.CoverageDisplay;
            RunsToday = snapshot.RunsToday;
            RunsLast24Hours = snapshot.RunsLast24Hours;
            JobsPerDay = snapshot.JobsPerDayDisplay;
            PeakDay = snapshot.PeakDayDisplay;
            AuditText = $"Worker logs: {snapshot.WorkerLogsScanned:N0} · Jobs únicos: {snapshot.TotalRuns:N0} · Duplicados: {snapshot.DuplicateLogsDiscarded:N0} · Con ID: {snapshot.StableIdentityRuns:N0}";

            DailyActivity.Clear();
            foreach (var point in snapshot.DailyActivity) DailyActivity.Add(point);

            ByRunner.Clear();
            foreach (var row in snapshot.ByRunner) ByRunner.Add(row);

            RecentRuns.Clear();
            foreach (var run in snapshot.RecentRuns) RecentRuns.Add(run);

            await repositoryRefresh;

            ScanProgress = 100;
            ScanDetail = snapshot.TotalRuns == 0
                ? "Historial analizado."
                : $"{snapshot.TotalRuns:N0} jobs locales auditados.";
            StatusText = snapshot.TotalRuns == 0
                ? snapshot.ActiveRuns > 0
                    ? $"Sin historial finalizado · {snapshot.ActiveRuns} job(s) en curso"
                    : "No hay jobs históricos disponibles para este periodo."
                : $"{snapshot.TotalRuns:N0} jobs locales · {snapshot.RunnersWithActivity} runners con actividad";
        }
        catch (Exception ex)
        {
            AppLogger.Error("Statistics refresh failed", ex);
            StatusText = "No se pudieron calcular las estadísticas.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshRepositorySnapshotAsync()
    {
        try
        {
            var paths = _settings.Load().RepositoryPaths
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths.Count == 0)
            {
                ApplyRepositorySnapshot([]);
                RepositoryStatusText = "Sin repositorios configurados.";
                return;
            }

            using var gate = new SemaphoreSlim(4, 4);
            var tasks = paths.Select(async path =>
            {
                await gate.WaitAsync();
                try
                {
                    return await _git.InspectAsync(path);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Could not inspect repository '{path}' for statistics", ex);
                    return new GitRepositoryInfo
                    {
                        Name = new DirectoryInfo(path).Name,
                        Path = path,
                        State = GitRepositoryState.Error,
                        ErrorMessage = ex.Message
                    };
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();

            var rows = (await Task.WhenAll(tasks))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyRepositorySnapshot(rows);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Repository statistics refresh failed", ex);
            RepositoryStatusText = "No se pudo actualizar el estado de los repositorios.";
        }
    }

    private void ApplyRepositorySnapshot(IReadOnlyList<GitRepositoryInfo> rows)
    {
        RepositoryRows.Clear();
        foreach (var row in rows) RepositoryRows.Add(row);

        RepositoryTotalCount = rows.Count;
        RepositoryCleanCount = rows.Count(x => x.State == GitRepositoryState.Clean);
        RepositoryChangesCount = rows.Count(x => x.IsDirty);
        RepositoryAheadCount = rows.Count(x => x.Ahead > 0);
        RepositoryBehindCount = rows.Count(x => x.Behind > 0);
        RepositoryAttentionCount = rows.Count(x => x.State is GitRepositoryState.Diverged or GitRepositoryState.Conflict or GitRepositoryState.Error);

        RepositoryCleanRate = rows.Count == 0
            ? "—"
            : $"{RepositoryCleanCount * 100d / rows.Count:0.#}%";

        var withRemote = rows.Where(x => x.HasRemote && x.State != GitRepositoryState.Error).ToList();
        var synchronized = withRemote.Count(x => x.Ahead == 0 && x.Behind == 0 && x.ConflictCount == 0);
        RepositorySyncRate = withRemote.Count == 0
            ? "—"
            : $"{synchronized * 100d / withRemote.Count:0.#}%";

        var latest = rows
            .Where(x => x.LastCommitDate is not null)
            .OrderByDescending(x => x.LastCommitDate)
            .FirstOrDefault();
        RepositoryLastCommit = latest?.LastCommitDate is null
            ? "—"
            : $"{latest.Name} · {latest.LastCommitDate.Value.LocalDateTime:dd/MM HH:mm}";

        var mostChanged = rows
            .Where(x => x.ChangeCount > 0)
            .OrderByDescending(x => x.ChangeCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        RepositoryMostChanged = mostChanged is null
            ? "—"
            : $"{mostChanged.Name} · {mostChanged.ChangeCount} cambio{(mostChanged.ChangeCount == 1 ? string.Empty : "s")}";

        RepositoryStatusText = rows.Count == 0
            ? "Sin repositorios configurados."
            : $"{rows.Count} repos · {RepositoryCleanCount} clean · {RepositoryChangesCount} con cambios · {RepositoryAttentionCount} con atención";
    }
}

public sealed record StatisticsPeriodOption(string Label, StatisticsPeriod Period)
{
    public override string ToString() => Label;
}