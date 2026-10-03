namespace NRS.Workbench.Platform.Linux;

public enum SystemdServiceState
{
    Unknown = 0,
    Inactive = 1,
    Activating = 2,
    Active = 3,
    Deactivating = 4,
    Failed = 5
}

public sealed class SystemdServiceController
{
    private readonly ILinuxCommandRunner _commands;

    public SystemdServiceController(ILinuxCommandRunner? commands = null)
    {
        _commands = commands ?? new ProcessLinuxCommandRunner();
    }

    public bool Exists(string serviceName)
    {
        serviceName = ValidateServiceName(serviceName);
        var result = _commands.Run(
            "systemctl",
            "show",
            "--property=LoadState",
            "--value",
            "--",
            serviceName);

        return result.Success &&
               !string.Equals(result.StdOut.Trim(), "not-found", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(result.StdOut);
    }

    public SystemdServiceState GetState(string serviceName)
    {
        serviceName = ValidateServiceName(serviceName);
        var result = _commands.Run(
            "systemctl",
            "show",
            "--property=ActiveState",
            "--value",
            "--",
            serviceName);

        if (!result.Success) return SystemdServiceState.Unknown;

        return result.StdOut.Trim().ToLowerInvariant() switch
        {
            "active" => SystemdServiceState.Active,
            "inactive" => SystemdServiceState.Inactive,
            "activating" => SystemdServiceState.Activating,
            "deactivating" => SystemdServiceState.Deactivating,
            "failed" => SystemdServiceState.Failed,
            _ => SystemdServiceState.Unknown
        };
    }

    public void Start(string serviceName) => RunControl("start", serviceName);

    public void Stop(string serviceName) => RunControl("stop", serviceName);

    private void RunControl(string action, string serviceName)
    {
        serviceName = ValidateServiceName(serviceName);
        var result = _commands.Run("systemctl", action, "--", serviceName);
        if (result.Success) return;

        var detail = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
        throw new InvalidOperationException(
            $"systemctl {action} failed for '{serviceName}' ({result.ExitCode}): {detail}");
    }

    private static string ValidateServiceName(string serviceName)
    {
        serviceName = serviceName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serviceName) ||
            serviceName.Length > 256 ||
            serviceName.StartsWith("-", StringComparison.Ordinal) ||
            serviceName.Contains('/') ||
            serviceName.Contains('\\') ||
            serviceName.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Invalid systemd service name.", nameof(serviceName));
        }

        return serviceName;
    }
}
