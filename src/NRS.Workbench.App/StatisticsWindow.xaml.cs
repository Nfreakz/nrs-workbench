using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NRS.Workbench.App.Services;
using NRS.Workbench.App.ViewModels;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class StatisticsWindow : Window
{
    private readonly StatisticsViewModel _viewModel;
    private readonly IDialogService _dialogs;
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
        _dialogs = new DialogService(settings);
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

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsLoading) return;
        if (_viewModel.RecentRuns.Count == 0)
        {
            _dialogs.ShowInfo(
                UiLanguage.Choose("No hay jobs en el periodo seleccionado para exportar.", "There are no jobs in the selected period to export.", "No hi ha jobs al període seleccionat per exportar."),
                UiLanguage.Choose("Exportar CSV", "Export CSV", "Exportar CSV"));
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = UiLanguage.Choose("Exportar historial de jobs", "Export job history", "Exportar historial de jobs"),
            Filter = UiLanguage.Choose("CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*", "CSV (*.csv)|*.csv|All files (*.*)|*.*", "CSV (*.csv)|*.csv|Tots els fitxers (*.*)|*.*"),
            FileName = StatisticsViewModel.DefaultCsvFileName,
            AddExtension = true,
            DefaultExt = ".csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _viewModel.ExportCsv(dialog.FileName);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not export statistics CSV", ex);
            _dialogs.ShowError(
                UiLanguage.Choose($"No se pudo exportar el CSV.\n\n{SensitiveDataRedactor.Redact(ex.Message)}", $"Could not export the CSV.\n\n{SensitiveDataRedactor.Redact(ex.Message)}", $"No s'ha pogut exportar el CSV.\n\n{SensitiveDataRedactor.Redact(ex.Message)}"),
                UiLanguage.Choose("Exportar CSV", "Export CSV", "Exportar CSV"));
        }
    }

    private async void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || _viewModel is null) return;
        await _viewModel.RefreshAsync(_closeCancellation.Token);
    }
}