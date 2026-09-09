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
    private bool _loaded;

    public StatisticsWindow(IRunnerStatisticsService statistics, Func<IReadOnlyList<RunnerInfo>> runnersProvider)
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            WindowThemeService.ApplyDarkTitleBar(this);
            FitToWorkArea();
        };
        _viewModel = new StatisticsViewModel(statistics, runnersProvider);
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            _loaded = true;
            await _viewModel.RefreshAsync();
        };
    }

    private void FitToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 24d;
        var availableWidth = Math.Max(900d, workArea.Width - margin);
        var availableHeight = Math.Max(650d, workArea.Height - margin);

        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    private async void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _viewModel is null) return;
        await _viewModel.RefreshAsync();
    }
}
