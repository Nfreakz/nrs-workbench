using System.Windows;
using NRS.Workbench.App.Services;

namespace NRS.Workbench.App;

public partial class App : Application
{
    private readonly FaultBurstThrottle _uiFaultDialogs = new(TimeSpan.FromMinutes(1));
    private RuntimeSessionTracker? _runtimeSession;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Initialize();
        _runtimeSession = new RuntimeSessionTracker(AppDataPaths.SettingsDirectory);
        var previousSession = _runtimeSession.BeginSession(AppDataPaths.IsPreview);
        if (previousSession is not null)
        {
            AppLogger.Error(
                $"Previous NRS Workbench session ended without a clean shutdown. " +
                $"session={previousSession.SessionId} pid={previousSession.ProcessId} " +
                $"started={previousSession.StartedAt:O} lastHeartbeat={previousSession.LastHeartbeatAt:O} " +
                $"preview={previousSession.Preview}");
        }

        UiLanguage.Select(new SettingsService().Load().Language);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("Unhandled AppDomain exception", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            var decision = _uiFaultDialogs.Register(DateTimeOffset.UtcNow);
            if (!decision.ShouldReport)
            {
                if (decision.SuppressedSinceLastReport == 1 || decision.SuppressedSinceLastReport % 20 == 0)
                    AppLogger.Error($"Suppressed repeated UI error dialog ({decision.SuppressedSinceLastReport} in current cooldown)", args.Exception);
                args.Handled = true;
                return;
            }

            if (decision.SuppressedSinceLastReport > 0)
                AppLogger.Info($"Suppressed {decision.SuppressedSinceLastReport} repeated UI error dialog(s) during the previous cooldown.");

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

    public void RecordRuntimeHeartbeat() => _runtimeSession?.Heartbeat();

    protected override void OnExit(ExitEventArgs e)
    {
        _runtimeSession?.EndSession();
        AppLogger.Info("NRS Workbench clean shutdown");
        base.OnExit(e);
    }
}
