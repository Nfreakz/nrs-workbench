using System.Text.Json;

namespace NRS.Workbench.App.Services;

/// <summary>
/// Local-only queue ownership journal. This file is deliberately excluded from
/// portable preferences because runner paths refer to one specific machine.
/// </summary>
public interface IRunnerQueueStateStore
{
    IReadOnlyCollection<string> Load();
    void Save(IReadOnlyCollection<string> paths);
}

public sealed class FileRunnerQueueStateStore : IRunnerQueueStateStore
{
    private readonly string _filePath;
    private sealed class Journal
    {
        public int SchemaVersion { get; set; } = 1;
        public List<string> ManagedStoppedPaths { get; set; } = [];
    }

    public FileRunnerQueueStateStore(string settingsDirectory) =>
        _filePath = Path.Combine(settingsDirectory, "runner-queue-state.json");

    public IReadOnlyCollection<string> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            if (new FileInfo(_filePath).Length > 65536)
                throw new InvalidDataException("Runner queue journal is unexpectedly large.");
            var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(_filePath));
            if (journal is null || journal.SchemaVersion != 1 || journal.ManagedStoppedPaths is null)
                throw new InvalidDataException("Runner queue journal has an unsupported format.");
            return journal.ManagedStoppedPaths
                .Where(path => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            // Fail closed: never restart unknown runners from an invalid journal.
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
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var journal = new Journal
            {
                ManagedStoppedPaths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(journal));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
