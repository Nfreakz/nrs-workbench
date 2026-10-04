using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NRS.Workbench.App.Services;

public sealed record GitHubRunnerRemoteMetadata(
    string Name,
    IReadOnlyList<string> CustomLabels,
    bool HasDefaultLabels,
    string Status = "",
    bool IsBusy = false);

public sealed record GitHubRunnerRegistrationToken(string Token, DateTimeOffset? ExpiresAt);

public sealed class GitHubRunnerRegistrationClient
{
    private readonly HttpClient _http;

    public GitHubRunnerRegistrationClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
    }

    public async Task<GitHubRunnerRemoteMetadata> GetRunnerAsync(
        string gitHubUrl,
        ulong agentId,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        var target = ParseTarget(gitHubUrl);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"https://api.github.com/{target.ApiPrefix}/actions/runners/{agentId}",
            personalAccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "read runner metadata");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ParseRemoteMetadata(json.RootElement);
    }

    public async Task<GitHubRunnerRemoteMetadata> GetRunnerByNameAsync(
        string gitHubUrl,
        string agentName,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Runner name is required.", nameof(agentName));

        var target = ParseTarget(gitHubUrl);
        var encodedName = Uri.EscapeDataString(agentName.Trim());
        using var request = CreateRequest(
            HttpMethod.Get,
            $"https://api.github.com/{target.ApiPrefix}/actions/runners?name={encodedName}&per_page=100",
            personalAccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "find runner by name");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (!root.TryGetProperty("runners", out var runners) || runners.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("GitHub returned an invalid runner list.");

        var exactMatches = runners.EnumerateArray()
            .Where(item =>
                item.TryGetProperty("name", out var nameValue) &&
                string.Equals(nameValue.GetString(), agentName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Count == 0)
            throw new InvalidOperationException($"GitHub runner '{agentName}' was not found.");
        if (exactMatches.Count > 1)
            throw new InvalidOperationException($"GitHub returned multiple runners named '{agentName}' for the same target.");

        return ParseRemoteMetadata(exactMatches[0]);
    }

    public async Task<GitHubRunnerRegistrationToken> CreateRegistrationTokenAsync(
        string gitHubUrl,
        string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        var target = ParseTarget(gitHubUrl);
        using var request = CreateRequest(
            HttpMethod.Post,
            $"https://api.github.com/{target.ApiPrefix}/actions/runners/registration-token",
            personalAccessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "create runner registration token");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var token = root.TryGetProperty("token", out var tokenValue)
            ? tokenValue.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub returned an empty runner registration token.");

        DateTimeOffset? expiresAt = null;
        if (root.TryGetProperty("expires_at", out var expiresValue) &&
            DateTimeOffset.TryParse(expiresValue.GetString(), out var parsed))
            expiresAt = parsed;

        return new GitHubRunnerRegistrationToken(token, expiresAt);
    }

    public static GitHubRunnerTarget ParseTarget(string gitHubUrl)
    {
        if (!Uri.TryCreate(gitHubUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Portable runner preparation currently supports github.com URLs only.");

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var owner = Uri.EscapeDataString(parts[0]);
            var repository = Uri.EscapeDataString(parts[1]);
            return new GitHubRunnerTarget(true, parts[0], parts[1], $"repos/{owner}/{repository}");
        }

        if (parts.Length == 1)
        {
            var organization = Uri.EscapeDataString(parts[0]);
            return new GitHubRunnerTarget(false, parts[0], string.Empty, $"orgs/{organization}");
        }

        throw new NotSupportedException("Could not determine the GitHub repository or organization from the runner URL.");
    }

    private static GitHubRunnerRemoteMetadata ParseRemoteMetadata(JsonElement root)
    {
        var name = root.TryGetProperty("name", out var nameValue)
            ? nameValue.GetString() ?? string.Empty
            : string.Empty;
        var custom = new List<string>();
        var hasDefault = false;

        if (root.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
        {
            foreach (var label in labels.EnumerateArray())
            {
                var labelName = label.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var type = label.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(labelName)) continue;
                if (string.Equals(type, "custom", StringComparison.OrdinalIgnoreCase))
                    custom.Add(labelName);
                else if (string.Equals(type, "read-only", StringComparison.OrdinalIgnoreCase))
                    hasDefault = true;
            }
        }

        var status = root.TryGetProperty("status", out var statusValue)
            ? statusValue.GetString() ?? string.Empty
            : string.Empty;
        var busy = root.TryGetProperty("busy", out var busyValue) &&
                   busyValue.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                   busyValue.GetBoolean();

        return new GitHubRunnerRemoteMetadata(
            name,
            custom.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            hasDefault,
            status,
            busy);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A GitHub personal access token is required.", nameof(token));

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("NRS-Workbench");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;

        var hint = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Check the GitHub token.",
            HttpStatusCode.Forbidden => "The token does not have the required runner administration permission.",
            HttpStatusCode.NotFound => "The runner or GitHub target was not found, or the token cannot access it.",
            _ => "GitHub rejected the request."
        };
        await Task.CompletedTask;
        throw new InvalidOperationException(
            $"Could not {operation}. GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}. {hint}");
    }
}

public sealed record GitHubRunnerTarget(
    bool IsRepository,
    string Owner,
    string Repository,
    string ApiPrefix);
