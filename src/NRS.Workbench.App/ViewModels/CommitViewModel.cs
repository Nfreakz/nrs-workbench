using System.Collections.ObjectModel;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.ViewModels;

public sealed class CommitViewModel : ObservableObject
{
    private readonly IGitService _git;
    private readonly IDialogService _dialogs;
    private GitRepositoryInfo _repository;
    private GitFileChange? _selectedChange;
    private string _commitMessage = string.Empty;
    private string _diffText = UiLanguage.Choose("Selecciona un archivo para ver el diff.", "Select a file to view its diff.");
    private string _statusText = UiLanguage.Choose("Cargando cambios...", "Loading changes...");
    private bool _isBusy;

    public CommitViewModel(IGitService git, GitRepositoryInfo repository, IDialogService dialogs)
    {
        _git = git;
        _repository = repository;
        _dialogs = dialogs;
    }

    public ObservableCollection<GitFileChange> Changes { get; } = [];

    public GitRepositoryInfo Repository
    {
        get => _repository;
        private set
        {
            if (!SetProperty(ref _repository, value)) return;
            RaisePropertyChanged(nameof(RepositoryTitle));
            RaisePropertyChanged(nameof(BranchText));
            RaisePropertyChanged(nameof(CanPushAfterCommit));
        }
    }

    public GitFileChange? SelectedChange
    {
        get => _selectedChange;
        set => SetProperty(ref _selectedChange, value);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set => SetProperty(ref _commitMessage, value);
    }

    public string DiffText
    {
        get => _diffText;
        private set => SetProperty(ref _diffText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string RepositoryTitle => Repository.Name;
    public string BranchText => string.IsNullOrWhiteSpace(Repository.Branch) ? "—" : Repository.Branch;
    public bool HasConflicts => Changes.Any(x => x.IsConflict);
    public int SelectedCount => Changes.Count(x => x.IsSelected && !x.IsConflict);
    public bool CanPushAfterCommit => Repository.HasUpstream && Repository.Behind == 0 && Repository.ConflictCount == 0;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var current = await _git.InspectAsync(Repository.Path);
            Repository = current;
            var changes = await _git.GetChangesAsync(current);
            Changes.Clear();
            foreach (var change in changes) Changes.Add(change);
            foreach (var change in Changes) change.PropertyChanged += (_, _) => RaiseSelectionSummary();

            SelectedChange = Changes.FirstOrDefault();
            StatusText = Changes.Count == 0
                ? UiLanguage.Choose("El repositorio está limpio. No hay cambios para commitear.", "The repository is clean. There is nothing to commit.")
                : UiLanguage.Choose($"{Changes.Count} archivo{(Changes.Count == 1 ? string.Empty : "s")} con cambios · {SelectedCount} seleccionado{(SelectedCount == 1 ? string.Empty : "s")}.", $"{Changes.Count} changed files · {SelectedCount} selected.", $"{Changes.Count} fitxer{(Changes.Count == 1 ? string.Empty : "s")} amb canvis · {SelectedCount} seleccionat{(SelectedCount == 1 ? string.Empty : "s")}.");
            RaiseSelectionSummary();
            if (SelectedChange is not null) await LoadDiffAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load commit changes", ex);
            StatusText = UiLanguage.Choose("No se pudieron cargar los cambios.", "Could not load changes.");
            DiffText = ex.Message;
        }
        finally { IsBusy = false; }
    }

    public async Task LoadDiffAsync()
    {
        if (SelectedChange is null)
        {
            DiffText = UiLanguage.Choose("Selecciona un archivo para ver el diff.", "Select a file to view its diff.");
            return;
        }

        try
        {
            DiffText = UiLanguage.Choose("Cargando diff...", "Loading diff...");
            var diff = await _git.GetDiffAsync(Repository, SelectedChange);
            DiffText = LimitDiff(diff);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load diff", ex);
            DiffText = UiLanguage.Choose("No se pudo cargar el diff:\r\n", "Could not load diff:\r\n") + ex.Message;
        }
    }

    public void SetAllSelected(bool selected)
    {
        foreach (var change in Changes.Where(x => !x.IsConflict)) change.IsSelected = selected;
        RaiseSelectionSummary();
    }

    public async Task<bool> CommitAsync(bool pushAfter)
    {
        if (IsBusy) return false;
        var selected = Changes.Where(x => x.IsSelected && !x.IsConflict).ToList();
        var message = CommitMessage.Trim();

        if (HasConflicts)
        {
            _dialogs.ShowError(UiLanguage.Choose("Hay conflictos sin resolver. NRS Workbench no permitirá crear el commit hasta que Git vuelva a un estado seguro.", "There are unresolved conflicts. NRS Workbench cannot commit until Git is in a safe state."), UiLanguage.Choose("Commit bloqueado", "Commit blocked"));
            return false;
        }
        if (selected.Count == 0)
        {
            _dialogs.ShowError(UiLanguage.Choose("Selecciona al menos un archivo.", "Select at least one file."), "Commit");
            return false;
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            _dialogs.ShowError(UiLanguage.Choose("Escribe un mensaje de commit.", "Enter a commit message."), "Commit");
            return false;
        }
        if (pushAfter && !CanPushAfterCommit)
        {
            _dialogs.ShowError(UiLanguage.Choose("Commit & Push no está disponible: la rama no tiene upstream, el remoto va por delante o existe un conflicto. Puedes crear el commit local y resolver la sincronización después.", "Commit & Push is unavailable: there is no upstream, the remote is ahead, or there is a conflict. You can commit locally and resolve synchronization later."), "Commit & Push");
            return false;
        }

        var action = pushAfter ? "Commit & Push" : "Commit";
        var preview = string.Join("\r\n", selected.Take(10).Select(x => "• " + x.DisplayPath));
        if (selected.Count > 10) preview += UiLanguage.Choose($"\r\n• … y {selected.Count - 10} más", $"\r\n• … and {selected.Count - 10} more", $"\r\n• … i {selected.Count - 10} més");
        var warning = selected.Any(x => x.IsStaged && x.HasUnstagedChanges)
            ? UiLanguage.Choose("\r\n\r\nNota: algún archivo tiene cambios staged y sin stage. Al estar seleccionado, se incluirá su contenido actual completo.", "\r\n\r\nNote: some files have both staged and unstaged changes. Selecting them includes their full current content.")
            : string.Empty;

        if (!_dialogs.Confirm(UiLanguage.Choose($"{action} en {Repository.Name} · {BranchText}\r\n\r\nMensaje:\r\n{message}\r\n\r\nArchivos ({selected.Count}):\r\n{preview}{warning}\r\n\r\n¿Continuar?", $"{action} in {Repository.Name} · {BranchText}\r\n\r\nMessage:\r\n{message}\r\n\r\nFiles ({selected.Count}):\r\n{preview}{warning}\r\n\r\nContinue?", $"{action} a {Repository.Name} · {BranchText}\r\n\r\nMissatge:\r\n{message}\r\n\r\nFitxers ({selected.Count}):\r\n{preview}{warning}\r\n\r\nVols continuar?"), action))
            return false;

        IsBusy = true;
        try
        {
            StatusText = UiLanguage.Choose("Creando commit...", "Creating commit...");
            await _git.CommitAsync(Repository, message, selected);
            Repository = await _git.InspectAsync(Repository.Path);

            if (pushAfter)
            {
                if (!Repository.CanPush)
                {
                    _dialogs.ShowInfo(UiLanguage.Choose("El commit se ha creado localmente, pero el push ya no es seguro o no está disponible con el estado actual. El commit queda conservado localmente.", "The commit was created locally, but pushing is no longer safe or available. The local commit is preserved."), UiLanguage.Choose("Commit creado", "Commit created"));
                    return true;
                }
                StatusText = UiLanguage.Choose("Commit creado · enviando push...", "Commit created · pushing...");
                await _git.PushAsync(Repository);
                Repository = await _git.InspectAsync(Repository.Path);
            }

            StatusText = pushAfter ? UiLanguage.Choose("Commit y push completados.", "Commit and push completed.") : UiLanguage.Choose("Commit creado correctamente.", "Commit created successfully.");
            _dialogs.ShowInfo(pushAfter ? UiLanguage.Choose("Commit creado y enviado correctamente.", "Commit created and pushed successfully.") : UiLanguage.Choose("Commit creado correctamente.", "Commit created successfully."), action);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(action, ex);
            _dialogs.ShowError(ex.Message, action);
            StatusText = action + UiLanguage.Choose(" ha fallado.", " failed.");
            return false;
        }
        finally { IsBusy = false; }
    }

    private void RaiseSelectionSummary()
    {
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(HasConflicts));
        StatusText = Changes.Count == 0
            ? UiLanguage.Choose("El repositorio está limpio.", "The repository is clean.")
            : UiLanguage.Choose($"{SelectedCount} de {Changes.Count} archivo{(Changes.Count == 1 ? string.Empty : "s")} seleccionado{(SelectedCount == 1 ? string.Empty : "s")}.", $"{SelectedCount} of {Changes.Count} files selected.", $"{SelectedCount} de {Changes.Count} fitxer{(Changes.Count == 1 ? string.Empty : "s")} seleccionat{(SelectedCount == 1 ? string.Empty : "s")}.");
    }

    private static string LimitDiff(string text)
    {
        const int maxChars = 220_000;
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + UiLanguage.Choose("\r\n\r\n… diff truncado para mantener la interfaz fluida.", "\r\n\r\n… diff truncated to keep the interface responsive.");
    }
}
