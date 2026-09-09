using System.Text.RegularExpressions;

namespace NRS.Workbench.App.Services;

public static partial class SensitiveDataRedactor
{
    [GeneratedRegex(@"(?i)(?<scheme>https?://)[^/\s@]+@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlUserInfoRegex();

    [GeneratedRegex(@"\b(?:github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,})\b", RegexOptions.CultureInvariant)]
    private static partial Regex GitHubTokenRegex();

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        var redacted = GitHubTokenRegex().Replace(text, "[REDACTED_GITHUB_TOKEN]");
        redacted = UrlUserInfoRegex().Replace(redacted, match => match.Groups["scheme"].Value + "***@");
        return redacted;
    }
}
