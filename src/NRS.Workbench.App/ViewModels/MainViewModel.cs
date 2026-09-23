using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IRunnerDiscoveryService _discovery;
    private readonly IRunnerControlService _control;
    private readonly ILogReaderService _logs;
    private readonly SettingsService _settingsService;
    private readonly IDialogService _dialogs;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<RunnerInfo> _allRunners = [];
    private List<string> _manualOrder = [];
    private string _selectedSortMode = "manual";
    private string _runnerSearchText = string.Empty;
    private string? _preferredRunnerPath;
    private bool _updatingRunnerView;
    private RunnerInfo? _selectedRunner;
    private string _logText = UiLanguage.Choose("Selecciona un runner para ver el log.", "Select a runner to view its log.");
    private string _statusText = UiLanguage.Choose("Inicializando...", "Initializing...");
    private DateTimeOffset _lastUpdated;
    private string _cpuUseText = "—";
    private string _memoryUseText = "—";
    private string _diskUseText = "—";
    private double _cpuPercent;
    private double _memoryPercent;
    private double _diskPercent;

    public ObservableCollection<RunnerInfo> Runners { get; } = [];
    public IReadOnlyList<RunnerInfo> AllRunners => _allRunners;
    public int TotalCount => _allRunners.Count;
    public int ReadyCount => _allRunners.Count(x => x.State == RunnerState.Ready);
    public int BusyCount => _allRunners.Count(x => x.State == RunnerState.Busy);
    public int StoppedCount => _allRunners.Count(x => x.State == RunnerState.Stopped);
    public int ErrorCount => _allRunners.Count(x => x.State is RunnerState.Error or RunnerState.Unregistered);
    public int IssueCount => StoppedCount + ErrorCount;
    public string HealthLabel => TotalCount == 0 ? UiLanguage.Choose("SIN DATOS", "NO DATA") : ErrorCount > 0 ? UiLanguage.Choose("ALERTA", "ALERT") : StoppedCount > 0 ? UiLanguage.Text("ATENCIÓN") : "OK";
    public string HealthKind => TotalCount == 0 ? "Unknown" : ErrorCount > 0 ? "Alert" : StoppedCount > 0 ? "Warning" : "Healthy";
    public string HealthDetail => TotalCount == 0 ? "0 runners" : IssueCount == 0 ? UiLanguage.Choose($"{TotalCount} operativos", $"{TotalCount} operational") : UiLanguage.Choose($"{IssueCount} con atención", $"{IssueCount} need attention");
    public IReadOnlyList<RunnerSortOption> SortOptions { get; } =
    [
        new("manual", UiLanguage.Choose("Orden manual", "Manual order")),
        new("name", UiLanguage.Choose("Nombre (A–Z)", "Name (A–Z)")),
        new("state", UiLanguage.Choose("Estado (BUSY primero)", "Status (BUSY first)")),
        new("target", UiLanguage.Choose("Destino GitHub", "GitHub target")),
        new("memory", UiLanguage.Choose("RAM (mayor primero)", "RAM (highest first)"))
    ];

    public string SelectedSortMode
    {
        get => _selectedSortMode;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var normalized = RunnerListOrganizer.NormalizeMode(value);
            if (!SetProperty(ref _selectedSortMode, normalized)) return;
            var settings = _settingsService.Load();
            settings.RunnerSortMode = normalized;
            _settingsService.Save(settings);
            UpdateRunnerView();
        }
    }

    public string RunnerSearchText
    {
        get => _runnerSearchText;
        set
        {
            if (!SetProperty(ref _runnerSearchText, value ?? string.Empty)) return;
            UpdateRunnerView();
            ClearRunnerSearchCommand.RaiseCanExecuteChanged();
        }
    }

    public string RunnerViewSummary => string.IsNullOrWhiteSpace(RunnerSearchText)
        ? UiLanguage.Choose($"{TotalCount} runners", $"{TotalCount} runners")
        : UiLanguage.Choose($"{Runners.Count} de {TotalCount} runners", $"{Runners.Count} of {TotalCount} runners");

    public RunnerInfo? SelectedRunner
    {
        get => _selectedRunner;
        set
        {
            if (!SetProperty(ref _selectedRunner, value)) return;
            if (!_updatingRunnerView && value is not null) _preferredRunnerPath = value.FolderPath;
            LoadSelectedLog();
            RaiseCommandStates();
        }
    }

    public string LogText { get => _logText; private set => SetProperty(ref _logText, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string LastUpdatedText => _lastUpdated == default ? string.Empty : _lastUpdated.ToString("dd MMM yyyy  HH:mm:ss");
    public string AppVersionText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            return version is null ? "v—" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }
    public string CpuUseText { get => _cpuUseText; private set => SetProperty(ref _cpuUseText, value); }
    public string MemoryUseText { get => _memoryUseText; private set => SetProperty(ref _memoryUseText, value); }
    public string DiskUseText { get => _diskUseText; private set => SetProperty(ref _diskUseText, value); }
    public double CpuPercent { get => _cpuPercent; private set => SetProperty(ref _cpuPercent, value); }
    public double MemoryPercent { get => _memoryPercent; private set => SetProperty(ref _memoryPercent, value); }
    public double DiskPercent { get => _diskPercent; private set => SetProperty(ref _diskPercent, value); }
    public string CpuCapacityText => UiLanguage.Choose($"{Environment.ProcessorCount} procesadores lógicos", $"{Environment.ProcessorCount} logical processors");
    public int RefreshIntervalSeconds => _settingsService.Load().RefreshIntervalSeconds;

    public void UpdateSystemResources(SystemResourceSnapshot snapshot)
    {
        CpuUseText = snapshot.CpuPercent is double cpu ? $"{cpu:N0}%" : "—";
        CpuPercent = snapshot.CpuPercent ?? 0;
        MemoryUseText = FormatCapacity(snapshot.UsedMemoryBytes, snapshot.TotalMemoryBytes);
        MemoryPercent = Percent(snapshot.UsedMemoryBytes, snapshot.TotalMemoryBytes);
        DiskUseText = FormatCapacity(snapshot.UsedDiskBytes, snapshot.TotalDiskBytes);
        DiskPercent = Percent(snapshot.UsedDiskBytes, snapshot.TotalDiskBytes);
    }

    private static string FormatCapacity(ulong? used, ulong? total) =>
        used is ulong value && total is ulong capacity && capacity > 0
            ? $"{value / 1073741824d:N1} / {capacity / 1073741824d:N1} GiB" : "—";

    private static string FormatCapacity(long? used, long? total) =>
        used is long value && total is long capacity && capacity > 0
            ? $"{value / 1073741824d:N1} / {capacity / 1073741824d:N1} GiB" : "—";

    private static double Percent(ulong? used, ulong? total) =>
        used is ulong value && total is ulong capacity && capacity > 0 ? 100d * value / capacity : 0;

    private static double Percent(long? used, long? total) =>
        used is long value && total is long capacity && capacity > 0 ? 100d * value / capacity : 0;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand RestartCommand { get; }
    public AsyncRelayCommand StartAllCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenDiagCommand { get; }
    public RelayCommand OpenGitHubCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand ClearRunnerSearchCommand { get; }
    public RelayCommand MoveRunnerUpCommand { get; }
    public RelayCommand MoveRunnerDownCommand { get; }

    public MainViewModel(IRunnerDiscoveryService discovery, IRunnerControlService control, ILogReaderService logs,
        SettingsService settingsService, IDialogService dialogs)
    {
        _discovery = discovery;
        _control = control;
        _logs = logs;
        _settingsService = settingsService;
        _dialogs = dialogs;
        var preferences = _settingsService.Load();
        _selectedSortMode = preferences.RunnerSortMode;
        _manualOrder = preferences.RunnerDisplayOrder.ToList();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StartCommand = new AsyncRelayCommand(StartSelectedAsync, CanStartSelected);
        StopCommand = new AsyncRelayCommand(StopSelectedAsync, CanStopSelected);
        RestartCommand = new AsyncRelayCommand(RestartSelectedAsync, CanRestartSelected);
        StartAllCommand = new AsyncRelayCommand(StartAllAsync, () => _allRunners.Any(x => x.State == RunnerState.Stopped));
        StopAllCommand = new AsyncRelayCommand(StopAllAsync, () => _allRunners.Any(x => x.State is RunnerState.Ready or RunnerState.Busy));
        OpenFolderCommand = new RelayCommand(() => OpenPath(SelectedRunner?.FolderPath), () => SelectedRunner is not null);
        OpenDiagCommand = new RelayCommand(() => OpenPath(SelectedRunner is null ? null : Path.Combine(SelectedRunner.FolderPath, "_diag")), () => SelectedRunner is not null);
        OpenGitHubCommand = new RelayCommand(() => OpenUrl(SelectedRunner?.GitHubUrl), () => !string.IsNullOrWhiteSpace(SelectedRunner?.GitHubUrl));
        ClearLogCommand = new RelayCommand(() => LogText = string.Empty, () => SelectedRunner is not null);
        ClearRunnerSearchCommand = new RelayCommand(() => RunnerSearchText = string.Empty, () => !string.IsNullOrEmpty(RunnerSearchText));
        MoveRunnerUpCommand = new RelayCommand(() => MoveSelectedRunner(-1), () => CanMoveSelectedRunner(-1));
        MoveRunnerDownCommand = new RelayCommand(() => MoveSelectedRunner(1), () => CanMoveSelectedRunner(1));
    }

    public async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            StatusText = UiLanguage.Choose("Actualizando runners...", "Refreshing runners...");
            var rows = await Task.Run(_discovery.Discover);
            _allRunners = rows;
            UpdateRunnerView();

            _lastUpdated = DateTimeOffset.Now;
            StatusText = rows.Count == 0
                ? UiLanguage.Choose("No se han detectado runners · revisa Configuración > Detectar automáticamente", "No runners detected · check Settings > Detect automatically")
                : UiLanguage.Choose($"Sistema listo · {rows.Count} runner{(rows.Count == 1 ? "" : "s")} detectado{(rows.Count == 1 ? "" : "s")}", $"System ready · {rows.Count} runner{(rows.Count == 1 ? "" : "s")} detected");
            RaiseCounts();
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Refresh failed", ex);
            StatusText = UiLanguage.Choose("Error al actualizar runners", "Could not refresh runners");
        }
        finally { _refreshGate.Release(); }
    }

    public bool EditSettings(Window owner)
    {
        var changed = _dialogs.EditSettings(owner);
        if (changed)
        {
            var preferences = _settingsService.Load();
            _manualOrder = preferences.RunnerDisplayOrder.ToList();
            _selectedSortMode = preferences.RunnerSortMode;
            RaisePropertyChanged(nameof(SelectedSortMode));
            _ = RefreshAsync();
        }
        return changed;
    }

    private void UpdateRunnerView()
    {
        var ordered = RunnerListOrganizer.Arrange(_allRunners, SelectedSortMode, _manualOrder, RunnerSearchText);
        _updatingRunnerView = true;
        try
        {
            // Update in place so the grid keeps its scroll position across automatic refreshes.
            for (var index = 0; index < ordered.Count; index++)
            {
                var target = ordered[index];
                if (index >= Runners.Count)
                {
                    Runners.Add(target);
                    continue;
                }

                if (!SamePath(Runners[index], target))
                {
                    var previous = -1;
                    for (var candidate = index + 1; candidate < Runners.Count; candidate++)
                        if (SamePath(Runners[candidate], target)) { previous = candidate; break; }
                    if (previous >= 0) Runners.Move(previous, index);
                    else Runners.Insert(index, target);
                }
                if (!ReferenceEquals(Runners[index], target)) Runners[index] = target;
            }
            while (Runners.Count > ordered.Count) Runners.RemoveAt(Runners.Count - 1);

            SelectedRunner = _preferredRunnerPath is null
                ? Runners.FirstOrDefault()
                : Runners.FirstOrDefault(runner =>
                    string.Equals(runner.FolderPath, _preferredRunnerPath, StringComparison.OrdinalIgnoreCase));
        }
        finally { _updatingRunnerView = false; }

        RaisePropertyChanged(nameof(RunnerViewSummary));
        RaiseCommandStates();
    }

    private static bool SamePath(RunnerInfo left, RunnerInfo right) =>
        string.Equals(left.FolderPath, right.FolderPath, StringComparison.OrdinalIgnoreCase);

    private bool CanMoveSelectedRunner(int direction)
    {
        if (SelectedSortMode != "manual" || !string.IsNullOrWhiteSpace(RunnerSearchText) || SelectedRunner is null)
            return false;
        var ordered = RunnerListOrganizer.Arrange(_allRunners, "manual", _manualOrder, null);
        var index = ordered.ToList().FindIndex(runner => SamePath(runner, SelectedRunner));
        return index >= 0 && index + direction >= 0 && index + direction < ordered.Count;
    }

    private void MoveSelectedRunner(int direction)
    {
        if (!CanMoveSelectedRunner(direction) || SelectedRunner is null) return;
        var ordered = RunnerListOrganizer.Arrange(_allRunners, "manual", _manualOrder, null);
        _manualOrder = RunnerListOrganizer.Move(ordered, SelectedRunner.FolderPath, direction).ToList();
        var settings = _settingsService.Load();
        settings.RunnerDisplayOrder = _manualOrder.ToList();
        _settingsService.Save(settings);
        UpdateRunnerView();
    }

    private async Task StartSelectedAsync()
    {
        if (SelectedRunner is null) return;
        await ExecuteAction(() => _control.StartAsync(SelectedRunner), UiLanguage.Choose($"Iniciando {SelectedRunner.Alias}...", $"Starting {SelectedRunner.Alias}..."));
    }

    private async Task StopSelectedAsync()
    {
        if (SelectedRunner is null || !CanStop(SelectedRunner)) return;
        if (!ConfirmBusy(SelectedRunner)) return;
        await ExecuteAction(() => _control.StopAsync(SelectedRunner), UiLanguage.Choose($"Parando {SelectedRunner.Alias}...", $"Stopping {SelectedRunner.Alias}..."));
    }

    private async Task RestartSelectedAsync()
    {
        if (SelectedRunner is null) return;
        if (!ConfirmBusy(SelectedRunner)) return;
        await ExecuteAction(() => _control.RestartAsync(SelectedRunner), UiLanguage.Choose($"Reiniciando {SelectedRunner.Alias}...", $"Restarting {SelectedRunner.Alias}..."));
    }

    private async Task StartAllAsync()
    {
        if (!string.IsNullOrWhiteSpace(RunnerSearchText) && !_dialogs.Confirm(
            UiLanguage.Choose("Hay una búsqueda activa. Iniciar todos actuará sobre todos los runners, incluidos los que no se ven. ¿Continuar?", "A search is active. Start all will affect every runner, including those not shown. Continue?"),
            UiLanguage.Text("Iniciar todos"))) return;
        foreach (var runner in _allRunners.Where(x => x.State == RunnerState.Stopped).ToList())
            await ExecuteAction(() => _control.StartAsync(runner), UiLanguage.Choose($"Iniciando {runner.Alias}...", $"Starting {runner.Alias}..."), refreshAfter: false);
        await RefreshAsync();
    }

    private async Task StopAllAsync()
    {
        var active = _allRunners.Where(x => x.State is RunnerState.Ready or RunnerState.Busy).ToList();
        var hasBusy = active.Any(x => x.State == RunnerState.Busy) && _settingsService.Load().ConfirmStopBusy;
        if ((hasBusy || !string.IsNullOrWhiteSpace(RunnerSearchText)) &&
            !_dialogs.Confirm(UiLanguage.Choose(
                $"{(!string.IsNullOrWhiteSpace(RunnerSearchText) ? "Hay una búsqueda activa. Parar todos actuará sobre todos los runners, incluidos los que no se ven.\n\n" : "")}{(hasBusy ? "Hay runners BUSY. Parar todos puede interrumpir jobs en ejecución.\n\n" : "")}¿Continuar?",
                $"{(!string.IsNullOrWhiteSpace(RunnerSearchText) ? "A search is active. Stop all will affect every runner, including those not shown.\n\n" : "")}{(hasBusy ? "Some runners are BUSY. Stopping all may interrupt running jobs.\n\n" : "")}Continue?"), UiLanguage.Text("Parar todos"))) return;
        foreach (var runner in active)
            await ExecuteAction(() => _control.StopAsync(runner), UiLanguage.Choose($"Parando {runner.Alias}...", $"Stopping {runner.Alias}..."), refreshAfter: false);
        await RefreshAsync();
    }

    private async Task ExecuteAction(Func<Task> action, string status, bool refreshAfter = true)
    {
        try
        {
            StatusText = status;
            await action();
            if (refreshAfter)
            {
                await Task.Delay(500);
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(status, ex);
            _dialogs.ShowError(UiLanguage.Choose($"No se pudo completar la acción.\n\n{ex.Message}\n\nSi el runner está instalado como servicio, prueba a ejecutar NRS Workbench como administrador.", $"Could not complete the action.\n\n{ex.Message}\n\nIf the runner is installed as a service, try running NRS Workbench as administrator."));
            StatusText = UiLanguage.Choose("La acción ha fallado", "Action failed");
        }
    }

    private bool ConfirmBusy(RunnerInfo runner)
    {
        if (runner.State != RunnerState.Busy || !_settingsService.Load().ConfirmStopBusy) return true;
        return _dialogs.Confirm(UiLanguage.Choose($"El runner '{runner.Alias}' está ejecutando un job.\n\nLa operación puede interrumpirlo y marcarlo como fallido.\n\n¿Continuar?", $"Runner '{runner.Alias}' is running a job.\n\nThis action may interrupt it and mark it as failed.\n\nContinue?"), "Runner BUSY");
    }

    private bool CanStartSelected() => SelectedRunner?.State == RunnerState.Stopped;
    private bool CanStopSelected() => SelectedRunner is not null && CanStop(SelectedRunner);
    private bool CanRestartSelected() => SelectedRunner?.State is RunnerState.Ready or RunnerState.Busy;
    private static bool CanStop(RunnerInfo runner) => runner.State is RunnerState.Ready or RunnerState.Busy or RunnerState.Starting;

    private void LoadSelectedLog()
    {
        LogText = SelectedRunner is null ? UiLanguage.Choose("Selecciona un runner para ver el log.", "Select a runner to view its log.") : _logs.ReadLatest(SelectedRunner.FolderPath);
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private void RaiseCounts()
    {
        RaisePropertyChanged(nameof(TotalCount));
        RaisePropertyChanged(nameof(ReadyCount));
        RaisePropertyChanged(nameof(BusyCount));
        RaisePropertyChanged(nameof(StoppedCount));
        RaisePropertyChanged(nameof(ErrorCount));
        RaisePropertyChanged(nameof(IssueCount));
        RaisePropertyChanged(nameof(HealthLabel));
        RaisePropertyChanged(nameof(HealthKind));
        RaisePropertyChanged(nameof(HealthDetail));
        RaisePropertyChanged(nameof(LastUpdatedText));
    }

    private void RaiseCommandStates()
    {
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        RestartCommand.RaiseCanExecuteChanged();
        StartAllCommand.RaiseCanExecuteChanged();
        StopAllCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        OpenDiagCommand.RaiseCanExecuteChanged();
        OpenGitHubCommand.RaiseCanExecuteChanged();
        ClearLogCommand.RaiseCanExecuteChanged();
        MoveRunnerUpCommand.RaiseCanExecuteChanged();
        MoveRunnerDownCommand.RaiseCanExecuteChanged();
    }
}

public sealed record RunnerSortOption(string Value, string Label);
