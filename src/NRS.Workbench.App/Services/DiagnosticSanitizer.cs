using System.Text.RegularExpressions;

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

        foreach (var item in replacements
                     .DistinctBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(item => item.Value.Length))
            result = result.Replace(item.Value, item.Replacement, StringComparison.OrdinalIgnoreCase);

        // User/machine names are replaced as whole tokens (no minimum length) so a short
        // name is still redacted without mangling unrelated words that merely contain it.
        result = ReplaceToken(result, machineName, "<MACHINE>");
        result = ReplaceToken(result, userName, "<USER>");

        return result;
    }

    private static string ReplaceToken(string text, string? name, string replacement)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrEmpty(normalized)) return text;
        return Regex.Replace(
            text,
            $@"(?<![A-Za-z0-9]){Regex.Escape(normalized)}(?![A-Za-z0-9])",
            replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void Add(List<(string Value, string Replacement)> items, string? value, string replacement, int minLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = value.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length < minLength) return;
        items.Add((normalized, replacement));
    }
}
