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
        RunnerQueueEnabledCheck.IsChecked = settings.RunnerQueueEnabled;
        RunnerQueueLimitChoice.SelectedValue = settings.RunnerQueueLimit.ToString();
        RunnerQueueResourceGuardCheck.IsChecked = settings.RunnerQueueResourceGuardEnabled;
        RunnerQueueCpuThresholdText.Text = settings.RunnerQueueCpuStartThreshold.ToString();
        RunnerQueueMemoryThresholdText.Text = settings.RunnerQueueMemoryStartThreshold.ToString();
        NotificationsEnabledCheck.IsChecked = settings.NotificationsEnabled;
        NotifyJobStartedCheck.IsChecked = settings.NotifyJobStarted;
        NotifyJobCompletedCheck.IsChecked = settings.NotifyJobCompleted;
        NotifyRunnerIssuesCheck.IsChecked = settings.NotifyRunnerIssues;
        NotifyOnlyWhenHiddenCheck.IsChecked = settings.NotifyOnlyWhenHidden;
        LanguageChoice.SelectedValue = settings.Language;
    }

    private bool TryApplyFormToWorkingSettings()
    {
        if (!int.TryParse(RefreshText.Text, out var refresh) || refresh < 2 || refresh > 300)
        {
            MessageBox.Show(UiLanguage.Choose("El intervalo debe estar entre 2 y 300 segundos.", "The interval must be between 2 and 300 seconds."), UiLanguage.Text("Configuración"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
        _workingSettings.RunnerQueueEnabled = RunnerQueueEnabledCheck.IsChecked == true;
        _workingSettings.RunnerQueueLimit = int.TryParse(RunnerQueueLimitChoice.SelectedValue as string, out var queueLimit) ? queueLimit : 2;
        if (!int.TryParse(RunnerQueueCpuThresholdText.Text, out var cpuThreshold) || cpuThreshold < 50 || cpuThreshold > 100 ||
            !int.TryParse(RunnerQueueMemoryThresholdText.Text, out var memoryThreshold) || memoryThreshold < 50 || memoryThreshold > 100)
        {
            MessageBox.Show(
                UiLanguage.Choose(
                    "Los límites de CPU y RAM deben estar entre 50 y 100%.",
                    "CPU and RAM thresholds must be between 50 and 100%.",
                    "Els límits de CPU i RAM han d'estar entre 50 i 100%."),
                UiLanguage.Text("Configuración"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        _workingSettings.RunnerQueueResourceGuardEnabled = RunnerQueueResourceGuardCheck.IsChecked == true;
        _workingSettings.RunnerQueueCpuStartThreshold = cpuThreshold;
        _workingSettings.RunnerQueueMemoryStartThreshold = memoryThreshold;
        _workingSettings.NotificationsEnabled = NotificationsEnabledCheck.IsChecked == true;
        _workingSettings.NotifyJobStarted = NotifyJobStartedCheck.IsChecked == true;
        _workingSettings.NotifyJobCompleted = NotifyJobCompletedCheck.IsChecked == true;
        _workingSettings.NotifyRunnerIssues = NotifyRunnerIssuesCheck.IsChecked == true;
        _workingSettings.NotifyOnlyWhenHidden = NotifyOnlyWhenHiddenCheck.IsChecked == true;
        _workingSettings.Language = LanguageChoice.SelectedValue as string ?? "es";
        return true;
    }

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        var roots = _settingsService.DetectRunnerRoots(PatternText.Text);
        if (roots.Count == 0)
        {
            MessageBox.Show(
                UiLanguage.Choose("No he encontrado carpetas de runner en el nivel raíz de las unidades locales.\n\nPuedes añadir manualmente la carpeta que contiene actions-runner*.", "No runner folders were found at the root of local drives.\n\nYou can add the folder containing actions-runner* manually."),
                UiLanguage.Choose("Detección automática", "Automatic detection"),
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
            Title = UiLanguage.Choose("Exportar configuración de NRS Workbench", "Export NRS Workbench settings"),
            Filter = UiLanguage.Choose("Configuración NRS Workbench (*.json)|*.json|Todos los archivos (*.*)|*.*", "NRS Workbench settings (*.json)|*.json|All files (*.*)|*.*"),
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
                UiLanguage.Choose("Configuración exportada.\n\nEl archivo contiene rutas y preferencias, pero no credenciales, tokens ni archivos internos de los runners.\n\nLas rutas locales pueden revelar nombres de carpetas o de usuario. Revísalo antes de compartir el archivo.", "Settings exported.\n\nThe file contains paths and preferences, but no runner credentials, tokens or internal files.\n\nLocal paths may reveal folder or user names. Review the file before sharing it."),
                UiLanguage.Choose("Exportar configuración", "Export settings"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                UiLanguage.Choose($"No se pudo exportar la configuración.\n\n{ex.Message}", $"Could not export settings.\n\n{ex.Message}", $"No s\u0027ha pogut exportar la configuració.\n\n{ex.Message}"),
                UiLanguage.Choose("Exportar configuración", "Export settings"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = UiLanguage.Choose("Importar configuración de NRS Workbench", "Import NRS Workbench settings"),
            Filter = UiLanguage.Choose("Configuración NRS Workbench (*.json)|*.json|Todos los archivos (*.*)|*.*", "NRS Workbench settings (*.json)|*.json|All files (*.*)|*.*"),
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
                if (PortableRepositoryPaths.RepairRunnerRoots(imported, detectedRoots))
                    automaticNotes.Add(UiLanguage.Choose($"Runners ajustados automáticamente a este PC: {string.Join(", ", imported.RunnerRoots)}.", $"Runner roots adjusted for this PC: {string.Join(", ", imported.RunnerRoots)}.", $"Runners ajustats automàticament a aquest PC: {string.Join(", ", imported.RunnerRoots)}."));
            }

            var repairedRepositories = PortableRepositoryPaths.Repair(imported);
            if (repairedRepositories > 0)
                automaticNotes.Add(UiLanguage.Choose($"Repositorios reparados automáticamente por cambio de unidad: {repairedRepositories}.", $"Repository paths repaired by drive substitution: {repairedRepositories}.", $"Repositoris reparats automàticament pel canvi d\u0027unitat: {repairedRepositories}."));

            var existingRepositories = _workingSettings.RepositoryPaths.ToList();
            var importedSet = imported.RepositoryPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var addedCount = imported.RepositoryPaths.Count(path => !existingRepositories.Contains(path, StringComparer.OrdinalIgnoreCase));
            var excluded = existingRepositories.Where(path => !importedSet.Contains(path)).ToList();
            if (excluded.Count > 0)
            {
                var preview = string.Join(Environment.NewLine, excluded.Take(6).Select(path => "  " + path));
                if (excluded.Count > 6) preview += UiLanguage.Choose($"{Environment.NewLine}  ... y {excluded.Count - 6} más.", $"{Environment.NewLine}  ... and {excluded.Count - 6} more.", $"{Environment.NewLine}  ... i {excluded.Count - 6} més.");
                var choice = MessageBox.Show(
                    this,
                    UiLanguage.Choose(
                        $"El archivo aporta {addedCount} repositorio(s) nuevo(s), pero no incluye {excluded.Count} repositorio(s) configurado(s) actualmente en este PC:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Sí: reemplazar la lista local por la importada (no se borran carpetas).{Environment.NewLine}No: conservar los repositorios locales y añadir los del archivo.{Environment.NewLine}Cancelar: descartar la importación sin cambiar la configuración.",
                        $"The file adds {addedCount} repositories, but omits {excluded.Count} repositories currently configured on this PC:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Yes: replace the local list (no folders are deleted).{Environment.NewLine}No: keep local repositories and add those from the file.{Environment.NewLine}Cancel: discard the import without changing settings."),
                    UiLanguage.Choose("Revisar repositorios antes de importar", "Review repositories before importing"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (choice is MessageBoxResult.Cancel or MessageBoxResult.None) return;
                if (choice == MessageBoxResult.No)
                {
                    var kept = PortableRepositoryPaths.PreserveExisting(imported, existingRepositories);
                    automaticNotes.Add(UiLanguage.Choose($"Repositorios locales conservados: {kept}.", $"Local repositories retained: {kept}.", $"Repositoris locals conservats: {kept}."));
                }
                else automaticNotes.Add(UiLanguage.Choose($"Al guardar se reemplazará la lista local; {excluded.Count} registro(s) dejarán de figurar (no se borrarán carpetas).", $"Saving will replace the local list; {excluded.Count} registrations will be removed (no folders are deleted).", $"En desar se substituirà la llista local; {excluded.Count} registre(s) deixaran de figurar (no s\u0027esborrarà cap carpeta)."));
            }

            _workingSettings = imported;
            ApplySettingsToForm(_workingSettings);

            var missingRunnerRoots = imported.RunnerRoots.Count(path => !Directory.Exists(path));
            var missingRepositories = imported.RepositoryPaths.Count(path => !Directory.Exists(path));
            var pathNote = missingRunnerRoots == 0 && missingRepositories == 0
                ? UiLanguage.Choose("Todas las rutas importadas existen en este PC.", "All imported paths exist on this PC.")
                : UiLanguage.Choose($"Rutas no encontradas en este PC: {missingRunnerRoots} de runners y {missingRepositories} de repositorios.", $"Paths not found on this PC: {missingRunnerRoots} runner roots and {missingRepositories} repositories.", $"Rutes no trobades en aquest PC: {missingRunnerRoots} de runners i {missingRepositories} de repositoris.");

            var automaticNote = automaticNotes.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, automaticNotes) + Environment.NewLine + Environment.NewLine;

            MessageBox.Show(
                UiLanguage.Choose($"Configuración cargada para revisar.\n\n{automaticNote}{pathNote}\n\nPulsa Guardar para aplicarla o Cancelar para descartarla.", $"Settings loaded for review.\n\n{automaticNote}{pathNote}\n\nSelect Save to apply them or Cancel to discard them.", $"Configuració carregada per revisar.\n\n{automaticNote}{pathNote}\n\nPrem Desar per aplicar-la o Cancel·lar per descartar-la."),
                UiLanguage.Choose("Importar configuración", "Import settings"),
                MessageBoxButton.OK,
                missingRunnerRoots == 0 && missingRepositories == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                UiLanguage.Choose($"No se pudo importar la configuración.\n\n{ex.Message}", $"Could not import settings.\n\n{ex.Message}", $"No s\u0027ha pogut importar la configuració.\n\n{ex.Message}"),
                UiLanguage.Choose("Importar configuración", "Import settings"),
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
