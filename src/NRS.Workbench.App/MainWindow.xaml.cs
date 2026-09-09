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
    private readonly IRunnerStatisticsService _statistics;
    private readonly SettingsService _settings;
    private readonly TrayIconService _tray;
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

        Loaded += async (_, _) =>
        {
            ApplyTimerInterval();
            ApplyTraySetting();
            _timer.Start();
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
            _tray.Dispose();
        };
    }

    private async Task RefreshAndUpdateTrayAsync()
    {
        await _viewModel.RefreshAsync();
        var runners = _viewModel.Runners.ToList();
        _tray.Update(runners);
        ProcessStateTransitions(runners);
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
                        $"{runner.Alias} · job iniciado",
                        string.IsNullOrWhiteSpace(runner.GitHubTarget) ? "El runner ha empezado a procesar un job." : $"{runner.GitHubTarget} está en ejecución.");
                }
                else if (settings.NotifyJobCompleted && previous == RunnerState.Busy && runner.State == RunnerState.Ready)
                {
                    _tray.ShowNotification(
                        $"{runner.Alias} · job finalizado",
                        "El runner ha vuelto a READY y está disponible para el siguiente trabajo.");
                }
                else if (settings.NotifyRunnerIssues && runner.State is RunnerState.Error or RunnerState.Unregistered)
                {
                    _tray.ShowNotification(
                        $"{runner.Alias} · requiere atención",
                        $"El runner ha cambiado de {previous.ToString().ToUpperInvariant()} a {runner.State.ToString().ToUpperInvariant()}.",
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
        var window = new StatisticsWindow(_statistics, () => _viewModel.Runners.ToList())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.EditSettings(this))
        {
            ApplyTimerInterval();
            ApplyTraySetting();
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
        if (_allowExit || !_settings.Load().KeepInTray) return;
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        if (!_settings.Load().KeepInTray) return;
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
