namespace NRS.Workbench.App.Services;

public static class DiagnosticSanitizer
{
    public static string Sanitize(
        string? text,
        IEnumerable<string>? sensitivePaths = null,
        string? userName = null,
        string? machineName = null,
        string? userProfile = null,
        string? localAppData = null)
    {
        var result = SensitiveDataRedactor.Redact(text);
        if (string.IsNullOrEmpty(result)) return result;

        userName ??= Environment.UserName;
        machineName ??= Environment.MachineName;
        userProfile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var replacements = new List<(string Value, string Replacement)>();
        Add(replacements, localAppData, "<LOCALAPPDATA>", 5);
        Add(replacements, userProfile, "<USERPROFILE>", 5);

        foreach (var path in sensitivePaths ?? [])
            Add(replacements, path, "<CONFIGURED_PATH>", 5);

        Add(replacements, machineName, "<MACHINE>", 3);
        Add(replacements, userName, "<USER>", 3);

        foreach (var item in replacements
                     .DistinctBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(item => item.Value.Length))
            result = result.Replace(item.Value, item.Replacement, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static void Add(List<(string Value, string Replacement)> items, string? value, string replacement, int minLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length < minLength) return;
        items.Add((normalized, replacement));
    }
}
