using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Initialize();
        UiLanguage.Select(new SettingsService().Load().Language);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("Unhandled AppDomain exception", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Unhandled UI exception", args.Exception);
            var choice = MessageBox.Show(
                UiLanguage.Choose(
                    $"Se ha producido un error inesperado.\n\n{SensitiveDataRedactor.Redact(args.Exception.Message)}\n\nEl detalle se ha guardado en el log local. ¿Abrir Feedback y diagnóstico para revisarlo o exportarlo?",
                    $"An unexpected error occurred.\n\n{SensitiveDataRedactor.Redact(args.Exception.Message)}\n\nDetails were saved to the local log. Open Feedback & diagnostics to review or export them?",
                    $"S'ha produït un error inesperat.\n\n{SensitiveDataRedactor.Redact(args.Exception.Message)}\n\nEl detall s'ha desat al log local. Vols obrir Feedback i diagnòstic per revisar-lo o exportar-lo?"),
                "NRS Workbench",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error,
                MessageBoxResult.Yes);

            if (choice == MessageBoxResult.Yes)
            {
                try
                {
                    var feedback = new FeedbackWindow(new SettingsService());
                    if (Current?.MainWindow is { IsLoaded: true } owner) feedback.Owner = owner;
                    feedback.ShowDialog();
                }
                catch (Exception feedbackError)
                {
                    AppLogger.Error("Could not open feedback window after UI exception", feedbackError);
                }
            }

            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
