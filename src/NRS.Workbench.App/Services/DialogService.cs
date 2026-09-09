using System.Windows;
using NRS.Workbench.Core.Models;
using NRS.Workbench.App;

namespace NRS.Workbench.App.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);
    void ShowError(string message, string title = "NRS Workbench");
    void ShowInfo(string message, string title = "NRS Workbench");
    bool EditSettings(Window owner);
}

public sealed class DialogService : IDialogService
{
    private readonly SettingsService _settingsService;
    public DialogService(SettingsService settingsService) => _settingsService = settingsService;

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message, string title = "NRS Workbench") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message, string title = "NRS Workbench") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool EditSettings(Window owner)
    {
        var window = new SettingsWindow(_settingsService) { Owner = owner };
        return window.ShowDialog() == true;
    }
}
