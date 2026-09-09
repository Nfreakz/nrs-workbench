namespace NRS.Workbench.App.Services;

public static class AppLogger
{
    private static readonly object Gate = new();
    private static string? _logFile;

    public static void Initialize()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NRSWorkbench",
            "logs");
        Directory.CreateDirectory(folder);
        _logFile = Path.Combine(folder, "nrs-workbench.log");
        Info("NRS Workbench starting");
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            if (_logFile is null) return;
            var safeMessage = SensitiveDataRedactor.Redact(message);
            var text = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {level} {safeMessage}";
            if (exception is not null) text += Environment.NewLine + SensitiveDataRedactor.Redact(exception.ToString());
            lock (Gate) File.AppendAllText(_logFile, text + Environment.NewLine);
        }
        catch { }
    }
}
