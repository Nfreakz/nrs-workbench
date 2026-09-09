using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _settingsService = settingsService;
        var settings = _settingsService.Load();
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RefreshText.Text, out var refresh) || refresh < 2 || refresh > 300)
        {
            MessageBox.Show("El intervalo debe estar entre 2 y 300 segundos.", "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = _settingsService.Load();
        settings.RunnerRoots = RootsText.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
        settings.FolderPattern = string.IsNullOrWhiteSpace(PatternText.Text) ? "actions-runner*" : PatternText.Text.Trim();
        settings.RefreshIntervalSeconds = refresh;
        settings.ConfirmStopBusy = ConfirmBusyCheck.IsChecked == true;
        settings.KeepInTray = KeepInTrayCheck.IsChecked == true;
        settings.NotificationsEnabled = NotificationsEnabledCheck.IsChecked == true;
        settings.NotifyJobStarted = NotifyJobStartedCheck.IsChecked == true;
        settings.NotifyJobCompleted = NotifyJobCompletedCheck.IsChecked == true;
        settings.NotifyRunnerIssues = NotifyRunnerIssuesCheck.IsChecked == true;
        settings.NotifyOnlyWhenHidden = NotifyOnlyWhenHiddenCheck.IsChecked == true;
        _settingsService.Save(settings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
