using System.Text.Json;

namespace NRS.Workbench.App.Services;

public static class PortableRunnerMetadataReader
{
    public static PortableRunnerRegistration? Read(string runnerFolder)
    {
        var path = Path.Combine(runnerFolder, ".runner");
        if (!File.Exists(path)) return null;

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var root = json.RootElement;

            var agentId = ReadUInt64(root, "agentId");
            var agentName = ReadString(root, "agentName");
            var gitHubUrl = ReadString(root, "gitHubUrl");
            if (string.IsNullOrWhiteSpace(gitHubUrl))
            {
                var serverUrl = ReadString(root, "serverUrl");
                if (serverUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                    gitHubUrl = serverUrl;
            }

            if (agentId == 0 || string.IsNullOrWhiteSpace(agentName) || string.IsNullOrWhiteSpace(gitHubUrl))
                return null;

            return new PortableRunnerRegistration(
                agentId,
                agentName,
                gitHubUrl.TrimEnd('/'),
                DefaultIfBlank(ReadString(root, "workFolder"), "_work"),
                DefaultIfBlank(ReadString(root, "poolName"), "Default"),
                ReadBool(root, "disableUpdate"),
                ReadBool(root, "ephemeral"));
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not read portable runner metadata for '{runnerFolder}'", ex);
            return null;
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static ulong ReadUInt64(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetUInt64(out var result) ? result : 0;

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static string DefaultIfBlank(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
