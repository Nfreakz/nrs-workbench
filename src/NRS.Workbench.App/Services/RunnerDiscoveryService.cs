using System.Diagnostics;
using System.Text.Json;
using NRS.Workbench.Core.Interfaces;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class RunnerDiscoveryService : IRunnerDiscoveryService
{
    private readonly SettingsService _settingsService;
    private readonly RunnerProcessService _processService;
    private readonly WindowsServiceController _serviceController;
    private readonly IRunnerProgressService _progressService;

    public RunnerDiscoveryService(SettingsService settingsService, RunnerProcessService processService, WindowsServiceController serviceController, IRunnerProgressService progressService)
    {
        _settingsService = settingsService;
        _processService = processService;
        _serviceController = serviceController;
        _progressService = progressService;
    }

    public IReadOnlyList<RunnerInfo> Discover()
    {
        var settings = _settingsService.Load();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in settings.RunnerRoots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var folder in Directory.EnumerateDirectories(root, settings.FolderPattern, SearchOption.TopDirectoryOnly))
                {
                    if (File.Exists(Path.Combine(folder, ".runner")) || File.Exists(Path.Combine(folder, "run.cmd")))
                        folders.Add(Path.GetFullPath(folder));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Could not scan runner root '{root}'", ex);
            }
        }

        return folders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(BuildRunner).ToList();
    }

    private RunnerInfo BuildRunner(string folder)
    {
        try
        {
            var metadata = ReadMetadata(folder);
            var snapshot = _processService.GetSnapshot(folder);
            var serviceName = ReadTextFile(Path.Combine(folder, ".service"));
            var mode = string.IsNullOrWhiteSpace(serviceName) ? RunnerMode.Interactive : RunnerMode.Service;
            var state = RunnerState.Unknown;
            var detail = string.Empty;

            if (!metadata.Configured)
            {
                state = RunnerState.Unregistered;
                mode = RunnerMode.Unknown;
            }
            else if (mode == RunnerMode.Service)
            {
                if (!_serviceController.Exists(serviceName))
                {
                    state = RunnerState.Error;
                    detail = "La carpeta indica un servicio que Windows no encuentra.";
                }
                else
                {
                    var native = _serviceController.GetState(serviceName);
                    state = native switch
                    {
                        NativeServiceState.Running when snapshot.HasWorker => RunnerState.Busy,
                        NativeServiceState.Running when snapshot.HasListener => RunnerState.Ready,
                        NativeServiceState.Running => RunnerState.Starting,
                        NativeServiceState.StartPending => RunnerState.Starting,
                        NativeServiceState.StopPending => RunnerState.Stopping,
                        NativeServiceState.Stopped => RunnerState.Stopped,
                        _ => RunnerState.Unknown
                    };
                }
            }
            else
            {
                state = snapshot.HasWorker ? RunnerState.Busy :
                        snapshot.HasListener ? RunnerState.Ready :
                        RunnerState.Stopped;
            }

            var listenerPid = snapshot.Listener?.Id;
            var workerPids = snapshot.Workers.Select(p => p.Id).ToArray();
            var progress = _progressService.GetProgress(folder, state);
            DisposeSnapshot(snapshot);

            return new RunnerInfo
            {
                Alias = GetAlias(folder),
                FolderPath = folder,
                AgentName = metadata.AgentName,
                GitHubTarget = metadata.GitHubTarget,
                GitHubUrl = metadata.GitHubUrl,
                Labels = metadata.Labels,
                Version = GetVersion(folder),
                ServiceName = serviceName,
                Mode = mode,
                State = state,
                ListenerPid = listenerPid,
                WorkerPids = workerPids,
                RamBytes = snapshot.RamBytes,
                StartedAt = snapshot.StartedAt,
                StatusDetail = detail,
                Progress = progress
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not inspect runner '{folder}'", ex);
            return new RunnerInfo
            {
                Alias = GetAlias(folder),
                FolderPath = folder,
                State = RunnerState.Error,
                StatusDetail = ex.Message
            };
        }
    }

    private static void DisposeSnapshot(RunnerProcessSnapshot snapshot)
    {
        snapshot.Listener?.Dispose();
        foreach (var worker in snapshot.Workers) worker.Dispose();
    }

    private static string GetAlias(string folder)
    {
        var name = new DirectoryInfo(folder).Name;
        if (name.Equals("actions-runner", StringComparison.OrdinalIgnoreCase)) return "main";
        const string prefix = "actions-runner-";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
    }

    private static string GetVersion(string folder)
    {
        try
        {
            var exe = Path.Combine(folder, "bin", "Runner.Listener.exe");
            if (!File.Exists(exe)) return string.Empty;
            var raw = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? string.Empty;
            var plus = raw.IndexOf('+');
            return plus > 0 ? raw[..plus] : raw;
        }
        catch { return string.Empty; }
    }

    private static Metadata ReadMetadata(string folder)
    {
        var path = Path.Combine(folder, ".runner");
        if (!File.Exists(path)) return new Metadata(false, string.Empty, string.Empty, string.Empty, string.Empty);

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var root = json.RootElement;
            var agent = GetString(root, "agentName");
            var url = GetString(root, "gitHubUrl");
            if (string.IsNullOrWhiteSpace(url))
            {
                var serverUrl = GetString(root, "serverUrl");
                if (serverUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) url = serverUrl;
            }
            var labels = string.Empty;
            if (root.TryGetProperty("labels", out var labelsElement) && labelsElement.ValueKind == JsonValueKind.Array)
                labels = string.Join(", ", labelsElement.EnumerateArray().Select(x => x.ToString()));
            return new Metadata(true, agent, GetGitHubTarget(url), url, labels);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not parse {path}", ex);
            return new Metadata(true, string.Empty, string.Empty, string.Empty, string.Empty);
        }
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetGitHubTarget(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return string.Empty;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return uri.Host;
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}/{parts[1]}" : parts.FirstOrDefault() ?? string.Empty;
    }

    private static string ReadTextFile(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty; }
        catch { return string.Empty; }
    }

    private sealed record Metadata(bool Configured, string AgentName, string GitHubTarget, string GitHubUrl, string Labels);
}
