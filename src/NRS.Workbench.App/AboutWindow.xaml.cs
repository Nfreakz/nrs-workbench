using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class AboutWindow : Window
{
    private const string SupportUrl = "https://buymeacoffee.com/neors";
    private readonly SettingsService _settings = new();

    public AboutWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = UiLanguage.Choose("Versión ", "Version ") + (version is null ? UiLanguage.Choose("desconocida", "unknown") : $"{version.Major}.{version.Minor}.{version.Build}");
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        SystemText.Text = RuntimeInformation.OSDescription;
        DataPathText.Text = _settings.SettingsDirectory;
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(SupportUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not open support page", ex);
            MessageBox.Show(UiLanguage.Choose("No se pudo abrir la página de apoyo.", "Could not open the support page."), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_settings.SettingsDirectory);
            Process.Start(new ProcessStartInfo(_settings.SettingsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not open local data directory", ex);
            MessageBox.Show(UiLanguage.Choose("No se pudo abrir la carpeta de datos locales.", "Could not open the local data folder."), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = UiLanguage.Choose($"NRS Workbench {VersionText.Text}\nCreado por Neo RS\n{RuntimeText.Text}\n{SystemText.Text}\nDatos: {_settings.SettingsDirectory}", $"NRS Workbench {VersionText.Text}\nCreated by Neo RS\n{RuntimeText.Text}\n{SystemText.Text}\nData: {_settings.SettingsDirectory}", $"NRS Workbench {VersionText.Text}\nCreat per Neo RS\n{RuntimeText.Text}\n{SystemText.Text}\nDades: {_settings.SettingsDirectory}");
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not copy About information", ex);
            MessageBox.Show(UiLanguage.Choose("No se pudo copiar la información al portapapeles.", "Could not copy information to the clipboard."), "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
