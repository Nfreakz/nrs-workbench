using System.Text.Json;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Queue ownership journal. It is never part of exported preferences. In portable
/// mode it may travel with the workspace, but it is machine-bound so a different
/// PC cannot automatically recover queue-owned stops from the previous host.
/// </summary>
public interface IRunnerQueueStateStore
{
    IReadOnlyCollection<string> Load();
    void Save(IReadOnlyCollection<string> paths);
}

public sealed class FileRunnerQueueStateStore : IRunnerQueueStateStore
{
    private readonly string _filePath;
    private readonly string? _portableRoot;
    private readonly string _machineFingerprint;

    private sealed class Journal
    {
        public int SchemaVersion { get; set; } = 2;
        public string MachineFingerprint { get; set; } = string.Empty;
        public List<string> ManagedStoppedPaths { get; set; } = [];
    }

    public FileRunnerQueueStateStore(string settingsDirectory, string? portableRoot = null)
    {
        _filePath = Path.Combine(settingsDirectory, "runner-queue-state.json");
        _portableRoot = portableRoot ??
            (AppDataPaths.IsPortable ? AppDataPaths.PortableVolumeRoot : null);
        _machineFingerprint = MachineIdentityService.GetFingerprint();
    }

    public IReadOnlyCollection<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            if (new FileInfo(_filePath).Length > 65536)
                throw new InvalidDataException("Runner queue journal is unexpectedly large.");

            using var document = JsonDocument.Parse(File.ReadAllText(_filePath));
            var root = document.RootElement;
            var schema = root.TryGetProperty("SchemaVersion", out var schemaValue) && schemaValue.TryGetInt32(out var parsed)
                ? parsed
                : 1;

            if (schema == 1)
            {
                if (_portableRoot is not null) return [];
                var legacy = JsonSerializer.Deserialize<LegacyJournal>(root.GetRawText());
                return NormalizeLoaded(legacy?.ManagedStoppedPaths ?? []);
            }

            var journal = JsonSerializer.Deserialize<Journal>(root.GetRawText());
            if (journal is null || journal.SchemaVersion != 2 || journal.ManagedStoppedPaths is null)
                throw new InvalidDataException("Runner queue journal has an unsupported format.");

            if (!string.Equals(journal.MachineFingerprint, _machineFingerprint, StringComparison.Ordinal))
            {
                AppLogger.Info("Portable queue journal belongs to another machine and was ignored.");
                return [];
            }

            var paths = _portableRoot is null
                ? journal.ManagedStoppedPaths
                : journal.ManagedStoppedPaths.Select(path => PortablePathCodec.Decode(path, _portableRoot)).ToList();
            return NormalizeLoaded(paths);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not read runner queue journal", ex);
            return [];
        }
    }

    public void Save(IReadOnlyCollection<string> paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        if (paths.Count == 0)
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return;
        }

        var storedPaths = _portableRoot is null
            ? paths.ToList()
            : paths.Select(path => PortablePathCodec.Encode(path, _portableRoot)).ToList();

        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var journal = new Journal
            {
                MachineFingerprint = _machineFingerprint,
                ManagedStoppedPaths = storedPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(journal));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static IReadOnlyCollection<string> NormalizeLoaded(IEnumerable<string> paths) =>
        paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class LegacyJournal
    {
        public int SchemaVersion { get; set; } = 1;
        public List<string> ManagedStoppedPaths { get; set; } = [];
    }
}
