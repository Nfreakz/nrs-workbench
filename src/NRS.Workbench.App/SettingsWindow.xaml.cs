using System.Windows;
using Microsoft.Win32;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private RunnerSettings _workingSettings;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _settingsService = settingsService;
        _workingSettings = _settingsService.Load();
        ApplySettingsToForm(_workingSettings);
    }

    private void ApplySettingsToForm(RunnerSettings settings)
    {
        RootsText.Text = string.Join(Environment.NewLine, settings.RunnerRoots);
        PatternText.Text = settings.FolderPattern;
        RefreshText.Text = settings.RefreshIntervalSeconds.ToString();
        ConfirmBusyCheck.IsChecked = settings.ConfirmStopBusy;
        KeepInTrayCheck.IsChecked = settings.KeepInTray;
        NotificationsEnabledCheck.IsChecked = settings.NotificationsEnabled;
        NotifyJobStartedCheck.IsChecked = settings.NotifyJobStarted;
        NotifyJobCompletedCheck.IsChecked = settings.NotifyJobCompleted;
        NotifyRunnerIssuesCheck.IsChecked = settings.NotifyRunnerIssues;
        NotifyOnlyWhenHiddenCheck.IsChecked = settings.NotifyOnlyWhenHidden;
    }

    private bool TryApplyFormToWorkingSettings()
    {
        if (!int.TryParse(RefreshText.Text, out var refresh) || refresh < 2 || refresh > 300)
        {
            MessageBox.Show("El intervalo debe estar entre 2 y 300 segundos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _workingSettings.RunnerRoots = RootsText.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
        _workingSettings.FolderPattern = string.IsNullOrWhiteSpace(PatternText.Text) ? "actions-runner*" : PatternText.Text.Trim();
        _workingSettings.RefreshIntervalSeconds = refresh;
        _workingSettings.ConfirmStopBusy = ConfirmBusyCheck.IsChecked == true;
        _workingSettings.KeepInTray = KeepInTrayCheck.IsChecked == true;
        _workingSettings.NotificationsEnabled = NotificationsEnabledCheck.IsChecked == true;
        _workingSettings.NotifyJobStarted = NotifyJobStartedCheck.IsChecked == true;
        _workingSettings.NotifyJobCompleted = NotifyJobCompletedCheck.IsChecked == true;
        _workingSettings.NotifyRunnerIssues = NotifyRunnerIssuesCheck.IsChecked == true;
        _workingSettings.NotifyOnlyWhenHidden = NotifyOnlyWhenHiddenCheck.IsChecked == true;
        return true;
    }

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        var roots = _settingsService.DetectRunnerRoots(PatternText.Text);
        if (roots.Count == 0)
        {
            MessageBox.Show(
                "No he encontrado carpetas de runner en el nivel raíz de las unidades locales.\n\nPuedes añadir manualmente la carpeta que contiene actions-runner*.",
                "Detección automática",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        RootsText.Text = string.Join(Environment.NewLine, roots);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyFormToWorkingSettings()) return;

        var dialog = new SaveFileDialog
        {
            Title = "Exportar configuración de NRS Workbench",
            Filter = "Configuración NRS Workbench (*.json)|*.json|Todos los archivos (*.*)|*.*",
            FileName = $"NRSWorkbench-config-{DateTime.Now:yyyyMMdd}.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _settingsService.ExportPortableSettings(_workingSettings, dialog.FileName);
            MessageBox.Show(
                "Configuración exportada.\n\nEl archivo contiene rutas y preferencias, pero no credenciales, tokens ni archivos internos de los runners.",
                "Exportar configuración",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo exportar la configuración.\n\n{ex.Message}",
                "Exportar configuración",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar configuración de NRS Workbench",
            Filter = "Configuración NRS Workbench (*.json)|*.json|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var imported = _settingsService.ReadPortableSettings(dialog.FileName);
            _workingSettings = imported;
            ApplySettingsToForm(_workingSettings);

            var missingRunnerRoots = imported.RunnerRoots.Count(path => !Directory.Exists(path));
            var missingRepositories = imported.RepositoryPaths.Count(path => !Directory.Exists(path));
            var pathNote = missingRunnerRoots == 0 && missingRepositories == 0
                ? "Todas las rutas importadas existen en este PC."
                : $"Rutas no encontradas en este PC: {missingRunnerRoots} de runners y {missingRepositories} de repositorios. Puedes usar ‘Detectar automáticamente’ para ajustar los runners.";

            MessageBox.Show(
                $"Configuración cargada para revisar.\n\n{pathNote}\n\nPulsa Guardar para aplicarla o Cancelar para descartarla.",
                "Importar configuración",
                MessageBoxButton.OK,
                missingRunnerRoots == 0 && missingRepositories == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo importar la configuración.\n\n{ex.Message}",
                "Importar configuración",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyFormToWorkingSettings()) return;
        _settingsService.Save(_workingSettings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
