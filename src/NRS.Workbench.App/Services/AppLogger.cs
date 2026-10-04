using System.Text;

namespace NRS.Workbench.App.Services;

public sealed record AppLogCleanupResult(int CleanedFiles, long ReclaimedBytes, int FailedFiles);

public static class AppLogger
{
    private const long MaxLogBytes = 5L * 1024 * 1024;
    private const int MaxArchives = 5;
    private static readonly object Gate = new();
    private static string? _logFile;

    public static void Initialize()
    {
        var folder = Path.Combine(AppDataPaths.SettingsDirectory, "logs");
        Directory.CreateDirectory(folder);
        _logFile = Path.Combine(folder, "nrs-workbench.log");
        lock (Gate) RotateIfNeededLocked(0);
        Info("NRS Workbench starting");
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public static AppLogCleanupResult ClearHistory()
    {
        lock (Gate)
        {
            if (_logFile is null) return new AppLogCleanupResult(0, 0, 0);
            var folder = Path.GetDirectoryName(_logFile);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return new AppLogCleanupResult(0, 0, 0);

            var cleaned = 0;
            var failed = 0;
            long reclaimed = 0;

            foreach (var file in Directory.EnumerateFiles(folder, "nrs-workbench*.log", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var length = new FileInfo(file).Length;
                    if (string.Equals(file, _logFile, StringComparison.OrdinalIgnoreCase))
                        File.WriteAllText(file, string.Empty, new UTF8Encoding(false));
                    else
                        File.Delete(file);

                    reclaimed += length;
                    cleaned++;
                }
                catch
                {
                    failed++;
                }
            }

            return new AppLogCleanupResult(cleaned, reclaimed, failed);
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            if (_logFile is null) return;
            var safeMessage = SensitiveDataRedactor.Redact(message);
            var text = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {level} {safeMessage}";
            if (exception is not null) text += Environment.NewLine + SensitiveDataRedactor.Redact(exception.ToString());
            text += Environment.NewLine;

            lock (Gate)
            {
                RotateIfNeededLocked(Encoding.UTF8.GetByteCount(text));
                File.AppendAllText(_logFile, text, new UTF8Encoding(false));
            }
        }
        catch { }
    }

    private static void RotateIfNeededLocked(long incomingBytes)
    {
        if (_logFile is null || !File.Exists(_logFile)) return;
        long currentLength;
        try { currentLength = new FileInfo(_logFile).Length; }
        catch { return; }
        if (currentLength + incomingBytes <= MaxLogBytes) return;

        for (var index = MaxArchives; index >= 1; index--)
        {
            var destination = ArchivePath(index);
            try
            {
                if (index == MaxArchives && File.Exists(destination)) File.Delete(destination);
                var source = index == 1 ? _logFile : ArchivePath(index - 1);
                if (!File.Exists(source)) continue;
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(source, destination);
            }
            catch { }
        }
    }

    private static string ArchivePath(int index) =>
        Path.Combine(Path.GetDirectoryName(_logFile!)!, $"nrs-workbench.{index}.log");
}
