namespace NRS.Workbench.App.Services;

public static class RunnerFolderDiscovery
{
    public static bool IsRunnerFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;

        return File.Exists(Path.Combine(folder, ".runner")) ||
               File.Exists(Path.Combine(folder, "run.cmd")) ||
               File.Exists(Path.Combine(folder, "bin", "Runner.Listener.exe"));
    }

    public static IReadOnlyList<string> FindRunnerFolders(string root)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        try
        {
            if (IsRunnerFolder(root))
                return [Path.GetFullPath(root)];

            foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsRunnerFolder(folder))
                    folders.Add(Path.GetFullPath(folder));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Could not inspect runner root '{root}'", ex);
        }

        return folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool ContainsRunnerFolder(string root) => FindRunnerFolders(root).Count > 0;
}
