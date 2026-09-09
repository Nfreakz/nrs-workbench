using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("Unhandled AppDomain exception", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"Se ha producido un error inesperado.\n\n{args.Exception.Message}\n\nConsulta el log de NRS Workbench para más detalles.",
                "NRS Workbench",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
