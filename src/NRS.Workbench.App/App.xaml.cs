using System.Security.Cryptography;
using System.Text;
using System.Windows;
using NRS.Workbench.App.Services;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App;

public partial class App : Application
{
    private readonly FaultBurstThrottle _uiFaultDialogs = new(TimeSpan.FromMinutes(1));
    private RuntimeSessionTracker? _runtimeSession;
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Initialize();
        if (!TryAcquireSingleInstance())
        {
            AppLogger.Info("Another NRS Workbench instance is already running for this data directory; exiting.");
            UiLanguage.Select(new SettingsService().Load().Language);
            MessageBox.Show(
                UiLanguage.Choose(
                    "NRS Workbench ya se está ejecutando. Usa la ventana o el icono de la bandeja de la instancia abierta.",
                    "NRS Workbench is already running. Use the window or tray icon of the open instance.",
                    "NRS Workbench ja s'està executant. Fes servir la finestra o la icona de la safata de la instància oberta."),
                "NRS Workbench",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

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
        DisplayText.Choose = UiLanguage.Choose;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("Unhandled AppDomain exception", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            // Always log the exception; only the dialog is throttled.
            AppLogger.Error("Unhandled UI exception", args.Exception);

            var decision = _uiFaultDialogs.Register(DateTimeOffset.UtcNow);
            if (!decision.ShouldReport)
            {
                args.Handled = true;
                return;
            }

            if (decision.SuppressedSinceLastReport > 0)
                AppLogger.Info($"Suppressed {decision.SuppressedSinceLastReport} repeated UI error dialog(s) during the previous cooldown.");

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

    /// <summary>
    /// Applies a new UI language without restarting: {local:Tr} is resolved once per
    /// window, so the main window is rebuilt and the old one (with its tray icon) closed.
    /// The single-instance mutex and runtime session are untouched.
    /// </summary>
    public void ReloadUiLanguage(MainWindow current)
    {
        var previousMode = ShutdownMode;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var next = new MainWindow(current);
            var bounds = current.WindowState == WindowState.Normal
                ? new Rect(current.Left, current.Top, current.Width, current.Height)
                : current.RestoreBounds;
            if (!bounds.IsEmpty)
            {
                next.WindowStartupLocation = WindowStartupLocation.Manual;
                next.Left = bounds.Left;
                next.Top = bounds.Top;
                next.Width = bounds.Width;
                next.Height = bounds.Height;
            }
            if (current.WindowState == WindowState.Maximized) next.WindowState = WindowState.Maximized;
            MainWindow = next;
            // Settings is opened from the visible main window, so the new one is shown too.
            next.Show();
            current.CloseForReload();
            next.Activate();
            AppLogger.Info($"UI language switched to {UiLanguage.Current} without restart.");
        }
        finally
        {
            ShutdownMode = previousMode;
        }
    }

    public void RecordRuntimeHeartbeat() => _runtimeSession?.Heartbeat();

    // One mutex per data directory, so PREVIEW and portable copies do not collide with the normal profile.
    private bool TryAcquireSingleInstance()
    {
        try
        {
            var directory = Path.GetFullPath(AppDataPaths.SettingsDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory)))[..32];
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\NRSWorkbench-" + hash, out var createdNew);
            if (createdNew) return true;
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return false;
        }
        catch (Exception ex)
        {
            // Never block startup because of the guard itself.
            AppLogger.Error("Single-instance check failed; continuing without it", ex);
            return true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _runtimeSession?.EndSession();
        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        if (_runtimeSession is not null) AppLogger.Info("NRS Workbench clean shutdown");
        base.OnExit(e);
    }
}
