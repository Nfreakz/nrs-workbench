using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class FeedbackWindow : Window
{
    private const string BugUrl = "https://github.com/Nfreakz/nrs-workbench/issues/new?template=bug_report.yml";
    private const string FeatureUrl = "https://github.com/Nfreakz/nrs-workbench/issues/new?template=feature_request.yml";

    private readonly DiagnosticBundleService _diagnostics;
    private DiagnosticPreview _preview = new(string.Empty, false, 0);

    public FeedbackWindow(SettingsService settingsService)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _diagnostics = new DiagnosticBundleService(settingsService);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            _preview = _diagnostics.BuildPreview();
            PreviewText.Text = _preview.Text;
            PreviewSummaryText.Text = UiLanguage.Choose(
                _preview.IncludesApplicationLog
                    ? $"Vista previa preparada · log sanitizado incluido ({_preview.ApplicationLogCharacters:N0} caracteres)."
                    : "Vista previa preparada · no hay contenido de log disponible.",
                _preview.IncludesApplicationLog
                    ? $"Preview ready · sanitized app log included ({_preview.ApplicationLogCharacters:N0} characters)."
                    : "Preview ready · no application log content is available.",
                _preview.IncludesApplicationLog
                    ? $"Vista prèvia preparada · log sanejat inclòs ({_preview.ApplicationLogCharacters:N0} caràcters)."
                    : "Vista prèvia preparada · no hi ha contingut de log disponible.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not build diagnostic preview", ex);
            PreviewSummaryText.Text = UiLanguage.Choose("No se pudo preparar la vista previa.", "Could not prepare the preview.", "No s'ha pogut preparar la vista prèvia.");
            PreviewText.Text = SensitiveDataRedactor.Redact(ex.Message);
        }
    }

    private void ReportIssue_Click(object sender, RoutedEventArgs e) => OpenUrl(BugUrl);
    private void SuggestFeature_Click(object sender, RoutedEventArgs e) => OpenUrl(FeatureUrl);
    private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

    private void CopyPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(PreviewText.Text ?? string.Empty);
            PreviewSummaryText.Text = UiLanguage.Choose("Vista previa copiada al portapapeles.", "Preview copied to the clipboard.", "Vista prèvia copiada al porta-retalls.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not copy diagnostic preview", ex);
            MessageBox.Show(UiLanguage.Choose("No se pudo copiar la vista previa.", "Could not copy the preview.", "No s'ha pogut copiar la vista prèvia."),
                "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveBundle_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
        var dialog = new SaveFileDialog
        {
            Title = UiLanguage.Choose("Guardar diagnóstico de NRS Workbench", "Save NRS Workbench diagnostics", "Desar diagnòstic de NRS Workbench"),
            Filter = UiLanguage.Choose("Diagnóstico NRS Workbench (*.zip)|*.zip|Todos los archivos (*.*)|*.*", "NRS Workbench diagnostics (*.zip)|*.zip|All files (*.*)|*.*", "Diagnòstic NRS Workbench (*.zip)|*.zip|Tots els fitxers (*.*)|*.*"),
            FileName = $"NRSWorkbench-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            AddExtension = true,
            DefaultExt = ".zip",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _diagnostics.SaveZip(dialog.FileName, _preview);
            MessageBox.Show(
                UiLanguage.Choose("ZIP guardado localmente. No se ha enviado ni subido a ningún sitio. Revisa su contenido antes de compartirlo.",
                    "ZIP saved locally. Nothing was sent or uploaded. Review its contents before sharing it.",
                    "ZIP desat localment. No s'ha enviat ni pujat enlloc. Revisa'n el contingut abans de compartir-lo."),
                UiLanguage.Text("Feedback y diagnóstico"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not save diagnostic bundle", ex);
            MessageBox.Show(
                UiLanguage.Choose($"No se pudo guardar el ZIP.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                    $"Could not save the ZIP.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                    $"No s'ha pogut desar el ZIP.\n\n{SensitiveDataRedactor.Redact(ex.Message)}"),
                UiLanguage.Text("Feedback y diagnóstico"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            AppLogger.Error("Could not open feedback page", ex);
            MessageBox.Show(UiLanguage.Choose("No se pudo abrir GitHub en el navegador.", "Could not open GitHub in the browser.", "No s'ha pogut obrir GitHub al navegador."),
                "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
