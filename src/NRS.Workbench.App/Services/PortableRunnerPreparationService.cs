using System.ComponentModel;
using System.Diagnostics;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class PortableRunnerCandidate : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _resultText = string.Empty;
    private bool _isWorking;

    public required string FolderPath { get; init; }
    public required string DisplayName { get; init; }
    public required string GitHubUrl { get; init; }
    public required string StateLabel { get; init; }
    public required PortableRunnerRegistration Registration { get; init; }
    public bool CanPrepare { get; init; }
    public bool IsPreparedForCurrentMachine { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public string ResultText
    {
        get => _resultText;
        set
        {
            if (_resultText == value) return;
            _resultText = value;
            OnPropertyChanged(nameof(ResultText));
        }
    }

    public bool IsWorking
    {
        get => _isWorking;
        set
        {
            if (_isWorking == value) return;
            _isWorking = value;
            OnPropertyChanged(nameof(IsWorking));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record PortableRunnerPreparationResult(bool Success, string Message);

public sealed record PortableRunnerPreflight(
    PortableRunnerRegistration Registration,
    GitHubRunnerRemoteMetadata RemoteMetadata);

public sealed class PortableRunnerPreparationService
{
    private readonly SettingsService _settings;
    private readonly RunnerProcessService _processes;
    private readonly PortableWorkspaceManifestStore _manifest;
    private readonly GitHubRunnerRegistrationClient _github;

    public PortableRunnerPreparationService(
        SettingsService settings,
        GitHubRunnerRegistrationClient? github = null,
        RunnerProcessService? processes = null)
    {
        if (!AppDataPaths.IsPortable)
            throw new InvalidOperationException("Runner preparation requires an active portable workspace.");

        _settings = settings;
        _github = github ?? new GitHubRunnerRegistrationClient();
        _processes = processes ?? new RunnerProcessService();
        _manifest = new PortableWorkspaceManifestStore(
            AppDataPaths.SettingsDirectory,
            AppDataPaths.PortableVolumeRoot);
    }

    public Task<IReadOnlyList<PortableRunnerCandidate>> DiscoverAsync(
        RunnerSettings settings,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Discover(settings, cancellationToken), cancellationToken);

    public async Task<PortableRunnerPreparationResult> PrepareAsync(
        PortableRunnerCandidate candidate,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        var preflight = (await PreflightAsync([candidate], personalAccessToken, cancellationToken))
            [candidate.FolderPath];
        return await PrepareAsync(candidate, preflight, personalAccessToken, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, PortableRunnerPreflight>> PreflightAsync(
        IReadOnlyList<PortableRunnerCandidate> candidates,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (string.IsNullOrWhiteSpace(personalAccessToken))
            throw new ArgumentException("GitHub token is required.", nameof(personalAccessToken));

        ValidateSelection(candidates);
        ValidateSmartQueueDisabled(_settings.Load());

        var result = new Dictionary<string, PortableRunnerPreflight>(StringComparer.OrdinalIgnoreCase);
        var registrationPermissionChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureStoppedInteractive(candidate.FolderPath);
                EnsureRunnerBinary(candidate.FolderPath);
                if (!IsSafeWorkFolder(candidate.FolderPath, candidate.Registration.WorkFolder))
                    throw new InvalidOperationException("The runner workFolder is not a safe relative path inside the runner folder.");

                var pending = _manifest.FindPending(candidate.FolderPath);
                var localRegistration = PortableRunnerMetadataReader.Read(candidate.FolderPath);
                var registration = localRegistration
                                   ?? pending?.Registration
                                   ?? candidate.Registration;

                GitHubRunnerRemoteMetadata remote;
                if (pending is not null && localRegistration is null)
                {
                    // A previous migration may already have replaced the remote
                    // runner and changed its numeric ID. Resolve by stable target
                    // + runner name so retries still verify the current remote state.
                    remote = await _github.GetRunnerByNameAsync(
                        registration.GitHubUrl,
                        registration.AgentName,
                        personalAccessToken,
                        cancellationToken);
                }
                else
                {
                    remote = await _github.GetRunnerAsync(
                        registration.GitHubUrl,
                        registration.AgentId,
                        personalAccessToken,
                        cancellationToken);
                }

                ValidateRemoteIdentity(registration, remote);
                ValidateRemoteAvailability(remote);

                var targetKey = registration.GitHubUrl.TrimEnd('/');
                if (registrationPermissionChecked.Add(targetKey))
                {
                    // Creating one short-lived token proves write permission for
                    // this GitHub target. The token is deliberately discarded:
                    // each runner gets a fresh token immediately before mutation.
                    var permissionToken = await _github.CreateRegistrationTokenAsync(
                        registration.GitHubUrl,
                        personalAccessToken,
                        cancellationToken);
                    ValidateRegistrationTokenFreshness(permissionToken);
                }

                result[candidate.FolderPath] = new PortableRunnerPreflight(
                    registration,
                    remote);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Preflight failed for '{candidate.DisplayName}': {ex.Message}",
                    ex);
            }
        }

        return result;
    }

    public async Task<PortableRunnerPreparationResult> PrepareAsync(
        PortableRunnerCandidate candidate,
        PortableRunnerPreflight preflight,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(preflight);
        if (!candidate.CanPrepare)
            return new PortableRunnerPreparationResult(false, "Runner is not eligible for portable preparation.");

        ValidateSmartQueueDisabled(_settings.Load());
        EnsureStoppedInteractive(candidate.FolderPath);
        EnsureRunnerBinary(candidate.FolderPath);

        if (string.IsNullOrWhiteSpace(personalAccessToken))
            throw new ArgumentException("GitHub token is required.", nameof(personalAccessToken));

        var registration = preflight.Registration;

        // Revalidate immediately before any local registration is removed. A
        // runner can become online/busy after the batch preflight while earlier
        // runners are being prepared.
        var remote = await _github.GetRunnerByNameAsync(
            registration.GitHubUrl,
            registration.AgentName,
            personalAccessToken,
            cancellationToken);
        ValidateRemoteIdentity(registration, remote);
        ValidateRemoteAvailability(remote);

        // Batch preflight already verified that registration-token creation is
        // permitted. Request a fresh short-lived token here so large batches do
        // not depend on tokens minted several runners/minutes earlier.
        var registrationToken = await _github.CreateRegistrationTokenAsync(
            registration.GitHubUrl,
            personalAccessToken,
            cancellationToken);
        ValidateRegistrationTokenFreshness(registrationToken);

        _manifest.RecordPending(
            candidate.FolderPath,
            registration,
            remote.CustomLabels,
            remote.HasDefaultLabels);

        try
        {
            if (File.Exists(Path.Combine(candidate.FolderPath, ".runner")))
            {
                await RunListenerAsync(
                    candidate.FolderPath,
                    ["remove", "--local"],
                    environment: null,
                    secretValues: [],
                    cancellationToken);
            }

            var target = GitHubRunnerRegistrationClient.ParseTarget(registration.GitHubUrl);
            var arguments = new List<string>
            {
                "configure",
                "--unattended",
                "--url", registration.GitHubUrl,
                "--name", registration.AgentName,
                "--work", registration.WorkFolder,
                "--replace"
            };

            if (!target.IsRepository &&
                !string.IsNullOrWhiteSpace(registration.PoolName) &&
                !string.Equals(registration.PoolName, "Default", StringComparison.OrdinalIgnoreCase))
            {
                arguments.Add("--runnergroup");
                arguments.Add(registration.PoolName);
            }

            if (remote.CustomLabels.Count > 0)
            {
                arguments.Add("--labels");
                arguments.Add(string.Join(",", remote.CustomLabels));
            }

            if (!remote.HasDefaultLabels)
                arguments.Add("--no-default-labels");
            if (registration.DisableUpdate)
                arguments.Add("--disableupdate");
            if (registration.Ephemeral)
                arguments.Add("--ephemeral");

            await RunListenerAsync(
                candidate.FolderPath,
                arguments,
                new Dictionary<string, string>
                {
                    ["ACTIONS_RUNNER_INPUT_TOKEN"] = registrationToken.Token
                },
                [registrationToken.Token],
                cancellationToken);

            var verified = PortableRunnerMetadataReader.Read(candidate.FolderPath)
                           ?? throw new InvalidOperationException("Runner configuration completed but .runner could not be verified.");

            _manifest.RecordPrepared(
                candidate.FolderPath,
                verified.GitHubUrl,
                verified.AgentName);

            return new PortableRunnerPreparationResult(
                true,
                UiLanguage.Choose("Preparado para este PC", "Prepared for this PC", "Preparat per a aquest PC"));
        }
        catch
        {
            // Pending metadata deliberately remains on disk so a fresh registration
            // token can be requested and the operation retried without credentials.
            throw;
        }
    }

    private IReadOnlyList<PortableRunnerCandidate> Discover(
        RunnerSettings settings,
        CancellationToken cancellationToken)
    {
        var folders = new HashSet<string>(
            _settings.DetectRunnerFolders(settings.RunnerRoots),
            StringComparer.OrdinalIgnoreCase);

        foreach (var pending in _manifest.GetPending())
            folders.Add(pending.FolderPath);

        var currentFingerprint = MachineIdentityService.GetFingerprint();
        var candidates = new List<PortableRunnerCandidate>();

        foreach (var folder in folders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PortablePathCodec.IsPortableToken(
                    PortablePathCodec.Encode(folder, AppDataPaths.PortableVolumeRoot)))
                continue;

            var pending = _manifest.FindPending(folder);
            var registration = PortableRunnerMetadataReader.Read(folder) ?? pending?.Registration;
            if (registration is null) continue;

            var serviceMode = File.Exists(Path.Combine(folder, ".service"));
            var running = IsRunning(folder);
            var runnerBinaryAvailable = File.Exists(Path.Combine(folder, "bin", "Runner.Listener.exe"));
            var supportedTarget = IsSupportedTarget(registration.GitHubUrl);
            var safeWorkFolder = IsSafeWorkFolder(folder, registration.WorkFolder);
            var credentialsPresent = HasExpectedLocalCredentials(folder);
            var prepared = _manifest.FindPrepared(folder);
            var preparedHere = prepared is not null &&
                               string.Equals(
                                   prepared.MachineFingerprint,
                                   currentFingerprint,
                                   StringComparison.Ordinal) &&
                               credentialsPresent;

            var canPrepare = !serviceMode && !running && !preparedHere && runnerBinaryAvailable && supportedTarget && safeWorkFolder;
            var state = serviceMode
                ? UiLanguage.Choose("Servicio · no portable", "Service · not portable", "Servei · no portable")
                : running
                    ? UiLanguage.Choose("En ejecución · detener primero", "Running · stop first", "En execució · atura primer")
                    : !runnerBinaryAvailable
                        ? UiLanguage.Choose("Instalación incompleta · falta Runner.Listener.exe", "Incomplete installation · Runner.Listener.exe missing", "Instal·lació incompleta · falta Runner.Listener.exe")
                        : !supportedTarget
                            ? UiLanguage.Choose("Destino no compatible · solo github.com", "Unsupported target · github.com only", "Destí no compatible · només github.com")
                            : !safeWorkFolder
                                ? UiLanguage.Choose("workFolder no portable · revisa la configuración", "Non-portable workFolder · review configuration", "workFolder no portable · revisa la configuració")
                                : pending is not null
                                    ? UiLanguage.Choose("Migración pendiente · reintentar", "Migration pending · retry", "Migració pendent · torna-ho a provar")
                                    : preparedHere
                                        ? UiLanguage.Choose("Listo en este PC", "Ready on this PC", "Llest en aquest PC")
                                        : prepared is not null && !credentialsPresent
                                            ? UiLanguage.Choose("Credenciales locales incompletas · preparar de nuevo", "Local credentials incomplete · prepare again", "Credencials locals incompletes · prepara de nou")
                                            : UiLanguage.Choose("Necesita preparación", "Needs preparation", "Necessita preparació");

            candidates.Add(new PortableRunnerCandidate
            {
                FolderPath = folder,
                DisplayName = registration.AgentName,
                GitHubUrl = registration.GitHubUrl,
                Registration = registration,
                StateLabel = state,
                CanPrepare = canPrepare,
                IsPreparedForCurrentMachine = preparedHere,
                IsSelected = canPrepare
            });
        }

        return candidates;
    }

    private bool IsRunning(string folder)
    {
        var snapshot = _processes.GetSnapshot(folder);
        try { return snapshot.HasListener || snapshot.HasWorker; }
        finally
        {
            snapshot.Listener?.Dispose();
            foreach (var worker in snapshot.Workers) worker.Dispose();
        }
    }

    public static void ValidateSelection(IReadOnlyList<PortableRunnerCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
            throw new InvalidOperationException("No runners were selected for portable preparation.");

        var duplicates = candidates
            .GroupBy(
                candidate => candidate.Registration.GitHubUrl.TrimEnd('/') + "\u001f" + candidate.Registration.AgentName,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First().Registration.AgentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                "Multiple selected runner folders use the same GitHub target and runner name: " +
                string.Join(", ", duplicates) +
                ". Resolve the duplicate before bulk preparation.");

        var ineligible = candidates.FirstOrDefault(candidate => !candidate.CanPrepare);
        if (ineligible is not null)
            throw new InvalidOperationException(
                $"Runner '{ineligible.DisplayName}' is not eligible for portable preparation.");
    }

    private static bool IsSupportedTarget(string gitHubUrl)
    {
        try
        {
            GitHubRunnerRegistrationClient.ParseTarget(gitHubUrl);
            return true;
        }
        catch { return false; }
    }

    public static void ValidateRemoteIdentity(
        PortableRunnerRegistration registration,
        GitHubRunnerRemoteMetadata remote)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(remote);

        if (string.IsNullOrWhiteSpace(remote.Name) ||
            !string.Equals(remote.Name.Trim(), registration.AgentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GitHub runner identity mismatch. Local runner '{registration.AgentName}' does not match remote runner '{remote.Name}'.");
        }
    }

    public static void ValidateSmartQueueDisabled(RunnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.RunnerQueueEnabled)
            throw new InvalidOperationException(
                "Disable Smart Queue before portable runner preparation so it cannot start a runner while its local registration is being replaced.");
    }

    public static bool HasExpectedLocalCredentials(string runnerFolder)
    {
        if (string.IsNullOrWhiteSpace(runnerFolder)) return false;
        return File.Exists(Path.Combine(runnerFolder, ".credentials")) &&
               File.Exists(Path.Combine(runnerFolder, ".credentials_rsaparams"));
    }

    public static void ValidateRemoteAvailability(GitHubRunnerRemoteMetadata remote)
    {
        ArgumentNullException.ThrowIfNull(remote);

        if (remote.IsBusy)
            throw new InvalidOperationException(
                "The GitHub runner is still busy on another host. Wait for its job to finish before preparing this copy.");

        if (string.Equals(remote.Status?.Trim(), "online", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The GitHub runner is still online. Stop the old runner instance or wait for GitHub to report it offline before preparing this copy.");
    }

    public static void ValidateRegistrationTokenFreshness(
        GitHubRunnerRegistrationToken registrationToken,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(registrationToken);
        if (string.IsNullOrWhiteSpace(registrationToken.Token))
            throw new InvalidOperationException("The GitHub runner registration token is empty.");

        if (registrationToken.ExpiresAt is null) return;

        var reference = now ?? DateTimeOffset.UtcNow;
        if (registrationToken.ExpiresAt <= reference.AddMinutes(1))
            throw new InvalidOperationException(
                "The GitHub runner registration token is expired or too close to expiry. Run preflight again before changing local runner configuration.");
    }

    public static bool IsSafeWorkFolder(string runnerFolder, string workFolder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(runnerFolder) || string.IsNullOrWhiteSpace(workFolder))
                return false;
            if (Path.IsPathFullyQualified(workFolder)) return false;

            var root = Path.GetFullPath(runnerFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(root, workFolder));
            var relative = Path.GetRelativePath(root, resolved);

            return relative != ".." &&
                   !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                   !Path.IsPathRooted(relative) &&
                   !ContainsReparsePoint(root, resolved);
        }
        catch { return false; }
    }

    private static void EnsureRunnerBinary(string folder)
    {
        var root = Path.GetFullPath(folder);
        var executable = Path.Combine(root, "bin", "Runner.Listener.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("Runner.Listener.exe was not found.", executable);
        if (ContainsReparsePoint(root, executable))
            throw new InvalidOperationException(
                "Portable preparation refuses runner folders or binaries that traverse a reparse point.");
    }

    private void EnsureStoppedInteractive(string folder)
    {
        var full = Path.GetFullPath(folder);
        if (ContainsReparsePoint(full, full))
            throw new InvalidOperationException(
                "Portable preparation refuses a runner folder that is a reparse point.");
        if (File.Exists(Path.Combine(folder, ".service")))
            throw new InvalidOperationException("Portable preparation currently supports interactive runners only.");
        if (IsRunning(folder))
            throw new InvalidOperationException("Stop the runner before preparing it for another PC.");
    }

    private static bool ContainsReparsePoint(string root, string target)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedTarget = Path.GetFullPath(target);
            var relative = Path.GetRelativePath(normalizedRoot, normalizedTarget);

            var current = normalizedRoot;
            if (IsExistingReparsePoint(current)) return true;
            if (relative == ".") return false;

            foreach (var part in relative.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (IsExistingReparsePoint(current)) return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsExistingReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static async Task RunListenerAsync(
        string runnerFolder,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        IReadOnlyList<string> secretValues,
        CancellationToken cancellationToken)
    {
        var executable = Path.Combine(runnerFolder, "bin", "Runner.Listener.exe");

        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = runnerFolder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var item in environment) start.Environment[item.Key] = item.Value;

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the GitHub Actions runner configuration process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode == 0) return;

        var detail = string.Join(
            Environment.NewLine,
            new[] { stdout, stderr }.Where(x => !string.IsNullOrWhiteSpace(x)));
        foreach (var secret in secretValues.Where(x => !string.IsNullOrWhiteSpace(x)))
            detail = detail.Replace(secret, "***", StringComparison.Ordinal);
        detail = SensitiveDataRedactor.Redact(detail);
        if (detail.Length > 3000) detail = detail[..3000] + "…";

        throw new InvalidOperationException(
            $"Runner configuration failed with exit code {process.ExitCode}." +
            (string.IsNullOrWhiteSpace(detail) ? string.Empty : Environment.NewLine + detail));
    }
}
