using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public enum RunnerDoctorSeverity
{
    Healthy = 0,
    Attention = 1,
    Problem = 2
}

public sealed record RunnerDoctorCheck(
    string Title,
    string Detail,
    RunnerDoctorSeverity Severity)
{
    public string SeverityLabel => Severity switch
    {
        RunnerDoctorSeverity.Problem => UiLanguage.Choose("Problema", "Problem", "Problema"),
        RunnerDoctorSeverity.Attention => UiLanguage.Choose("Atención", "Attention", "Atenció"),
        _ => "OK"
    };
}

public sealed record RunnerDoctorReport(
    RunnerInfo Runner,
    IReadOnlyList<RunnerDoctorCheck> Checks,
    DateTimeOffset? LastWorkerLogAt)
{
    public RunnerDoctorSeverity Severity => Checks.Count == 0
        ? RunnerDoctorSeverity.Attention
        : Checks.Max(x => x.Severity);

    public string HealthLabel => Severity switch
    {
        RunnerDoctorSeverity.Problem => UiLanguage.Choose("PROBLEMA", "PROBLEM", "PROBLEMA"),
        RunnerDoctorSeverity.Attention => UiLanguage.Choose("ATENCIÓN", "ATTENTION", "ATENCIÓ"),
        _ => "OK"
    };

    public string StateLabel => Runner.State.ToString().ToUpperInvariant();

    public string ModeLabel => Runner.Mode switch
    {
        RunnerMode.Service => UiLanguage.Choose("Servicio", "Service", "Servei"),
        RunnerMode.Interactive => UiLanguage.Choose("Interactivo", "Interactive", "Interactiu"),
        _ => UiLanguage.Choose("Desconocido", "Unknown", "Desconegut")
    };

    public string VersionLabel => string.IsNullOrWhiteSpace(Runner.Version)
        ? UiLanguage.Choose("Desconocida", "Unknown", "Desconeguda")
        : Runner.Version;

    public string LastActivityLabel => LastWorkerLogAt is null
        ? UiLanguage.Choose("Sin historial", "No history", "Sense historial")
        : FormatAge(DateTimeOffset.Now - LastWorkerLogAt.Value);

    public string SummaryLabel
    {
        get
        {
            var problems = Checks.Count(x => x.Severity == RunnerDoctorSeverity.Problem);
            var attention = Checks.Count(x => x.Severity == RunnerDoctorSeverity.Attention);
            if (problems > 0)
                return UiLanguage.Choose(
                    $"{problems} problema{(problems == 1 ? "" : "s")} · {attention} aviso{(attention == 1 ? "" : "s")}",
                    $"{problems} problem{(problems == 1 ? "" : "s")} · {attention} warning{(attention == 1 ? "" : "s")}",
                    $"{problems} problema{(problems == 1 ? "" : "s")} · {attention} avís{(attention == 1 ? "" : "os")}");
            if (attention > 0)
                return UiLanguage.Choose(
                    $"{attention} punto{(attention == 1 ? "" : "s")} a revisar",
                    $"{attention} item{(attention == 1 ? "" : "s")} to review",
                    $"{attention} punt{(attention == 1 ? "" : "s")} a revisar");
            return UiLanguage.Choose("Todo correcto", "All checks passed", "Tot correcte");
        }
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age.TotalMinutes < 1) return UiLanguage.Choose("Ahora", "Now", "Ara");
        if (age.TotalHours < 1) return UiLanguage.Choose($"Hace {(int)age.TotalMinutes} min", $"{(int)age.TotalMinutes} min ago", $"Fa {(int)age.TotalMinutes} min");
        if (age.TotalDays < 1) return UiLanguage.Choose($"Hace {(int)age.TotalHours} h", $"{(int)age.TotalHours} h ago", $"Fa {(int)age.TotalHours} h");
        return UiLanguage.Choose($"Hace {(int)age.TotalDays} d", $"{(int)age.TotalDays} d ago", $"Fa {(int)age.TotalDays} d");
    }
}

public sealed class RunnerDoctorService
{
    private static readonly TimeSpan BusyLogStaleThreshold = TimeSpan.FromMinutes(30);
    private readonly WindowsServiceController _serviceController;

    public RunnerDoctorService(WindowsServiceController? serviceController = null)
    {
        _serviceController = serviceController ?? new WindowsServiceController();
    }

    public IReadOnlyList<RunnerDoctorReport> Analyze(IReadOnlyList<RunnerInfo> runners) =>
        runners
            .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(AnalyzeRunner)
            .ToList();

    public RunnerDoctorReport AnalyzeRunner(RunnerInfo runner)
    {
        var checks = new List<RunnerDoctorCheck>();
        var folderExists = Directory.Exists(runner.FolderPath);

        checks.Add(folderExists
            ? Ok(UiLanguage.Choose("Carpeta del runner", "Runner folder", "Carpeta del runner"),
                UiLanguage.Choose("La carpeta configurada existe y es accesible.", "The configured folder exists and is accessible.", "La carpeta configurada existeix i és accessible."))
            : Problem(UiLanguage.Choose("Carpeta del runner", "Runner folder", "Carpeta del runner"),
                UiLanguage.Choose("La carpeta configurada no existe o ya no está disponible.", "The configured folder does not exist or is no longer available.", "La carpeta configurada no existeix o ja no està disponible.")));

        var runCmd = Path.Combine(runner.FolderPath, "run.cmd");
        var listenerExe = Path.Combine(runner.FolderPath, "bin", "Runner.Listener.exe");
        var installMarkersOk = folderExists && File.Exists(runCmd) && File.Exists(listenerExe);
        checks.Add(installMarkersOk
            ? Ok(UiLanguage.Choose("Instalación", "Installation", "Instal·lació"),
                UiLanguage.Choose("run.cmd y Runner.Listener.exe están presentes.", "run.cmd and Runner.Listener.exe are present.", "run.cmd i Runner.Listener.exe són presents."))
            : Problem(UiLanguage.Choose("Instalación", "Installation", "Instal·lació"),
                UiLanguage.Choose("Faltan archivos esenciales de la instalación del runner.", "Essential runner installation files are missing.", "Falten fitxers essencials de la instal·lació del runner.")));

        var registered = folderExists && File.Exists(Path.Combine(runner.FolderPath, ".runner"));
        checks.Add(registered
            ? Ok(UiLanguage.Choose("Registro local", "Local registration", "Registre local"),
                UiLanguage.Choose("Se detecta el marcador de registro .runner.", "The .runner registration marker is present.", "Es detecta el marcador de registre .runner."))
            : Problem(UiLanguage.Choose("Registro local", "Local registration", "Registre local"),
                UiLanguage.Choose("No se detecta .runner. El runner puede no estar configurado.", ".runner was not found. The runner may not be configured.", "No es detecta .runner. El runner pot no estar configurat.")));

        AddStateCheck(runner, checks);
        AddProcessChecks(runner, checks);
        AddServiceCheck(runner, checks);

        checks.Add(string.IsNullOrWhiteSpace(runner.Version)
            ? Attention(UiLanguage.Choose("Versión", "Version", "Versió"),
                UiLanguage.Choose("No se ha podido identificar la versión instalada.", "The installed version could not be identified.", "No s'ha pogut identificar la versió instal·lada."))
            : Ok(UiLanguage.Choose("Versión", "Version", "Versió"),
                UiLanguage.Choose($"Runner {runner.Version}.", $"Runner {runner.Version}.", $"Runner {runner.Version}.")));

        var lastWorker = AddDiagnosticsCheck(runner, checks);

        if (!string.IsNullOrWhiteSpace(runner.StatusDetail))
            checks.Add(Problem(UiLanguage.Choose("Detalle del estado", "State detail", "Detall de l'estat"), runner.StatusDetail));

        return new RunnerDoctorReport(runner, checks, lastWorker);
    }

    private static void AddStateCheck(RunnerInfo runner, ICollection<RunnerDoctorCheck> checks)
    {
        switch (runner.State)
        {
            case RunnerState.Error:
                checks.Add(Problem(UiLanguage.Choose("Estado actual", "Current state", "Estat actual"),
                    UiLanguage.Choose("El runner está en ERROR.", "The runner is in ERROR state.", "El runner està en estat ERROR.")));
                break;
            case RunnerState.Unregistered:
                checks.Add(Problem(UiLanguage.Choose("Estado actual", "Current state", "Estat actual"),
                    UiLanguage.Choose("El runner aparece como no registrado.", "The runner appears unregistered.", "El runner apareix com a no registrat.")));
                break;
            case RunnerState.Unknown:
                checks.Add(Attention(UiLanguage.Choose("Estado actual", "Current state", "Estat actual"),
                    UiLanguage.Choose("No se puede clasificar el estado actual.", "The current state cannot be classified.", "No es pot classificar l'estat actual.")));
                break;
            default:
                checks.Add(Ok(UiLanguage.Choose("Estado actual", "Current state", "Estat actual"),
                    UiLanguage.Choose($"Estado {runner.State.ToString().ToUpperInvariant()} coherente.", $"State {runner.State.ToString().ToUpperInvariant()} is recognised.", $"Estat {runner.State.ToString().ToUpperInvariant()} reconegut.")));
                break;
        }
    }

    private static void AddProcessChecks(RunnerInfo runner, ICollection<RunnerDoctorCheck> checks)
    {
        var expectsListener = runner.State is RunnerState.Ready or RunnerState.Busy;
        if (expectsListener && runner.ListenerPid is null)
        {
            checks.Add(Attention(UiLanguage.Choose("Listener", "Listener", "Listener"),
                UiLanguage.Choose("El estado indica que debería haber un listener, pero no se ve su PID.", "The state expects a listener, but no listener PID is visible.", "L'estat indica que hi hauria d'haver un listener, però no se'n veu el PID.")));
        }
        else if (runner.ListenerPid is int pid)
        {
            checks.Add(Ok("Listener", $"PID {pid}"));
        }
        else
        {
            checks.Add(Ok("Listener", UiLanguage.Choose("Sin listener activo, coherente con el estado actual.", "No active listener, consistent with the current state.", "Sense listener actiu, coherent amb l'estat actual.")));
        }

        if (runner.State == RunnerState.Busy && runner.WorkerPids.Count == 0)
        {
            checks.Add(Attention("Worker",
                UiLanguage.Choose("El runner figura BUSY pero no se ha detectado Runner.Worker.exe.", "The runner is BUSY but Runner.Worker.exe was not detected.", "El runner figura BUSY però no s'ha detectat Runner.Worker.exe.")));
        }
        else if (runner.WorkerPids.Count > 0)
        {
            checks.Add(Ok("Worker", UiLanguage.Choose(
                $"{runner.WorkerPids.Count} proceso{(runner.WorkerPids.Count == 1 ? "" : "s")} Worker detectado{(runner.WorkerPids.Count == 1 ? "" : "s")}.",
                $"{runner.WorkerPids.Count} Worker process{(runner.WorkerPids.Count == 1 ? "" : "es")} detected.",
                $"{runner.WorkerPids.Count} procés{(runner.WorkerPids.Count == 1 ? "" : "s")} Worker detectat{(runner.WorkerPids.Count == 1 ? "" : "s")}.")));
        }
        else
        {
            checks.Add(Ok("Worker", UiLanguage.Choose("Sin Worker activo.", "No active Worker process.", "Sense Worker actiu.")));
        }
    }

    private void AddServiceCheck(RunnerInfo runner, ICollection<RunnerDoctorCheck> checks)
    {
        if (runner.Mode != RunnerMode.Service)
        {
            checks.Add(Ok(UiLanguage.Choose("Modo", "Mode", "Mode"),
                UiLanguage.Choose("Runner interactivo. No depende de un servicio de Windows.", "Interactive runner. It does not depend on a Windows service.", "Runner interactiu. No depèn d'un servei de Windows.")));
            return;
        }

        if (string.IsNullOrWhiteSpace(runner.ServiceName))
        {
            checks.Add(Attention(UiLanguage.Choose("Servicio de Windows", "Windows service", "Servei de Windows"),
                UiLanguage.Choose("El runner indica modo servicio, pero no expone el nombre del servicio.", "The runner indicates service mode but exposes no service name.", "El runner indica mode servei però no exposa el nom del servei.")));
            return;
        }

        try
        {
            if (!_serviceController.Exists(runner.ServiceName))
            {
                checks.Add(Problem(UiLanguage.Choose("Servicio de Windows", "Windows service", "Servei de Windows"),
                    UiLanguage.Choose("El servicio configurado no existe en Windows.", "The configured service does not exist in Windows.", "El servei configurat no existeix a Windows.")));
                return;
            }

            var state = _serviceController.GetState(runner.ServiceName);
            checks.Add(Ok(UiLanguage.Choose("Servicio de Windows", "Windows service", "Servei de Windows"),
                UiLanguage.Choose($"Servicio detectado · {state}.", $"Service detected · {state}.", $"Servei detectat · {state}.")));
        }
        catch (Exception ex)
        {
            checks.Add(Attention(UiLanguage.Choose("Servicio de Windows", "Windows service", "Servei de Windows"),
                UiLanguage.Choose($"No se ha podido consultar el servicio: {ex.Message}", $"Could not query the service: {ex.Message}", $"No s'ha pogut consultar el servei: {ex.Message}")));
        }
    }

    private static DateTimeOffset? AddDiagnosticsCheck(RunnerInfo runner, ICollection<RunnerDoctorCheck> checks)
    {
        var diag = Path.Combine(runner.FolderPath, "_diag");
        if (!Directory.Exists(diag))
        {
            checks.Add(Attention(UiLanguage.Choose("Diagnósticos locales", "Local diagnostics", "Diagnòstics locals"),
                UiLanguage.Choose("No existe la carpeta _diag.", "The _diag folder does not exist.", "No existeix la carpeta _diag.")));
            return null;
        }

        try
        {
            var latest = new DirectoryInfo(diag)
                .EnumerateFiles("Worker_*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is null)
            {
                checks.Add(Ok(UiLanguage.Choose("Diagnósticos locales", "Local diagnostics", "Diagnòstics locals"),
                    UiLanguage.Choose("_diag existe; todavía no hay historial Worker.", "_diag exists; there is no Worker history yet.", "_diag existeix; encara no hi ha historial Worker.")));
                return null;
            }

            var lastWrite = new DateTimeOffset(latest.LastWriteTime);
            var age = DateTimeOffset.Now - lastWrite;
            if (runner.State == RunnerState.Busy && age > BusyLogStaleThreshold)
            {
                checks.Add(Attention(UiLanguage.Choose("Actividad del Worker", "Worker activity", "Activitat del Worker"),
                    UiLanguage.Choose("El runner está BUSY, pero el último Worker log lleva más de 30 minutos sin cambiar.", "The runner is BUSY, but the latest Worker log has not changed for more than 30 minutes.", "El runner està BUSY, però l'últim Worker log fa més de 30 minuts que no canvia.")));
            }
            else
            {
                checks.Add(Ok(UiLanguage.Choose("Diagnósticos locales", "Local diagnostics", "Diagnòstics locals"),
                    UiLanguage.Choose($"Último Worker log: {latest.Name}.", $"Latest Worker log: {latest.Name}.", $"Últim Worker log: {latest.Name}.")));
            }

            return lastWrite;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            checks.Add(Attention(UiLanguage.Choose("Diagnósticos locales", "Local diagnostics", "Diagnòstics locals"),
                UiLanguage.Choose($"No se puede revisar _diag: {ex.Message}", $"Could not inspect _diag: {ex.Message}", $"No es pot revisar _diag: {ex.Message}")));
            return null;
        }
    }

    private static RunnerDoctorCheck Ok(string title, string detail) =>
        new(title, detail, RunnerDoctorSeverity.Healthy);

    private static RunnerDoctorCheck Attention(string title, string detail) =>
        new(title, detail, RunnerDoctorSeverity.Attention);

    private static RunnerDoctorCheck Problem(string title, string detail) =>
        new(title, detail, RunnerDoctorSeverity.Problem);
}
