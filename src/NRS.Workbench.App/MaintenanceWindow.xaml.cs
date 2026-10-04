using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class MaintenanceWindow : Window
{
    private readonly MaintenanceService _service;
    private readonly Func<IReadOnlyList<RunnerInfo>> _runnerProvider;
    private CancellationTokenSource? _scanCancellation;
    private MaintenanceScanResult? _lastScan;

    public MaintenanceWindow(MaintenanceService service, Func<IReadOnlyList<RunnerInfo>> runnerProvider)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _service = service;
        _runnerProvider = runnerProvider;
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => _scanCancellation?.Cancel();
    }

    private int SelectedDays => int.TryParse((DaysChoice.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var days) ? days : 30;

    private async Task RefreshAsync()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        SetBusy(true);
        StatusText.Text = UiLanguage.Choose("Escaneando almacenamiento…", "Scanning storage…", "Escanejant l'emmagatzematge…");
        try
        {
            _lastScan = await _service.ScanAsync(_runnerProvider(), SelectedDays, _scanCancellation.Token);
            MaintenanceGrid.ItemsSource = _lastScan.Entries;
            MeasuredText.Text = _lastScan.TotalDisplay;
            ReclaimableText.Text = _lastScan.ReclaimableDisplay;
            StatusText.Text = UiLanguage.Choose($"{_lastScan.Entries.Count} bloque(s) revisados", $"{_lastScan.Entries.Count} block(s) reviewed", $"{_lastScan.Entries.Count} bloc(s) revisats");
        }
        catch (OperationCanceledException) { StatusText.Text = UiLanguage.Choose("Escaneo cancelado", "Scan cancelled", "Escaneig cancel·lat"); }
        catch (Exception ex)
        {
            AppLogger.Error("Maintenance scan failed", ex);
            StatusText.Text = UiLanguage.Choose("No se pudo completar el escaneo", "Could not complete the scan", "No s'ha pogut completar l'escaneig");
            MessageBox.Show(this, SensitiveDataRedactor.Redact(ex.Message), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void DaysChoice_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) await RefreshAsync(); }

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScan is null) return;
        var selected = _lastScan.Entries.Where(x => x.IsSelected && x.IsCleanable).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, UiLanguage.Choose("No hay elementos limpiables seleccionados.", "No cleanable items are selected.", "No hi ha elements netejables seleccionats."), UiLanguage.Text("Mantenimiento"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reclaimable = selected.Sum(x => x.ReclaimableBytes);
        var affectsHistory = selected.Any(x => x.Risk == MaintenanceRisk.History);
        var warning = affectsHistory
            ? UiLanguage.Choose("La selección incluye logs históricos de runners. Al borrarlos disminuirá el historial disponible para estadísticas y estimaciones.", "The selection includes historical runner logs. Removing them reduces history available to statistics and progress estimates.", "La selecció inclou logs històrics de runners. En esborrar-los disminuirà l'historial disponible per a estadístiques i estimacions.")
            : UiLanguage.Choose("La selección contiene únicamente datos clasificados como seguros para limpiar.", "The selection contains only data classified as safe to clean.", "La selecció només conté dades classificades com a segures per netejar.");

        if (MessageBox.Show(this,
            UiLanguage.Choose($"Se intentarán liberar hasta {MaintenanceService.FormatBytes(reclaimable)}.\n\n{warning}\n\n¿Continuar?", $"Up to {MaintenanceService.FormatBytes(reclaimable)} will be reclaimed.\n\n{warning}\n\nContinue?", $"S'intentaran alliberar fins a {MaintenanceService.FormatBytes(reclaimable)}.\n\n{warning}\n\nVols continuar?"),
            UiLanguage.Text("Mantenimiento"), MessageBoxButton.YesNo,
            affectsHistory ? MessageBoxImage.Warning : MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        SetBusy(true);
        try
        {
            var result = await _service.CleanupAsync(selected);
            MessageBox.Show(this,
                UiLanguage.Choose($"Limpieza completada.\n\nArchivos limpiados: {result.CleanedFiles}\nEspacio liberado: {MaintenanceService.FormatBytes(result.ReclaimedBytes)}\nBloques omitidos por actividad/cambio de seguridad: {result.SkippedEntries}\nFallos: {result.FailedFiles}", $"Cleanup completed.\n\nFiles cleaned: {result.CleanedFiles}\nSpace reclaimed: {MaintenanceService.FormatBytes(result.ReclaimedBytes)}\nBlocks skipped because activity/safety changed: {result.SkippedEntries}\nFailures: {result.FailedFiles}", $"Neteja completada.\n\nFitxers netejats: {result.CleanedFiles}\nEspai alliberat: {MaintenanceService.FormatBytes(result.ReclaimedBytes)}\nBlocs omesos perquè ha canviat l'activitat/seguretat: {result.SkippedEntries}\nErrors: {result.FailedFiles}"),
                UiLanguage.Text("Mantenimiento"), MessageBoxButton.OK, result.FailedFiles == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Maintenance cleanup failed", ex);
            MessageBox.Show(this, SensitiveDataRedactor.Redact(ex.Message), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (MaintenanceGrid.SelectedItem is not MaintenanceEntry entry || string.IsNullOrWhiteSpace(entry.Location)) return;
        try { if (Directory.Exists(entry.Location)) Process.Start(new ProcessStartInfo("explorer.exe", entry.Location) { UseShellExecute = true }); }
        catch (Exception ex) { AppLogger.Error("Could not open maintenance location", ex); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void SetBusy(bool busy) { CleanButton.IsEnabled = !busy; DaysChoice.IsEnabled = !busy; }
}
