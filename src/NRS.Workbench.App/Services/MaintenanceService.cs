using System.ComponentModel;
using System.Text.Json;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public enum MaintenanceRisk { Safe, History, Inventory }

public sealed class MaintenanceEntry : INotifyPropertyChanged
{
    private bool _isSelected;
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Detail { get; init; }
    public string Location { get; init; } = string.Empty;
    public int FileCount { get; init; }
    public long TotalBytes { get; init; }
    public long ReclaimableBytes { get; init; }
    public required MaintenanceRisk Risk { get; init; }
    public required string RiskLabel { get; init; }
    public bool IsCleanable { get; init; }
    public IReadOnlyList<string> CandidateFiles { get; init; } = [];
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public string TotalDisplay => MaintenanceService.FormatBytes(TotalBytes);
    public string ReclaimableDisplay => ReclaimableBytes <= 0 ? "—" : MaintenanceService.FormatBytes(ReclaimableBytes);
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record MaintenanceScanResult(IReadOnlyList<MaintenanceEntry> Entries, long TotalBytes, long ReclaimableBytes)
{
    public string TotalDisplay => MaintenanceService.FormatBytes(TotalBytes);
    public string ReclaimableDisplay => MaintenanceService.FormatBytes(ReclaimableBytes);
}

public sealed record MaintenanceCleanupResult(int DeletedFiles, long ReclaimedBytes, int FailedFiles);

public sealed class MaintenanceService
{
    private readonly string _settingsDirectory;
    private readonly Func<DateTimeOffset> _now;

    public MaintenanceService(string settingsDirectory, Func<DateTimeOffset>? now = null)
    {
        _settingsDirectory = settingsDirectory;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public Task<MaintenanceScanResult> ScanAsync(IReadOnlyList<RunnerInfo> runners, int olderThanDays, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(runners, olderThanDays, cancellationToken), cancellationToken);

    public Task<MaintenanceCleanupResult> CleanupAsync(IReadOnlyList<MaintenanceEntry> entries, CancellationToken cancellationToken = default) =>
        Task.Run(() => Cleanup(entries, cancellationToken), cancellationToken);

    private MaintenanceScanResult Scan(IReadOnlyList<RunnerInfo> runners, int olderThanDays, CancellationToken cancellationToken)
    {
        olderThanDays = Math.Clamp(olderThanDays, 7, 365);
        var entries = new List<MaintenanceEntry>();
        AddWorkbenchLogs(entries);
        AddPreviewLogs(entries);

        foreach (var runner in runners.OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddRunnerDiagnostics(entries, runner, olderThanDays);
            AddRunnerWorkInventory(entries, runner, cancellationToken);
        }

        return new MaintenanceScanResult(entries, entries.Sum(x => x.TotalBytes), entries.Sum(x => x.ReclaimableBytes));
    }

    private void AddWorkbenchLogs(List<MaintenanceEntry> entries)
    {
        var folder = Path.Combine(_settingsDirectory, "logs");
        var files = SafeFiles(folder, "nrs-workbench*.log");
        if (files.Count == 0) return;
        var total = files.Sum(SafeLength);
        entries.Add(new MaintenanceEntry
        {
            Id = "workbench-logs",
            Category = UiLanguage.Choose("Logs de NRS Workbench", "NRS Workbench logs", "Logs de NRS Workbench"),
            Detail = UiLanguage.Choose(
                "El log activo rota a 5 MB y conserva hasta 5 archivos. Puedes vaciar el historial local cuando ya no lo necesites.",
                "The active log rotates at 5 MB and keeps up to 5 files. You can clear local history when it is no longer needed.",
                "El log actiu rota als 5 MB i conserva fins a 5 fitxers. Pots buidar l'historial local quan ja no el necessitis."),
            Location = folder, FileCount = files.Count, TotalBytes = total, ReclaimableBytes = total,
            Risk = MaintenanceRisk.Safe, RiskLabel = UiLanguage.Choose("Seguro", "Safe", "Segur"),
            IsCleanable = true, IsSelected = true, CandidateFiles = files
        });
    }

    private void AddPreviewLogs(List<MaintenanceEntry> entries)
    {
        if (AppDataPaths.IsPreview) return;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NRSWorkbenchPreview", "logs");
        var files = SafeFiles(folder, "*.log");
        if (files.Count == 0) return;
        var total = files.Sum(SafeLength);
        entries.Add(new MaintenanceEntry
        {
            Id = "preview-logs",
            Category = UiLanguage.Choose("Logs de PREVIEW", "PREVIEW logs", "Logs de PREVIEW"),
            Detail = UiLanguage.Choose("Logs del perfil de pruebas aislado.", "Logs from the isolated preview profile.", "Logs del perfil de proves aïllat."),
            Location = folder, FileCount = files.Count, TotalBytes = total, ReclaimableBytes = total,
            Risk = MaintenanceRisk.Safe, RiskLabel = UiLanguage.Choose("Seguro", "Safe", "Segur"),
            IsCleanable = true, IsSelected = true, CandidateFiles = files
        });
    }

    private void AddRunnerDiagnostics(List<MaintenanceEntry> entries, RunnerInfo runner, int olderThanDays)
    {
        var folder = Path.Combine(runner.FolderPath, "_diag");
        var files = SafeFiles(folder, "*.log");
        if (files.Count == 0) return;
        var total = files.Sum(SafeLength);
        var cutoff = _now().AddDays(-olderThanDays).UtcDateTime;
        var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var newestWorker = files.Where(x => Path.GetFileName(x).StartsWith("Worker_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(SafeLastWriteUtc).FirstOrDefault();
        var newestRunner = files.Where(x => Path.GetFileName(x).StartsWith("Runner_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(SafeLastWriteUtc).FirstOrDefault();
        if (newestWorker is not null) protectedPaths.Add(newestWorker);
        if (newestRunner is not null) protectedPaths.Add(newestRunner);

        var candidates = runner.State == RunnerState.Busy
            ? new List<string>()
            : files.Where(x => SafeLastWriteUtc(x) < cutoff && !protectedPaths.Contains(x)).ToList();

        entries.Add(new MaintenanceEntry
        {
            Id = "diag:" + runner.FolderPath,
            Category = UiLanguage.Choose($"Diagnósticos · {runner.Alias}", $"Diagnostics · {runner.Alias}", $"Diagnòstics · {runner.Alias}"),
            Detail = runner.State == RunnerState.Busy
                ? UiLanguage.Choose("Runner BUSY: limpieza bloqueada hasta que termine el job.", "Runner is BUSY: cleanup is blocked until the job finishes.", "Runner BUSY: la neteja queda bloquejada fins que acabi el job.")
                : UiLanguage.Choose(
                    $"Logs de más de {olderThanDays} días. Siempre se conservan los Runner/Worker más recientes. Limpiar Worker antiguos reduce estadísticas y referencias de estimación.",
                    $"Logs older than {olderThanDays} days. The newest Runner/Worker logs are always kept. Removing old Worker logs reduces statistics and estimation references.",
                    $"Logs de més de {olderThanDays} dies. Sempre es conserven els Runner/Worker més recents. Netejar Worker antics redueix estadístiques i referències d'estimació."),
            Location = folder, FileCount = files.Count, TotalBytes = total, ReclaimableBytes = candidates.Sum(SafeLength),
            Risk = MaintenanceRisk.History, RiskLabel = UiLanguage.Choose("Afecta historial", "Affects history", "Afecta l'historial"),
            IsCleanable = candidates.Count > 0, IsSelected = false, CandidateFiles = candidates
        });
    }

    private static void AddRunnerWorkInventory(List<MaintenanceEntry> entries, RunnerInfo runner, CancellationToken cancellationToken)
    {
        var workName = ReadWorkFolder(runner.FolderPath);
        var folder = Path.IsPathRooted(workName) ? workName : Path.Combine(runner.FolderPath, workName);
        if (!Directory.Exists(folder)) return;
        var measured = MeasureTree(folder, cancellationToken);
        entries.Add(new MaintenanceEntry
        {
            Id = "work:" + runner.FolderPath,
            Category = UiLanguage.Choose($"Trabajo · {runner.Alias}", $"Work directory · {runner.Alias}", $"Treball · {runner.Alias}"),
            Detail = UiLanguage.Choose(
                "Inventario de _work. No se elimina automáticamente porque puede contener un checkout o archivos de un job.",
                "_work inventory. It is never deleted automatically because it may contain a checkout or job files.",
                "Inventari de _work. No s'elimina automàticament perquè pot contenir un checkout o fitxers d'un job."),
            Location = folder, FileCount = measured.Count, TotalBytes = measured.Bytes, ReclaimableBytes = 0,
            Risk = MaintenanceRisk.Inventory, RiskLabel = UiLanguage.Choose("Solo inventario", "Inventory only", "Només inventari"),
            IsCleanable = false, IsSelected = false
        });
    }

    private MaintenanceCleanupResult Cleanup(IReadOnlyList<MaintenanceEntry> entries, CancellationToken cancellationToken)
    {
        var deleted = 0; var failed = 0; long reclaimed = 0;
        foreach (var entry in entries.Where(x => x.IsSelected && x.IsCleanable))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Id == "workbench-logs" &&
                string.Equals(Path.GetFullPath(_settingsDirectory), Path.GetFullPath(AppDataPaths.SettingsDirectory), StringComparison.OrdinalIgnoreCase))
            {
                reclaimed += AppLogger.ClearHistory();
                deleted += entry.FileCount;
                continue;
            }
            foreach (var path in entry.CandidateFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var length = SafeLength(path);
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    reclaimed += length;
                    deleted++;
                }
                catch { failed++; }
            }
        }
        return new MaintenanceCleanupResult(deleted, reclaimed, failed);
    }

    private static List<string> SafeFiles(string folder, string pattern)
    {
        try { return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly).ToList() : []; }
        catch { return []; }
    }

    private static (int Count, long Bytes) MeasureTree(string root, CancellationToken cancellationToken)
    {
        var count = 0; long bytes = 0; var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    count++; bytes += SafeLength(file);
                }
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue; }
                    catch { continue; }
                    pending.Push(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (count, bytes);
    }

    private static string ReadWorkFolder(string runnerFolder)
    {
        try
        {
            var path = Path.Combine(runnerFolder, ".runner");
            if (!File.Exists(path)) return "_work";
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (json.RootElement.TryGetProperty("workFolder", out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!;
        }
        catch { }
        return "_work";
    }

    private static long SafeLength(string path) { try { return File.Exists(path) ? new FileInfo(path).Length : 0; } catch { return 0; } }
    private static DateTime SafeLastWriteUtc(string path) { try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MaxValue; } }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024d:N1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:N1} MB";
        return $"{bytes / 1024d / 1024d / 1024d:N2} GB";
    }
}
