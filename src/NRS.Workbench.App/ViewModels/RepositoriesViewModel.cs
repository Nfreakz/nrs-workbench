using System.Collections.ObjectModel;
using System.Diagnostics;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.ViewModels;

public sealed class RepositoriesViewModel : ObservableObject
{
    private readonly IGitService _git;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private GitRepositoryInfo? _selectedRepository;
    private string _statusText = UiLanguage.Choose("Preparando repositorios...", "Preparing repositories...");
    private string _gitVersionText = UiLanguage.Choose("Comprobando Git...", "Checking Git...");
    private DateTimeOffset _lastUpdated;

    public ObservableCollection<GitRepositoryInfo> Repositories { get; } = [];

    public GitRepositoryInfo? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            if (!SetProperty(ref _selectedRepository, value)) return;
            RaiseCommandStates();
        }
    }

    public int TotalCount => Repositories.Count;
    public int CleanCount => Repositories.Count(x => x.State == GitRepositoryState.Clean);
    public int ChangesCount => Repositories.Count(x => x.State == GitRepositoryState.Changes);
    public int AheadCount => Repositories.Count(x => x.Ahead > 0 && x.Behind == 0);
    public int BehindCount => Repositories.Count(x => x.Behind > 0 && x.Ahead == 0);
    public int AttentionCount => Repositories.Count(x => x.State is GitRepositoryState.Diverged or GitRepositoryState.Conflict or GitRepositoryState.Error);

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string GitVersionText { get => _gitVersionText; private set => SetProperty(ref _gitVersionText, value); }
    public string LastUpdatedText => _lastUpdated == default ? string.Empty : _lastUpdated.ToString("dd MMM yyyy  HH:mm:ss");

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand FetchCommand { get; }
    public AsyncRelayCommand PullCommand { get; }
    public AsyncRelayCommand PushCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenGitHubCommand { get; }
    public RelayCommand OpenTerminalCommand { get; }

    public RepositoriesViewModel(IGitService git, SettingsService settings, IDialogService dialogs)
    {
        _git = git;
        _settings = settings;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        FetchCommand = new AsyncRelayCommand(FetchSelectedAsync, () => SelectedRepository?.CanFetch == true);
        PullCommand = new AsyncRelayCommand(PullSelectedAsync, () => SelectedRepository?.CanPull == true);
        PushCommand = new AsyncRelayCommand(PushSelectedAsync, () => SelectedRepository?.CanPush == true);
        RemoveCommand = new RelayCommand(RemoveSelected, () => SelectedRepository is not null);
        OpenFolderCommand = new RelayCommand(() => OpenPath(SelectedRepository?.Path), () => SelectedRepository is not null);
        OpenGitHubCommand = new RelayCommand(() => OpenUrl(SelectedRepository?.GitHubUrl), () => !string.IsNullOrWhiteSpace(SelectedRepository?.GitHubUrl));
        OpenTerminalCommand = new RelayCommand(OpenTerminal, () => SelectedRepository is not null);
    }

    public async Task InitializeAsync()
    {
        try { GitVersionText = await _git.GetVersionAsync(); }
        catch (Exception ex)
        {
            AppLogger.Error("Could not detect Git", ex);
            GitVersionText = UiLanguage.Choose("Git no disponible", "Git unavailable");
        }
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            var selectedPath = SelectedRepository?.Path;
            var paths = _settings.Load().RepositoryPaths
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            StatusText = paths.Count == 0
                ? UiLanguage.Choose("Añade un repositorio o escanea una carpeta para empezar.", "Add a repository or scan a folder to get started.")
                : UiLanguage.Choose($"Actualizando {paths.Count} repositorio{(paths.Count == 1 ? string.Empty : "s")}...", $"Refreshing {paths.Count} repositories...", $"Actualitzant {paths.Count} repositori{(paths.Count == 1 ? string.Empty : "s")}...");

            var rows = new List<GitRepositoryInfo>();
            using var gate = new SemaphoreSlim(4, 4);
            var tasks = paths.Select(async path =>
            {
                await gate.WaitAsync();
                try { return await _git.InspectAsync(path); }
                catch (Exception ex)
                {
                    AppLogger.Error($"Could not inspect repository '{path}'", ex);
                    return new GitRepositoryInfo
                    {
                        Name = new DirectoryInfo(path).Name,
                        Path = path,
                        State = GitRepositoryState.Error,
                        ErrorMessage = ex.Message
                    };
                }
                finally { gate.Release(); }
            }).ToArray();
            rows.AddRange(await Task.WhenAll(tasks));

            Repositories.Clear();
            foreach (var row in rows.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) Repositories.Add(row);

            SelectedRepository = selectedPath is null
                ? Repositories.FirstOrDefault()
                : Repositories.FirstOrDefault(x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? Repositories.FirstOrDefault();

            _lastUpdated = DateTimeOffset.Now;
            StatusText = rows.Count == 0
                ? UiLanguage.Choose("Sin repositorios configurados. Usa Añadir repositorio o Escanear carpeta.", "No repositories configured. Use Add repository or Scan folder.")
                : UiLanguage.Choose($"{rows.Count} repositorio{(rows.Count == 1 ? string.Empty : "s")} · {CleanCount} clean · {ChangesCount} con cambios · {AttentionCount} con atención", $"{rows.Count} repositories · {CleanCount} clean · {ChangesCount} changed · {AttentionCount} need attention", $"{rows.Count} repositori{(rows.Count == 1 ? string.Empty : "s")} · {CleanCount} nets · {ChangesCount} amb canvis · {AttentionCount} requereixen atenció");
            RaiseSummary();
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Repository refresh failed", ex);
            StatusText = UiLanguage.Choose("No se pudieron actualizar los repositorios.", "Could not refresh repositories.");
        }
        finally { _refreshGate.Release(); }
    }

    public async Task AddRepositoryAsync(string path)
    {
        try
        {
            var info = await _git.InspectAsync(path);
            if (info.State == GitRepositoryState.Error)
            {
                _dialogs.ShowError(info.ErrorMessage, "Repositorio Git");
                return;
            }

            var settings = _settings.Load();
            if (!settings.RepositoryPaths.Contains(info.Path, StringComparer.OrdinalIgnoreCase))
            {
                settings.RepositoryPaths.Add(info.Path);
                _settings.Save(settings);
            }
            StatusText = UiLanguage.Choose($"Añadido {info.Name}.", $"Added {info.Name}.", $"Afegit {info.Name}.");
            await RefreshAsync();
            SelectedRepository = Repositories.FirstOrDefault(x => string.Equals(x.Path, info.Path, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not add repository", ex);
            _dialogs.ShowError(ex.Message, UiLanguage.Text("Añadir repositorio"));
        }
    }

    public async Task<int> ScanFolderAsync(string rootPath)
    {
        try
        {
            StatusText = UiLanguage.Choose($"Buscando repositorios en {rootPath}...", $"Finding repositories in {rootPath}...", $"Cercant repositoris a {rootPath}...");
            var found = await _git.FindRepositoriesAsync(rootPath, 3);
            var settings = _settings.Load();
            var before = settings.RepositoryPaths.Count;
            foreach (var path in found)
            {
                if (!settings.RepositoryPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    settings.RepositoryPaths.Add(path);
            }
            _settings.Save(settings);
            var added = settings.RepositoryPaths.Count - before;
            await RefreshAsync();
            StatusText = found.Count == 0
                ? UiLanguage.Choose("No se encontraron repositorios Git en la carpeta seleccionada.", "No Git repositories found in the selected folder.")
                : UiLanguage.Choose($"Escaneo completado · {found.Count} encontrados · {added} añadidos.", $"Scan complete · {found.Count} found · {added} added.", $"Exploració completada · {found.Count} trobats · {added} afegits.");
            return added;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Repository scan failed", ex);
            _dialogs.ShowError(ex.Message, UiLanguage.Choose("Escanear repositorios", "Scan repositories"));
            return 0;
        }
    }

    private async Task FetchSelectedAsync()
    {
        if (SelectedRepository is null) return;
        await ExecuteGitActionAsync(() => _git.FetchAsync(SelectedRepository), $"Fetch · {SelectedRepository.Name}");
    }

    private async Task PullSelectedAsync()
    {
        if (SelectedRepository is null) return;
        if (!_dialogs.Confirm(UiLanguage.Choose($"Se ejecutará git pull --ff-only en:\n\n{SelectedRepository.Name} · {SelectedRepository.Branch}\n{SelectedRepository.Path}\n\nEl pull se bloqueará si requiere merge o rebase.\n\n¿Continuar?", $"Run git pull --ff-only in:\n\n{SelectedRepository.Name} · {SelectedRepository.Branch}\n{SelectedRepository.Path}\n\nPull will stop if a merge or rebase is required.\n\nContinue?", $"S\u0027executarà git pull --ff-only a:\n\n{SelectedRepository.Name} · {SelectedRepository.Branch}\n{SelectedRepository.Path}\n\nEl pull es bloquejarà si requereix merge o rebase.\n\nVols continuar?"), UiLanguage.Choose("Pull seguro", "Safe pull"))) return;
        await ExecuteGitActionAsync(() => _git.PullFastForwardAsync(SelectedRepository), $"Pull · {SelectedRepository.Name}");
    }

    private async Task PushSelectedAsync()
    {
        if (SelectedRepository is null) return;
        if (!_dialogs.Confirm(UiLanguage.Choose($"Se enviarán {SelectedRepository.Ahead} commit{(SelectedRepository.Ahead == 1 ? string.Empty : "s")} a {SelectedRepository.Upstream}.\n\nRepositorio: {SelectedRepository.Name}\nRama: {SelectedRepository.Branch}\n\n¿Hacer push?", $"Send {SelectedRepository.Ahead} commits to {SelectedRepository.Upstream}.\n\nRepository: {SelectedRepository.Name}\nBranch: {SelectedRepository.Branch}\n\nPush?", $"S\u0027enviaran {SelectedRepository.Ahead} commit{(SelectedRepository.Ahead == 1 ? string.Empty : "s")} a {SelectedRepository.Upstream}.\n\nRepositori: {SelectedRepository.Name}\nBranca: {SelectedRepository.Branch}\n\nVols fer push?"), UiLanguage.Choose("Confirmar push", "Confirm push"))) return;
        await ExecuteGitActionAsync(() => _git.PushAsync(SelectedRepository), $"Push · {SelectedRepository.Name}");
    }

    private async Task ExecuteGitActionAsync(Func<Task> action, string label)
    {
        try
        {
            StatusText = label + "...";
            await action();
            await RefreshAsync();
            StatusText = label + UiLanguage.Choose(" completado.", " completed.");
        }
        catch (Exception ex)
        {
            AppLogger.Error(label, ex);
            _dialogs.ShowError(ex.Message, label);
            StatusText = label + UiLanguage.Choose(" ha fallado.", " failed.");
        }
    }

    private void RemoveSelected()
    {
        if (SelectedRepository is null) return;
        var repo = SelectedRepository;
        if (!_dialogs.Confirm(UiLanguage.Choose($"Quitar '{repo.Name}' del NRS Workbench?\n\nNo se borrará ninguna carpeta ni dato Git.", $"Remove '{repo.Name}' from NRS Workbench?\n\nNo folder or Git data will be deleted.", $"Treure '{repo.Name}' de NRS Workbench?\n\nNo s\u0027esborrarà cap carpeta ni dada de Git."), UiLanguage.Choose("Quitar repositorio", "Remove repository"))) return;

        var settings = _settings.Load();
        settings.RepositoryPaths.RemoveAll(x => string.Equals(x, repo.Path, StringComparison.OrdinalIgnoreCase));
        _settings.Save(settings);
        _ = RefreshAsync();
    }

    private void OpenTerminal()
    {
        if (SelectedRepository is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{SelectedRepository.Path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = SelectedRepository.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Could not open terminal", ex);
                _dialogs.ShowError("No se pudo abrir Windows Terminal ni cmd.exe.", "Terminal");
            }
        }
    }

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private void RaiseSummary()
    {
        RaisePropertyChanged(nameof(TotalCount));
        RaisePropertyChanged(nameof(CleanCount));
        RaisePropertyChanged(nameof(ChangesCount));
        RaisePropertyChanged(nameof(AheadCount));
        RaisePropertyChanged(nameof(BehindCount));
        RaisePropertyChanged(nameof(AttentionCount));
        RaisePropertyChanged(nameof(LastUpdatedText));
    }

    private void RaiseCommandStates()
    {
        FetchCommand.RaiseCanExecuteChanged();
        PullCommand.RaiseCanExecuteChanged();
        PushCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        OpenGitHubCommand.RaiseCanExecuteChanged();
        OpenTerminalCommand.RaiseCanExecuteChanged();
    }
}
