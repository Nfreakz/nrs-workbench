namespace NRS.Workbench.Platform.Linux;

public static class LinuxAppDataPaths
{
    private const string ProductFolder = "nrs-workbench";
    private const string PreviewFolder = "nrs-workbench-preview";

    public static string GetConfigDirectory(
        string? homeDirectory = null,
        string? xdgConfigHome = null,
        bool preview = false)
    {
        var baseDirectory = ResolveBase(
            xdgConfigHome,
            "XDG_CONFIG_HOME",
            homeDirectory,
            ".config");

        return Path.Combine(baseDirectory, preview ? PreviewFolder : ProductFolder);
    }

    public static string GetDataDirectory(
        string? homeDirectory = null,
        string? xdgDataHome = null,
        bool preview = false)
    {
        var baseDirectory = ResolveBase(
            xdgDataHome,
            "XDG_DATA_HOME",
            homeDirectory,
            Path.Combine(".local", "share"));

        return Path.Combine(baseDirectory, preview ? PreviewFolder : ProductFolder);
    }

    private static string ResolveBase(
        string? explicitXdg,
        string environmentVariable,
        string? explicitHome,
        string fallbackRelativePath)
    {
        var xdg = string.IsNullOrWhiteSpace(explicitXdg)
            ? Environment.GetEnvironmentVariable(environmentVariable)
            : explicitXdg;
        if (!string.IsNullOrWhiteSpace(xdg)) return Path.GetFullPath(xdg);

        var home = string.IsNullOrWhiteSpace(explicitHome)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : explicitHome;
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("A Linux home directory could not be resolved.");

        return Path.GetFullPath(Path.Combine(home, fallbackRelativePath));
    }
}
