using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class RunnerDoctorWindow : Window
{
    private readonly RunnerDoctorService _doctor;
    private readonly Func<Task<IReadOnlyList<RunnerInfo>>> _runnerProvider;
    private IReadOnlyList<RunnerDoctorReport> _reports = [];

    public RunnerDoctorWindow(
        RunnerDoctorService doctor,
        Func<Task<IReadOnlyList<RunnerInfo>>> runnerProvider)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _doctor = doctor;
        _runnerProvider = runnerProvider;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private RunnerDoctorReport? SelectedReport => RunnerGrid.SelectedItem as RunnerDoctorReport;

    private async Task RefreshAsync()
    {
        try
        {
            ScopeText.Text = UiLanguage.Choose("Analizando runners...", "Analysing runners...", "Analitzant runners...");
            var runners = await _runnerProvider();
            _reports = await Task.Run(() => _doctor.Analyze(runners));

            DataContext = _reports;
            HealthyCountText.Text = _reports.Count(x => x.Severity == RunnerDoctorSeverity.Healthy).ToString();
            AttentionCountText.Text = _reports.Count(x => x.Severity == RunnerDoctorSeverity.Attention).ToString();
            ProblemCountText.Text = _reports.Count(x => x.Severity == RunnerDoctorSeverity.Problem).ToString();
            ScopeText.Text = UiLanguage.Choose(
                $"{_reports.Count} runner{(_reports.Count == 1 ? "" : "s")} · actualizado {DateTime.Now:HH:mm:ss}",
                $"{_reports.Count} runner{(_reports.Count == 1 ? "" : "s")} · updated {DateTime.Now:HH:mm:ss}",
                $"{_reports.Count} runner{(_reports.Count == 1 ? "" : "s")} · actualitzat {DateTime.Now:HH:mm:ss}");

            if (_reports.Count > 0)
            {
                var previousPath = SelectedReport?.Runner.FolderPath;
                RunnerGrid.SelectedItem = _reports.FirstOrDefault(x =>
                    string.Equals(x.Runner.FolderPath, previousPath, StringComparison.OrdinalIgnoreCase))
                    ?? _reports[0];
            }
            else
            {
                ChecksList.ItemsSource = null;
                SelectedAliasText.Text = UiLanguage.Choose("Sin runners detectados", "No runners detected", "No s'han detectat runners");
                SelectedMetaText.Text = UiLanguage.Choose("Revisa Configuración > Detectar automáticamente.", "Check Settings > Detect automatically.", "Revisa Configuració > Detectar automàticament.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Runner Doctor refresh failed", ex);
            ScopeText.Text = UiLanguage.Choose("No se pudo completar el diagnóstico.", "The diagnosis could not be completed.", "No s'ha pogut completar el diagnòstic.");
        }
    }

    private void RunnerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var report = SelectedReport;
        if (report is null)
        {
            ChecksList.ItemsSource = null;
            return;
        }

        ChecksList.ItemsSource = report.Checks;
        SelectedAliasText.Text = report.Runner.Alias;
        var target = string.IsNullOrWhiteSpace(report.Runner.GitHubTarget)
            ? UiLanguage.Choose("Destino no identificado", "Target not identified", "Destinació no identificada")
            : report.Runner.GitHubTarget;
        SelectedMetaText.Text = $"{report.HealthLabel} · {report.StateLabel} · {report.ModeLabel} · {target}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        OpenPath(SelectedReport?.Runner.FolderPath);

    private void OpenDiag_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectedReport is null ? null : Path.Combine(SelectedReport.Runner.FolderPath, "_diag");
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show(this,
                UiLanguage.Choose("Este runner no tiene una carpeta _diag disponible.", "This runner has no available _diag folder.", "Aquest runner no té cap carpeta _diag disponible."),
                "Runner Doctor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        OpenPath(path);
    }

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        var report = SelectedReport;
        if (report is null) return;

        var text = new StringBuilder()
            .AppendLine($"NRS Workbench · Runner Doctor · {report.Runner.Alias}")
            .AppendLine($"Health: {report.HealthLabel}")
            .AppendLine($"State: {report.StateLabel}")
            .AppendLine($"Mode: {report.ModeLabel}")
            .AppendLine($"Version: {report.VersionLabel}")
            .AppendLine($"Target: {report.Runner.GitHubTarget}")
            .AppendLine($"Last local activity: {report.LastActivityLabel}")
            .AppendLine();

        foreach (var check in report.Checks)
            text.AppendLine($"[{check.SeverityLabel}] {check.Title}: {check.Detail}");

        try
        {
            Clipboard.SetText(SensitiveDataRedactor.Redact(text.ToString()));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not copy Runner Doctor summary", ex);
        }
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
