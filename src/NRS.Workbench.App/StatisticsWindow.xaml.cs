using System.Windows;
using System.Windows.Controls;
using NRS.Workbench.App.Services;
using NRS.Workbench.App.ViewModels;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class StatisticsWindow : Window
{
    private readonly StatisticsViewModel _viewModel;
    private readonly CancellationTokenSource _closeCancellation = new();
    private bool _loaded;

    public StatisticsWindow(IRunnerStatisticsService statistics, Func<IReadOnlyList<RunnerInfo>> runnersProvider)
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            WindowThemeService.ApplyDarkTitleBar(this);
            WindowThemeService.FitToWorkArea(this);
        };

        var settings = new SettingsService();
        _viewModel = new StatisticsViewModel(statistics, runnersProvider, new GitService(), settings);
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            _loaded = true;
            await _viewModel.RefreshAsync(_closeCancellation.Token);
        };
        Closed += (_, _) =>
        {
            _closeCancellation.Cancel();
            _closeCancellation.Dispose();
        };
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    private async void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _viewModel is null) return;
        await _viewModel.RefreshAsync(_closeCancellation.Token);
    }
}