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
                "Configuración exportada.\n\nEl archivo contiene rutas y preferencias, pero no credenciales, tokens ni archivos internos de los runners.\n\nLas rutas locales pueden revelar nombres de carpetas o de usuario. Revísalo antes de compartir el archivo.",
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
            var automaticNotes = new List<string>();

            var missingRunnerRootsBeforeRepair = imported.RunnerRoots.Count(path => !Directory.Exists(path));
            if (imported.RunnerRoots.Count > 0 && missingRunnerRootsBeforeRepair == imported.RunnerRoots.Count)
            {
                var detectedRoots = _settingsService.DetectRunnerRoots(imported.FolderPattern);
                if (detectedRoots.Count > 0)
                {
                    imported.RunnerRoots = detectedRoots.ToList();
                    automaticNotes.Add($"Runners ajustados automáticamente a este PC: {string.Join(", ", detectedRoots)}.");
                }
            }

            var repairedRepositories = TryRepairImportedRepositoryPaths(imported);
            if (repairedRepositories > 0)
                automaticNotes.Add($"Repositorios reparados automáticamente por cambio de unidad: {repairedRepositories}.");

            _workingSettings = imported;
            ApplySettingsToForm(_workingSettings);

            var missingRunnerRoots = imported.RunnerRoots.Count(path => !Directory.Exists(path));
            var missingRepositories = imported.RepositoryPaths.Count(path => !Directory.Exists(path));
            var pathNote = missingRunnerRoots == 0 && missingRepositories == 0
                ? "Todas las rutas importadas existen en este PC."
                : $"Rutas no encontradas en este PC: {missingRunnerRoots} de runners y {missingRepositories} de repositorios.";

            var automaticNote = automaticNotes.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, automaticNotes) + Environment.NewLine + Environment.NewLine;

            MessageBox.Show(
                $"Configuración cargada para revisar.\n\n{automaticNote}{pathNote}\n\nPulsa Guardar para aplicarla o Cancelar para descartarla.",
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

    private static int TryRepairImportedRepositoryPaths(RunnerSettings settings)
    {
        var driveRoots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                driveRoots.Add(drive.RootDirectory.FullName);
            }
            catch
            {
                // A drive can disappear while the import dialog is open. Skip it.
            }
        }

        var repaired = 0;
        for (var index = 0; index < settings.RepositoryPaths.Count; index++)
        {
            var originalPath = settings.RepositoryPaths[index];
            if (Directory.Exists(originalPath)) continue;

            string? sourceRoot;
            try
            {
                sourceRoot = Path.GetPathRoot(Path.GetFullPath(originalPath));
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceRoot)) continue;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(sourceRoot, originalPath);
            }
            catch
            {
                continue;
            }

            if (Path.IsPathRooted(relativePath) ||
                relativePath.Equals("..", StringComparison.Ordinal) ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            var matches = new List<string>();
            foreach (var driveRoot in driveRoots)
            {
                if (string.Equals(driveRoot, sourceRoot, StringComparison.OrdinalIgnoreCase)) continue;

                string candidate;
                try
                {
                    candidate = Path.GetFullPath(Path.Combine(driveRoot, relativePath));
                }
                catch
                {
                    continue;
                }

                if (!Directory.Exists(candidate)) continue;
                var gitMarker = Path.Combine(candidate, ".git");
                if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker)) continue;

                if (!matches.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    matches.Add(candidate);
            }

            if (matches.Count != 1) continue;
            settings.RepositoryPaths[index] = matches[0];
            repaired++;
        }

        settings.RepositoryPaths = settings.RepositoryPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return repaired;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyFormToWorkingSettings()) return;
        _settingsService.Save(_workingSettings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
