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
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.CanPrepare)
            return new PortableRunnerPreparationResult(false, "Runner is not eligible for portable preparation.");
        if (string.IsNullOrWhiteSpace(personalAccessToken))
            return new PortableRunnerPreparationResult(false, "GitHub token is required.");

        EnsureStoppedInteractive(candidate.FolderPath);

        var pending = _manifest.FindPending(candidate.FolderPath);
        var localRegistration = PortableRunnerMetadataReader.Read(candidate.FolderPath);
        var registration = localRegistration
                           ?? pending?.Registration
                           ?? candidate.Registration;

        GitHubRunnerRemoteMetadata remote;
        if (pending is not null && localRegistration is null)
        {
            // A previous attempt already removed local machine-bound credentials.
            // Reuse the non-secret remote metadata captured before that removal so
            // retry does not depend on the old runner ID still existing remotely.
            remote = new GitHubRunnerRemoteMetadata(
                pending.Registration.AgentName,
                pending.CustomLabels,
                pending.HasDefaultLabels);
        }
        else
        {
            remote = await _github.GetRunnerAsync(
                registration.GitHubUrl,
                registration.AgentId,
                personalAccessToken,
                cancellationToken);
        }

        var registrationToken = await _github.CreateRegistrationTokenAsync(
            registration.GitHubUrl,
            personalAccessToken,
            cancellationToken);

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
            var prepared = _manifest.FindPrepared(folder);
            var preparedHere = prepared is not null &&
                               string.Equals(
                                   prepared.MachineFingerprint,
                                   currentFingerprint,
                                   StringComparison.Ordinal);

            var canPrepare = !serviceMode && !running && !preparedHere;
            var state = serviceMode
                ? UiLanguage.Choose("Servicio · no portable", "Service · not portable", "Servei · no portable")
                : running
                    ? UiLanguage.Choose("En ejecución · detener primero", "Running · stop first", "En execució · atura primer")
                    : preparedHere
                        ? UiLanguage.Choose("Listo en este PC", "Ready on this PC", "Llest en aquest PC")
                        : pending is not null
                            ? UiLanguage.Choose("Migración pendiente · reintentar", "Migration pending · retry", "Migració pendent · torna-ho a provar")
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

    private void EnsureStoppedInteractive(string folder)
    {
        if (File.Exists(Path.Combine(folder, ".service")))
            throw new InvalidOperationException("Portable preparation currently supports interactive runners only.");
        if (IsRunning(folder))
            throw new InvalidOperationException("Stop the runner before preparing it for another PC.");
    }

    private static async Task RunListenerAsync(
        string runnerFolder,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment,
        IReadOnlyList<string> secretValues,
        CancellationToken cancellationToken)
    {
        var executable = Path.Combine(runnerFolder, "bin", "Runner.Listener.exe");
        if (!File.Exists(executable))
            throw new FileNotFoundException("Runner.Listener.exe was not found.", executable);

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
