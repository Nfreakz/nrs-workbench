using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace NRS.Workbench.App.Services;

public sealed record DiagnosticPreview(string Text, bool IncludesApplicationLog, int ApplicationLogCharacters);

public sealed class DiagnosticBundleService
{
    private const long MaxLogBytes = 256 * 1024;
    private readonly SettingsService _settingsService;

    public DiagnosticBundleService(SettingsService settingsService) => _settingsService = settingsService;

    public DiagnosticPreview BuildPreview()
    {
        var settings = _settingsService.Load();
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var versionText = version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
        var configuredPaths = settings.RunnerRoots
            .Concat(settings.RepositoryPaths)
            .Append(_settingsService.SettingsDirectory)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string rawLog;
        string? logError = null;
        try { rawLog = ReadApplicationLog(); }
        catch (Exception ex)
        {
            rawLog = string.Empty;
            logError = SensitiveDataRedactor.Redact(ex.Message);
        }

        var safeLog = DiagnosticSanitizer.Sanitize(rawLog, configuredPaths);
        var builder = new StringBuilder();
        builder.AppendLine("NRS Workbench diagnostics");
        builder.AppendLine("========================");
        builder.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"App version: {versionText}");
        builder.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"UI language: {settings.Language}");
        builder.AppendLine();
        builder.AppendLine("Configuration summary");
        builder.AppendLine("---------------------");
        builder.AppendLine($"Runner roots configured: {settings.RunnerRoots.Count} (paths omitted)");
        builder.AppendLine($"Repositories configured: {settings.RepositoryPaths.Count} (paths omitted)");
        builder.AppendLine($"Refresh interval: {settings.RefreshIntervalSeconds} seconds");
        builder.AppendLine($"Smart Queue: {(settings.RunnerQueueEnabled ? "enabled" : "disabled")}");
        builder.AppendLine($"Smart Queue limit: {settings.RunnerQueueLimit}");
        builder.AppendLine($"Smart Queue pool: {(settings.RunnerQueueUseAllRunners ? "all detected runners" : $"custom ({settings.RunnerQueueIncludedPaths.Count} selected)")}");
        builder.AppendLine($"Resource guard: {(settings.RunnerQueueResourceGuardEnabled ? "enabled" : "disabled")}");
        builder.AppendLine();
        builder.AppendLine("Privacy");
        builder.AppendLine("-------");
        builder.AppendLine("This preview intentionally omits settings.json, runner _diag files, runner credentials, repository contents and remote URLs.");
        builder.AppendLine("Known configured paths, the Windows user/machine identity, HTTP(S) user-info and GitHub token-shaped strings are redacted.");
        builder.AppendLine("Sanitization is best-effort. Review this exact preview before sharing the exported ZIP.");
        builder.AppendLine();
        builder.AppendLine("Sanitized application log tail");
        builder.AppendLine("------------------------------");

        if (!string.IsNullOrWhiteSpace(logError))
            builder.AppendLine($"Application log could not be read: {DiagnosticSanitizer.Sanitize(logError, configuredPaths)}");
        else if (string.IsNullOrWhiteSpace(safeLog))
            builder.AppendLine("(No application log content is available.)");
        else
            builder.Append(safeLog.TrimEnd()).AppendLine();

        return new DiagnosticPreview(builder.ToString(), !string.IsNullOrWhiteSpace(safeLog), safeLog.Length);
    }

    public void SaveZip(string destinationPath, DiagnosticPreview preview)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        using var file = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        WriteEntry(archive, "diagnostics.txt", preview.Text);
        WriteEntry(archive, "README.txt",
            "NRS Workbench diagnostic bundle\r\n" +
            "===============================\r\n\r\n" +
            "This ZIP was generated locally and is never uploaded automatically.\r\n" +
            "Review diagnostics.txt before sharing it.\r\n\r\n" +
            "The bundle intentionally excludes settings.json, runner _diag files, runner credentials, repository contents and remote URLs.\r\n" +
            "Sanitization is best-effort, so the user remains the final reviewer before publication.\r\n");
    }

    private string ReadApplicationLog()
    {
        var path = Path.Combine(_settingsService.SettingsDirectory, "logs", "nrs-workbench.log");
        if (!File.Exists(path)) return string.Empty;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var trimmed = stream.Length > MaxLogBytes;
        if (trimmed) stream.Seek(-MaxLogBytes, SeekOrigin.End);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
        var text = reader.ReadToEnd();
        if (trimmed)
        {
            var firstBreak = text.IndexOf('\n');
            if (firstBreak >= 0 && firstBreak + 1 < text.Length) text = text[(firstBreak + 1)..];
        }
        return text;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
