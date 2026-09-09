using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class AboutWindow : Window
{
    private readonly SettingsService _settings = new();

    public AboutWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowThemeService.ApplyDarkTitleBar(this);

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = $"Versión {(version is null ? "desconocida" : $"{version.Major}.{version.Minor}.{version.Build}")}";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
        SystemText.Text = RuntimeInformation.OSDescription;
        DataPathText.Text = _settings.SettingsDirectory;
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
            MessageBox.Show("No se pudo abrir la carpeta de datos locales.", "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = $"NRS Workbench {VersionText.Text}\nCreado por Neo RS\n{RuntimeText.Text}\n{SystemText.Text}\nDatos: {_settings.SettingsDirectory}";
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not copy About information", ex);
            MessageBox.Show("No se pudo copiar la información al portapapeles.", "NRS Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
