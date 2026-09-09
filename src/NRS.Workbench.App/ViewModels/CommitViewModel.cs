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
    private string _diffText = "Selecciona un archivo para ver el diff.";
    private string _statusText = "Cargando cambios...";
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
                ? "El repositorio está limpio. No hay cambios para commitear."
                : $"{Changes.Count} archivo{(Changes.Count == 1 ? string.Empty : "s")} con cambios · {SelectedCount} seleccionado{(SelectedCount == 1 ? string.Empty : "s")}.";
            RaiseSelectionSummary();
            if (SelectedChange is not null) await LoadDiffAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load commit changes", ex);
            StatusText = "No se pudieron cargar los cambios.";
            DiffText = ex.Message;
        }
        finally { IsBusy = false; }
    }

    public async Task LoadDiffAsync()
    {
        if (SelectedChange is null)
        {
            DiffText = "Selecciona un archivo para ver el diff.";
            return;
        }

        try
        {
            DiffText = "Cargando diff...";
            var diff = await _git.GetDiffAsync(Repository, SelectedChange);
            DiffText = LimitDiff(diff);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load diff", ex);
            DiffText = "No se pudo cargar el diff:\r\n" + ex.Message;
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
            _dialogs.ShowError("Hay conflictos sin resolver. NRS Workbench no permitirá crear el commit hasta que Git vuelva a un estado seguro.", "Commit bloqueado");
            return false;
        }
        if (selected.Count == 0)
        {
            _dialogs.ShowError("Selecciona al menos un archivo.", "Commit");
            return false;
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            _dialogs.ShowError("Escribe un mensaje de commit.", "Commit");
            return false;
        }
        if (pushAfter && !CanPushAfterCommit)
        {
            _dialogs.ShowError("Commit & Push no está disponible: la rama no tiene upstream, el remoto va por delante o existe un conflicto. Puedes crear el commit local y resolver la sincronización después.", "Commit & Push");
            return false;
        }

        var action = pushAfter ? "Commit & Push" : "Commit";
        var preview = string.Join("\r\n", selected.Take(10).Select(x => "• " + x.DisplayPath));
        if (selected.Count > 10) preview += $"\r\n• … y {selected.Count - 10} más";
        var warning = selected.Any(x => x.IsStaged && x.HasUnstagedChanges)
            ? "\r\n\r\nNota: algún archivo tiene cambios staged y sin stage. Al estar seleccionado, se incluirá su contenido actual completo."
            : string.Empty;

        if (!_dialogs.Confirm($"{action} en {Repository.Name} · {BranchText}\r\n\r\nMensaje:\r\n{message}\r\n\r\nArchivos ({selected.Count}):\r\n{preview}{warning}\r\n\r\n¿Continuar?", action))
            return false;

        IsBusy = true;
        try
        {
            StatusText = "Creando commit...";
            await _git.CommitAsync(Repository, message, selected);
            Repository = await _git.InspectAsync(Repository.Path);

            if (pushAfter)
            {
                if (!Repository.CanPush)
                {
                    _dialogs.ShowInfo("El commit se ha creado localmente, pero el push ya no es seguro o no está disponible con el estado actual. El commit queda conservado localmente.", "Commit creado");
                    return true;
                }
                StatusText = "Commit creado · enviando push...";
                await _git.PushAsync(Repository);
                Repository = await _git.InspectAsync(Repository.Path);
            }

            StatusText = pushAfter ? "Commit y push completados." : "Commit creado correctamente.";
            _dialogs.ShowInfo(pushAfter ? "Commit creado y enviado correctamente." : "Commit creado correctamente.", action);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(action, ex);
            _dialogs.ShowError(ex.Message, action);
            StatusText = action + " ha fallado.";
            return false;
        }
        finally { IsBusy = false; }
    }

    private void RaiseSelectionSummary()
    {
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(HasConflicts));
        StatusText = Changes.Count == 0
            ? "El repositorio está limpio."
            : $"{SelectedCount} de {Changes.Count} archivo{(Changes.Count == 1 ? string.Empty : "s")} seleccionado{(SelectedCount == 1 ? string.Empty : "s")}.";
    }

    private static string LimitDiff(string text)
    {
        const int maxChars = 220_000;
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\r\n\r\n… diff truncado para mantener la interfaz fluida.";
    }
}
