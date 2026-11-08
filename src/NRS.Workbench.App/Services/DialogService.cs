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
        Show(UiLanguage.Text(message), UiLanguage.Text(title), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string message, string title = "NRS Workbench") =>
        Show(UiLanguage.Text(message), UiLanguage.Text(title), MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message, string title = "NRS Workbench") =>
        Show(UiLanguage.Text(message), UiLanguage.Text(title), MessageBoxButton.OK, MessageBoxImage.Information);

    private static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = ResolveOwner();
        return owner is null
            ? MessageBox.Show(message, title, buttons, image)
            : MessageBox.Show(owner, message, title, buttons, image);
    }

    private static Window? ResolveOwner()
    {
        var app = Application.Current;
        if (app is null) return null;
        var active = app.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive && window.IsVisible);
        if (active is not null) return active;
        var main = app.MainWindow;
        return main is { IsLoaded: true, IsVisible: true } ? main : null;
    }

    public bool EditSettings(Window owner)
    {
        var window = new SettingsWindow(_settingsService) { Owner = owner };
        return window.ShowDialog() == true;
    }
}
