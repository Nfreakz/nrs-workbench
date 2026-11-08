using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed record UpdateCheckResult(Version LatestVersion, string TagName, string ReleaseUrl, bool IsNewer);

/// <summary>
/// Anonymous "is there a newer release?" check against the public GitHub REST API.
/// It never sends a token, runs at most once per 24 hours and swallows every error:
/// a failed check must not be visible to the user beyond a log line.
/// </summary>
public sealed class UpdateCheckService
{
    // Repository comes from the RepositoryUrl assembly metadata (Directory.Build.props),
    // like FeedbackWindow, so no account name is hardcoded in source.
    private static readonly string RepositoryPath = ResolveRepositoryPath();
    public static string ReleasesPageUrl => $"https://github.com/{RepositoryPath}/releases";
    private static string ApiBase => $"https://api.github.com/repos/{RepositoryPath}";

    private static string ResolveRepositoryPath()
    {
        var value = typeof(UpdateCheckService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "RepositoryUrl", StringComparison.Ordinal))
            ?.Value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Trim('/');
        return string.Empty;
    }

    /// <summary>False when the build carries no GitHub RepositoryUrl; the check is then skipped.</summary>
    public static bool IsConfigured => RepositoryPath.Count(c => c == '/') == 1;
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const long MaxResponseBytes = 512 * 1024;

    private readonly SettingsService _settings;
    private readonly HttpClient _http;

    /// <summary>Newer release found by the last completed check in this process, if any.</summary>
    public static UpdateCheckResult? AvailableUpdate { get; private set; }

    public UpdateCheckService(SettingsService settings, HttpClient? httpClient = null)
    {
        _settings = settings;
        _http = httpClient ?? new HttpClient { Timeout = RequestTimeout };
    }

    public static Version CurrentVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version
                ?? typeof(UpdateCheckService).Assembly.GetName().Version;
            return version is null ? new Version(0, 0, 0) : new Version(version.Major, version.Minor, Math.Max(0, version.Build));
        }
    }

    public static string CurrentVersionDisplay
    {
        get
        {
            var version = CurrentVersion;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>
    /// Returns an update result when the check is enabled, due and finds a newer
    /// release. Returns null in every other case, including on errors.
    /// </summary>
    public async Task<UpdateCheckResult?> CheckIfDueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _settings.Load();
            if (!settings.UpdateCheckEnabled || !IsConfigured) return null;
            var now = DateTimeOffset.UtcNow;
            if (settings.LastUpdateCheckUtc is { } last && last <= now && now - last < MinimumInterval) return null;

            var result = await CheckNowAsync(cancellationToken).ConfigureAwait(false);

            // Persist the timestamp only after a completed round-trip so a transient
            // network failure is retried on the next launch instead of in 24 h.
            if (result is not null)
            {
                var latest = _settings.Load();
                latest.LastUpdateCheckUtc = now;
                _settings.Save(latest);
            }

            if (result is not { IsNewer: true }) return null;
            AvailableUpdate = result;
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Update check failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Queries GitHub regardless of the 24 h interval. Returns null when no release
    /// could be read. Throws on network/parse errors so callers can log them.
    /// </summary>
    public async Task<UpdateCheckResult?> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        // "releases/latest" ignores prereleases and answers 404 while the project only
        // publishes prereleases; fall back to the newest entry of the releases list.
        var json = await GetAsync(ApiBase + "/releases/latest", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json ?? await GetAsync(ApiBase + "/releases?per_page=5", cancellationToken).ConfigureAwait(false) ?? "null");
        var release = document.RootElement.ValueKind switch
        {
            JsonValueKind.Object => document.RootElement,
            JsonValueKind.Array when document.RootElement.GetArrayLength() > 0 => document.RootElement[0],
            _ => default
        };
        if (release.ValueKind != JsonValueKind.Object) return null;

        var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var url = release.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
        return Evaluate(tag, url, CurrentVersion);
    }

    /// <summary>Pure comparison, separated so it can be exercised without network access.</summary>
    public static UpdateCheckResult? Evaluate(string? tagName, string? htmlUrl, Version current)
    {
        if (!TryParseTag(tagName, out var latest)) return null;
        var safeUrl = IsSafeGitHubUrl(htmlUrl) ? htmlUrl! : ReleasesPageUrl;
        return new UpdateCheckResult(latest, tagName!.Trim(), safeUrl, latest > current);
    }

    public static bool TryParseTag(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName)) return false;
        var text = tagName.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        var dash = text.IndexOfAny(['-', '+']);
        if (dash > 0) text = text[..dash];
        var parts = text.Split('.');
        if (parts.Length < 2 || parts.Length > 4) return false;
        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (i >= parts.Length) { numbers[i] = 0; continue; }
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out numbers[i])) return false;
        }
        version = new Version(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    private static bool IsSafeGitHubUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("NRSWorkbench/" + CurrentVersionDisplay);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            throw new InvalidDataException("GitHub release response is unexpectedly large.");
        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (body.Length > MaxResponseBytes)
            throw new InvalidDataException("GitHub release response is unexpectedly large.");
        return body;
    }
}
