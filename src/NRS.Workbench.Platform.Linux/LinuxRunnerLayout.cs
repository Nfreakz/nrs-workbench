namespace NRS.Workbench.Platform.Linux;

public static class LinuxRunnerLayout
{
    public const string RunScriptName = "run.sh";
    public const string ListenerRelativePath = "bin/Runner.Listener";
    public const string WorkerRelativePath = "bin/Runner.Worker";
    public const string RegistrationFileName = ".runner";
    public const string ServiceFileName = ".service";

    public static bool IsRunnerFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

        return File.Exists(Path.Combine(folder, RegistrationFileName)) ||
               File.Exists(Path.Combine(folder, RunScriptName)) ||
               File.Exists(Path.Combine(folder, "bin", "Runner.Listener"));
    }

    public static IReadOnlyList<string> FindRunnerFolders(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        if (IsRunnerFolder(root))
            return [Path.GetFullPath(root)];

        var folders = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsSymbolicLink(folder)) continue;
                if (IsRunnerFolder(folder))
                    folders.Add(Path.GetFullPath(folder));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return folders.OrderBy(path => path, StringComparer.Ordinal).ToList();
    }

    public static bool ContainsRunnerFolder(string root) => FindRunnerFolders(root).Count > 0;

    public static string ReadServiceName(string runnerFolder)
    {
        try
        {
            var path = Path.Combine(runnerFolder, ServiceFileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}
