using System.Collections.ObjectModel;
using System.Windows;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class PortableWorkspaceWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly RunnerSettings _settings;
    private readonly ObservableCollection<PortableRunnerCandidate> _candidates = [];
    private CancellationTokenSource? _operationCancellation;

    public PortableWorkspaceWindow(SettingsService settingsService, RunnerSettings settings)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);
        _settingsService = settingsService;
        _settings = settings;
        RunnerGrid.ItemsSource = _candidates;

        Loaded += async (_, _) =>
        {
            ApplyMode();
            if (AppDataPaths.IsPortable) await RefreshCandidatesAsync();
        };
        Closed += (_, _) =>
        {
            _operationCancellation?.Cancel();
            TokenBox.Clear();
        };
    }

    private void ApplyMode()
    {
        if (AppDataPaths.IsPortable)
        {
            ActivationPanel.Visibility = Visibility.Collapsed;
            PortablePanel.Visibility = Visibility.Visible;
            InactiveCloseButton.Visibility = Visibility.Collapsed;
            ModeBadge.Text = UiLanguage.Choose("PORTABLE ACTIVO", "PORTABLE ACTIVE", "PORTABLE ACTIU");
            ModeBadge.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x67, 0xAF, 0x87));
            PortablePathText.Text = UiLanguage.Choose(
                $"Datos: {AppDataPaths.SettingsDirectory} · Unidad: {AppDataPaths.PortableVolumeRoot}",
                $"Data: {AppDataPaths.SettingsDirectory} · Volume: {AppDataPaths.PortableVolumeRoot}",
                $"Dades: {AppDataPaths.SettingsDirectory} · Unitat: {AppDataPaths.PortableVolumeRoot}");
        }
        else
        {
            ActivationPanel.Visibility = Visibility.Visible;
            PortablePanel.Visibility = Visibility.Collapsed;
            InactiveCloseButton.Visibility = Visibility.Visible;
            ModeBadge.Text = UiLanguage.Choose("MODO NORMAL", "NORMAL MODE", "MODE NORMAL");
            ModeBadge.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
        }
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            UiLanguage.Choose(
                "Activa el modo portable desde el PC donde estas carpetas de runners ya están registradas y funcionan. NRS Workbench las asociará a este equipo en el manifiesto portable sin leer sus credenciales.\n\nSi esta copia y sus runners ya se han movido desde otro PC, cancela y prepara los runners después desde el modo portable.\n\n¿Activar ahora?",
                "Enable portable mode from the PC where these runner folders are already registered and working. NRS Workbench will associate them with this PC in the portable manifest without reading their credentials.\n\nIf this copy and its runners were already moved from another PC, cancel and prepare the runners later from portable mode.\n\nEnable now?",
                "Activa el mode portable des del PC on aquestes carpetes de runners ja estan registrades i funcionen. NRS Workbench les associarà a aquest equip al manifest portable sense llegir-ne les credencials.\n\nSi aquesta còpia i els runners ja s'han mogut des d'un altre PC, cancel·la i prepara els runners després des del mode portable.\n\nVols activar-lo ara?"),
            UiLanguage.Choose("Activar modo portable", "Enable portable mode", "Activar mode portable"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            var result = new PortableWorkspaceService().ActivateCurrentCopy(_settings);
            MessageBox.Show(this,
                UiLanguage.Choose(
                    $"Modo portable preparado.\n\nDatos: {result.DataDirectory}\nRunners registrados para este PC: {result.RecordedRunners}\n\nCierra y vuelve a abrir esta copia de NRS Workbench para usar el perfil portable.",
                    $"Portable mode is ready.\n\nData: {result.DataDirectory}\nRunners recorded for this PC: {result.RecordedRunners}\n\nClose and reopen this NRS Workbench copy to use the portable profile.",
                    $"Mode portable preparat.\n\nDades: {result.DataDirectory}\nRunners registrats per a aquest PC: {result.RecordedRunners}\n\nTanca i torna a obrir aquesta còpia de NRS Workbench per utilitzar el perfil portable."),
                UiLanguage.Choose("Modo portable", "Portable mode", "Mode portable"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not activate portable workspace", ex);
            MessageBox.Show(this, SensitiveDataRedactor.Redact(ex.Message), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshCandidatesAsync()
    {
        SetWorking(true);
        try
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();

            var service = new PortableRunnerPreparationService(_settingsService);
            var candidates = await service.DiscoverAsync(_settings, _operationCancellation.Token);
            _candidates.Clear();
            foreach (var candidate in candidates) _candidates.Add(candidate);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.Error("Could not inspect portable runners", ex);
            MessageBox.Show(this, SensitiveDataRedactor.Redact(ex.Message), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetWorking(false); }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCandidatesAsync();

    private async void Prepare_Click(object sender, RoutedEventArgs e)
    {
        var selected = _candidates.Where(x => x.IsSelected && x.CanPrepare).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this,
                UiLanguage.Choose("No hay runners seleccionados que necesiten preparación.", "No selected runners need preparation.", "No hi ha runners seleccionats que necessitin preparació."),
                UiLanguage.Choose("Espacio portable", "Portable workspace", "Espai portable"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pat = TokenBox.Password;
        if (string.IsNullOrWhiteSpace(pat))
        {
            MessageBox.Show(this,
                UiLanguage.Choose("Introduce un token de acceso de GitHub para esta operación.", "Enter a GitHub access token for this operation.", "Introdueix un token d'accés de GitHub per a aquesta operació."),
                UiLanguage.Choose("Espacio portable", "Portable workspace", "Espai portable"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this,
            UiLanguage.Choose(
                $"Se volverán a preparar {selected.Count} runner(s) interactivos para este PC. NRS Workbench conservará nombre, destino, workFolder, grupo y etiquetas.\n\nEl token no se guardará. ¿Continuar?",
                $"{selected.Count} interactive runner(s) will be prepared again for this PC. NRS Workbench will preserve name, target, workFolder, group and labels.\n\nThe token will not be stored. Continue?",
                $"Es tornaran a preparar {selected.Count} runner(s) interactius per a aquest PC. NRS Workbench conservarà nom, destí, workFolder, grup i etiquetes.\n\nEl token no es desarà. Vols continuar?"),
            UiLanguage.Choose("Preparar runners", "Prepare runners", "Preparar runners"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        SetWorking(true);
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();

        try
        {
            var service = new PortableRunnerPreparationService(_settingsService);
            foreach (var candidate in selected)
                candidate.ResultText = UiLanguage.Choose("Validando acceso…", "Validating access…", "Validant accés…");

            IReadOnlyDictionary<string, PortableRunnerPreflight> preflight;
            try
            {
                preflight = await service.PreflightAsync(selected, pat, _operationCancellation.Token);
            }
            catch (Exception ex)
            {
                foreach (var candidate in selected)
                    candidate.ResultText = UiLanguage.Choose("Sin cambios · prevalidación fallida", "No changes · preflight failed", "Sense canvis · prevalidació fallida");

                MessageBox.Show(
                    this,
                    UiLanguage.Choose(
                        $"No se ha modificado ningún runner. La prevalidación de GitHub falló antes de retirar configuraciones locales.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                        $"No runner was modified. GitHub preflight failed before any local configuration was removed.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                        $"No s'ha modificat cap runner. La prevalidació de GitHub ha fallat abans de retirar cap configuració local.\n\n{SensitiveDataRedactor.Redact(ex.Message)}"),
                    UiLanguage.Choose("Preparar runners", "Prepare runners", "Preparar runners"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            foreach (var candidate in selected)
            {
                _operationCancellation.Token.ThrowIfCancellationRequested();
                candidate.IsWorking = true;
                candidate.ResultText = UiLanguage.Choose("Preparando…", "Preparing…", "Preparant…");
                try
                {
                    var result = await service.PrepareAsync(
                        candidate,
                        preflight[candidate.FolderPath],
                        _operationCancellation.Token);
                    candidate.ResultText = result.Message;
                    candidate.IsSelected = false;
                }
                catch (Exception ex)
                {
                    candidate.ResultText = UiLanguage.Choose(
                        $"Error: {SensitiveDataRedactor.Redact(ex.Message)}",
                        $"Error: {SensitiveDataRedactor.Redact(ex.Message)}",
                        $"Error: {SensitiveDataRedactor.Redact(ex.Message)}");
                }
                finally { candidate.IsWorking = false; }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            TokenBox.Clear();
            pat = string.Empty;
            SetWorking(false);
        }

        await RefreshCandidatesAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SetWorking(bool working)
    {
        PrepareButton.IsEnabled = !working;
        TokenBox.IsEnabled = !working;
    }
}
