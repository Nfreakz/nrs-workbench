using System.Text.Json;

namespace NRS.Workbench.App.Services;

public sealed record PortableRunnerRegistration(
    ulong AgentId,
    string AgentName,
    string GitHubUrl,
    string WorkFolder,
    string PoolName,
    bool DisableUpdate,
    bool Ephemeral);

public sealed record PortablePreparedRunner(
    string Path,
    string MachineFingerprint,
    DateTimeOffset PreparedAtUtc,
    string GitHubUrl,
    string AgentName);

public sealed record PortablePendingRunner(
    string Path,
    DateTimeOffset StartedAtUtc,
    PortableRunnerRegistration Registration,
    IReadOnlyList<string> CustomLabels,
    bool HasDefaultLabels);

public sealed class PortableWorkspaceManifest
{
    public int SchemaVersion { get; set; } = 1;
    public List<PortablePreparedRunner> PreparedRunners { get; set; } = [];
    public List<PortablePendingRunner> PendingRunners { get; set; } = [];
}

public sealed class PortableWorkspaceManifestStore
{
    private const long MaxManifestBytes = 1024 * 1024;
    private readonly string _filePath;
    private readonly string _portableRoot;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public PortableWorkspaceManifestStore(string dataDirectory, string portableRoot)
    {
        _filePath = Path.Combine(dataDirectory, "portable-workspace.json");
        _portableRoot = Path.GetFullPath(portableRoot);
    }

    public PortableWorkspaceManifest Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new PortableWorkspaceManifest();
            if (new FileInfo(_filePath).Length > MaxManifestBytes)
                throw new InvalidDataException("Portable workspace manifest is unexpectedly large.");

            var manifest = JsonSerializer.Deserialize<PortableWorkspaceManifest>(
                File.ReadAllText(_filePath),
                Options) ?? new PortableWorkspaceManifest();
            if (manifest.SchemaVersion != 1)
                throw new InvalidDataException("Unsupported portable workspace manifest.");
            manifest.PreparedRunners ??= [];
            manifest.PendingRunners ??= [];
            return manifest;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not read portable workspace manifest", ex);
            return new PortableWorkspaceManifest();
        }
    }

    public void RecordPrepared(string runnerFolder, string gitHubUrl, string agentName)
    {
        var manifest = Load();
        var key = EncodePath(runnerFolder);
        manifest.PreparedRunners.RemoveAll(x => string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase));
        manifest.PendingRunners.RemoveAll(x => string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase));
        manifest.PreparedRunners.Add(new PortablePreparedRunner(
            key,
            MachineIdentityService.GetFingerprint(),
            DateTimeOffset.UtcNow,
            gitHubUrl,
            agentName));
        Save(manifest);
    }

    public void RecordPending(
        string runnerFolder,
        PortableRunnerRegistration registration,
        IReadOnlyList<string> customLabels,
        bool hasDefaultLabels)
    {
        var manifest = Load();
        var key = EncodePath(runnerFolder);
        manifest.PendingRunners.RemoveAll(x => string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase));
        manifest.PendingRunners.Add(new PortablePendingRunner(
            key,
            DateTimeOffset.UtcNow,
            registration,
            customLabels.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            hasDefaultLabels));
        Save(manifest);
    }

    public void ClearPending(string runnerFolder)
    {
        var manifest = Load();
        var key = EncodePath(runnerFolder);
        if (manifest.PendingRunners.RemoveAll(x => string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase)) > 0)
            Save(manifest);
    }

    public PortablePreparedRunner? FindPrepared(string runnerFolder)
    {
        var key = EncodePath(runnerFolder);
        return Load().PreparedRunners.FirstOrDefault(x =>
            string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase));
    }

    public PortablePendingRunner? FindPending(string runnerFolder)
    {
        var key = EncodePath(runnerFolder);
        return Load().PendingRunners.FirstOrDefault(x =>
            string.Equals(x.Path, key, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<(string FolderPath, PortablePendingRunner Pending)> GetPending()
    {
        var result = new List<(string, PortablePendingRunner)>();
        foreach (var pending in Load().PendingRunners)
        {
            try { result.Add((PortablePathCodec.Decode(pending.Path, _portableRoot), pending)); }
            catch { }
        }
        return result;
    }

    private string EncodePath(string path) => PortablePathCodec.Encode(path, _portableRoot);

    private void Save(PortableWorkspaceManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(manifest, Options));
            File.Move(temporary, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
