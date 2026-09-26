using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using NRS.Workbench.App.Services;
using NRS.Workbench.App.ViewModels;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _resourceTimer;
    private readonly SystemResourceService _systemResources = new();
    private readonly IRunnerStatisticsService _statistics;
    private readonly SettingsService _settings;
    private readonly TrayIconService _tray;
    private readonly RunnerQueueCoordinator _runnerQueue;
    private bool _allowExit;
    private readonly Dictionary<string, RunnerState> _previousStates = new(StringComparer.OrdinalIgnoreCase);
    private bool _stateSnapshotInitialized;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);

        _settings = new SettingsService();
        var processService = new RunnerProcessService();
        var serviceController = new WindowsServiceController();
        IRunnerProgressService progress = new RunnerProgressService();
        _statistics = new RunnerStatisticsService();
        IRunnerDiscoveryService discovery = new RunnerDiscoveryService(_settings, processService, serviceController, progress);
        IRunnerControlService control = new RunnerControlService(processService, serviceController);
        _runnerQueue = new RunnerQueueCoordinator(control,
            new FileRunnerQueueStateStore(_settings.SettingsDirectory));
        ILogReaderService logs = new LogReaderService();
        var dialogs = new DialogService(_settings);

        _viewModel = new MainViewModel(discovery, control, logs, _settings, dialogs);
        DataContext = _viewModel;

        _tray = new TrayIconService(
            show: () => Dispatcher.Invoke(ShowFromTray),
            refresh: () => Dispatcher.Invoke(() => _viewModel.RefreshCommand.Execute(null)),
            startAll: () => Dispatcher.Invoke(() => _viewModel.StartAllCommand.Execute(null)),
            stopAll: () => Dispatcher.Invoke(() => _viewModel.StopAllCommand.Execute(null)),
            about: () => Dispatcher.Invoke(ShowAbout),
            exit: () => Dispatcher.Invoke(ExitApplication));

        _timer = new DispatcherTimer();
        _timer.Tick += async (_, _) => await RefreshAndUpdateTrayAsync();
        _resourceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _resourceTimer.Tick += (_, _) => _viewModel.UpdateSystemResources(_systemResources.Read());

        Loaded += async (_, _) =>
        {
            ReviewRecoveredQueueStops();
            ApplyTimerInterval();
            ApplyTraySetting();
            _timer.Start();
            _viewModel.UpdateSystemResources(_systemResources.Read());
            _resourceTimer.Start();
            await RefreshAndUpdateTrayAsync();
        };

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _settings.Load().KeepInTray)
                HideToTray();
        };

        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            _timer.Stop();
            _resourceTimer.Stop();
            _tray.Dispose();
        };
    }

    private void ReviewRecoveredQueueStops()
    {
        var recovered = _runnerQueue.RecoveredQueuePaths;
        if (recovered.Count == 0) return;
        var folderNames = string.Join(Environment.NewLine,
            recovered.Take(8).Select(path => "  • " + Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))));
        if (recovered.Count > 8) folderNames += Environment.NewLine + "  …";

        var answer = MessageBox.Show(this,
            UiLanguage.Choose(
                $"NRS Workbench recuerda {recovered.Count} runner(s) que la cola gestionaba antes de cerrar la aplicación:{Environment.NewLine}{folderNames}{Environment.NewLine}{Environment.NewLine}¿Permitir que la cola los recupere? Comprueba que no los hayas detenido manualmente después. Si eliges No, seguirán detenidos.",
                $"NRS Workbench remembers {recovered.Count} runner(s) managed by the queue before the app closed:{Environment.NewLine}{folderNames}{Environment.NewLine}{Environment.NewLine}Allow the queue to resume them? Check that you have not manually stopped them since. Choosing No leaves them stopped."),
            UiLanguage.Text("Revisar runners tras reiniciar"),
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
            _runnerQueue.ApproveRecoveredQueueStops();
        else
            _runnerQueue.DiscardRecoveredQueueStops();
    }

    private async Task RefreshAndUpdateTrayAsync()
    {
        await _viewModel.RefreshAsync();
        var runners = _viewModel.AllRunners.ToList();
        _tray.Update(runners);
        ProcessStateTransitions(runners);
        var queueStatus = await _runnerQueue.ReconcileAsync(runners, _settings.Load());
        _viewModel.UpdateQueueStatus(queueStatus);
    }

    private void ProcessStateTransitions(IReadOnlyList<RunnerInfo> runners)
    {
        var settings = _settings.Load();

        if (!_stateSnapshotInitialized)
        {
            foreach (var runner in runners) _previousStates[runner.FolderPath] = runner.State;
            _stateSnapshotInitialized = true;
            return;
        }

        foreach (var runner in runners)
        {
            if (!_previousStates.TryGetValue(runner.FolderPath, out var previous))
            {
                _previousStates[runner.FolderPath] = runner.State;
                continue;
            }

            if (previous == runner.State) continue;

            if (settings.NotificationsEnabled && ShouldShowNotification(settings))
            {
                if (settings.NotifyJobStarted && previous == RunnerState.Ready && runner.State == RunnerState.Busy)
                {
                    _tray.ShowNotification(
                        UiLanguage.Choose($"{runner.Alias} · job iniciado", $"{runner.Alias} · job started"),
                        string.IsNullOrWhiteSpace(runner.GitHubTarget) ? UiLanguage.Choose("El runner ha empezado a procesar un job.", "The runner started processing a job.") : UiLanguage.Choose($"{runner.GitHubTarget} está en ejecución.", $"{runner.GitHubTarget} is running."));
                }
                else if (settings.NotifyJobCompleted && previous == RunnerState.Busy && runner.State == RunnerState.Ready)
                {
                    _tray.ShowNotification(
                        UiLanguage.Choose($"{runner.Alias} · job finalizado", $"{runner.Alias} · job completed"),
                        UiLanguage.Choose("El runner ha vuelto a READY y está disponible para el siguiente trabajo.", "The runner is READY and available for the next job."));
                }
                else if (settings.NotifyRunnerIssues && runner.State is RunnerState.Error or RunnerState.Unregistered)
                {
                    _tray.ShowNotification(
                        UiLanguage.Choose($"{runner.Alias} · requiere atención", $"{runner.Alias} · needs attention"),
                        UiLanguage.Choose($"El runner ha cambiado de {previous.ToString().ToUpperInvariant()} a {runner.State.ToString().ToUpperInvariant()}.", $"Runner changed from {previous.ToString().ToUpperInvariant()} to {runner.State.ToString().ToUpperInvariant()}."),
                        System.Windows.Forms.ToolTipIcon.Warning);
                }
            }

            _previousStates[runner.FolderPath] = runner.State;
        }

        var activePaths = runners.Select(x => x.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in _previousStates.Keys.Where(x => !activePaths.Contains(x)).ToList())
            _previousStates.Remove(missing);
    }

    private bool ShouldShowNotification(RunnerSettings settings)
    {
        if (!settings.NotifyOnlyWhenHidden) return true;
        var anyManagerWindowActive = Application.Current.Windows.OfType<Window>().Any(window => window.IsActive);
        return !anyManagerWindowActive;
    }

    private void Repositories_Click(object sender, RoutedEventArgs e)
    {
        var window = new RepositoriesWindow(_settings) { Owner = this };
        window.ShowDialog();
    }

    private void Statistics_Click(object sender, RoutedEventArgs e)
    {
        var window = new StatisticsWindow(_statistics, () => _viewModel.AllRunners.ToList())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.EditSettings(this))
        {
            ApplyTimerInterval();
            ApplyTraySetting();
            await RefreshAndUpdateTrayAsync();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e) => ShowAbout();

    private void ShowAbout()
    {
        ShowFromTray();
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var settings = _settings.Load();
        if (_allowExit || (!settings.KeepInTray && !settings.RunnerQueueEnabled)) return;
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        var settings = _settings.Load();
        if (!settings.KeepInTray && !settings.RunnerQueueEnabled) return;
        ShowInTaskbar = false;
        Hide();
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void ExitApplication()
    {
        if (_settings.Load().RunnerQueueEnabled &&
            MessageBox.Show(
                UiLanguage.Choose("La cola necesita que NRS Workbench siga activo. Si sales, los runners que sigan conectados pueden continuar aceptando jobs sin el límite configurado hasta que los pares o vuelvas a abrir la app. ¿Salir de todos modos?", "The queue needs NRS Workbench to remain active. If you exit, connected runners may keep accepting jobs without the configured limit until you stop them or reopen the app. Exit anyway?"),
                UiLanguage.Choose("Salir con la cola activa", "Exit with queue active"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        _allowExit = true;
        Close();
    }

    private void ApplyTimerInterval()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Clamp(_viewModel.RefreshIntervalSeconds, 2, 300));
    }

    private void ApplyTraySetting()
    {
        var settings = _settings.Load();
        _tray.SetVisible(settings.KeepInTray || settings.NotificationsEnabled);
    }
}
