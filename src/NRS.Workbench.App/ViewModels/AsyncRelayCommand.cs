using System.Windows.Input;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try { await _execute(); }
        catch (Exception ex)
        {
            // async void: an escaped exception would reach the global UI handler.
            AppLogger.Error("Command failed", ex);
            try
            {
                new DialogService(new SettingsService()).ShowError(UiLanguage.Choose(
                    $"No se pudo completar la acción.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                    $"The action could not be completed.\n\n{SensitiveDataRedactor.Redact(ex.Message)}",
                    $"No s'ha pogut completar l'acció.\n\n{SensitiveDataRedactor.Redact(ex.Message)}"));
            }
            catch (Exception dialogError)
            {
                AppLogger.Error("Could not show command error", dialogError);
            }
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
