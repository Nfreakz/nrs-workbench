using System.Text.Json;

namespace NRS.Workbench.App.Services;

public sealed record RuntimeSessionSnapshot(
    string SessionId,
    int ProcessId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt,
    bool Preview,
    string MachineFingerprint);

public sealed class RuntimeSessionTracker
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
    private readonly string _statePath;
    private readonly string _machineFingerprint;
    private readonly bool _portableMode;
    private RuntimeSessionSnapshot? _current;
    private DateTimeOffset _lastPersistedHeartbeat;

    public RuntimeSessionTracker(
        string settingsDirectory,
        string? machineFingerprint = null,
        bool? portableMode = null)
    {
        _statePath = Path.Combine(settingsDirectory, "runtime-session.json");
        _machineFingerprint = machineFingerprint ?? MachineIdentityService.GetFingerprint();
        _portableMode = portableMode ?? AppDataPaths.IsPortable;
    }

    public RuntimeSessionSnapshot? BeginSession(bool preview)
    {
        RuntimeSessionSnapshot? previous = null;
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                previous = JsonSerializer.Deserialize<RuntimeSessionSnapshot>(json);

                if (_portableMode &&
                    (previous is null ||
                     string.IsNullOrWhiteSpace(previous.MachineFingerprint) ||
                     !string.Equals(previous.MachineFingerprint, _machineFingerprint, StringComparison.Ordinal)))
                {
                    AppLogger.Info("Portable runtime-session marker belongs to another machine and was ignored.");
                    previous = null;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not read previous runtime session marker", ex);
        }

        var now = DateTimeOffset.Now;
        _current = new RuntimeSessionSnapshot(
            Guid.NewGuid().ToString("N"),
            Environment.ProcessId,
            now,
            now,
            preview,
            _machineFingerprint);
        _lastPersistedHeartbeat = now;
        Persist();
        return previous;
    }

    public void Heartbeat()
    {
        if (_current is null) return;
        var now = DateTimeOffset.Now;
        if (now - _lastPersistedHeartbeat < HeartbeatInterval) return;

        _current = _current with { LastHeartbeatAt = now };
        _lastPersistedHeartbeat = now;
        Persist();
    }

    public void EndSession()
    {
        _current = null;
        try
        {
            if (File.Exists(_statePath)) File.Delete(_statePath);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not clear runtime session marker", ex);
        }
    }

    private void Persist()
    {
        if (_current is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var temporary = _statePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_current));
            File.Move(temporary, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not persist runtime session heartbeat", ex);
        }
    }
}
