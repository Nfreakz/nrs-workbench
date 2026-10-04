namespace NRS.Workbench.App.Services;

public static class AppDataPaths
{
    private const string PreviewProfile = "preview";
    private static readonly string BaseDirectoryAtStartup = Path.GetFullPath(AppContext.BaseDirectory);
    private static readonly bool PreviewAtStartup =
        string.Equals(
            Environment.GetEnvironmentVariable("NRS_WORKBENCH_PROFILE"),
            PreviewProfile,
            StringComparison.OrdinalIgnoreCase);
    private static readonly bool PortableAtStartup =
        !PreviewAtStartup && File.Exists(Path.Combine(BaseDirectoryAtStartup, "NRSWorkbench.portable"));

    public static bool IsPreview => PreviewAtStartup;
    public static bool IsPortable => PortableAtStartup;
    public static string ApplicationDirectory => BaseDirectoryAtStartup;
    public static string PortableMarkerPath => Path.Combine(BaseDirectoryAtStartup, "NRSWorkbench.portable");

    public static string PortableVolumeRoot =>
        Path.GetPathRoot(BaseDirectoryAtStartup)
        ?? throw new InvalidOperationException("Could not determine the NRS Workbench volume root.");

    public static string PortableDataDirectory => Path.Combine(BaseDirectoryAtStartup, "Data");

    public static string SettingsDirectory
    {
        get
        {
            if (IsPreview)
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "NRSWorkbenchPreview");
            }

            if (IsPortable) return PortableDataDirectory;

            var normalLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(normalLocalAppData, "NRSWorkbench");
        }
    }
}
