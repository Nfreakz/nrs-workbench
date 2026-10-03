using System.Diagnostics;

namespace NRS.Workbench.Platform.Linux;

public sealed record LinuxCommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

public interface ILinuxCommandRunner
{
    LinuxCommandResult Run(string fileName, params string[] arguments);
}

public sealed class ProcessLinuxCommandRunner : ILinuxCommandRunner
{
    public LinuxCommandResult Run(string fileName, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var start = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new LinuxCommandResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult().Trim(),
            stderr.GetAwaiter().GetResult().Trim());
    }
}
