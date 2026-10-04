namespace NRS.Workbench.App.Services;

public static class AppDataPaths
{
    private const string PreviewProfile = "preview";
    private static readonly string BaseDirectoryAtStartup = Path.GetFullPath(AppContext.BaseDirectory);
    private static readonly bool PortableAtStartup =
        !IsPreviewEnvironment() && File.Exists(Path.Combine(BaseDirectoryAtStartup, "NRSWorkbench.portable"));

    public static bool IsPreview => IsPreviewEnvironment();
    public static bool IsPortable => !IsPreview && PortableAtStartup;
    public static string ApplicationDirectory => BaseDirectoryAtStartup;
    public static string PortableMarkerPath => Path.Combine(BaseDirectoryAtStartup, "NRSWorkbench.portable");

    public static string PortableVolumeRoot =>
        Path.GetPathRoot(BaseDirectoryAtStartup)
        ?? throw new InvalidOperationException("Could not determine the NRS Workbench volume root.");

    public static string PortableDataDirectory => Path.Combine(BaseDirectoryAtStartup, "Data");

    private static bool IsPreviewEnvironment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("NRS_WORKBENCH_PROFILE"),
            PreviewProfile,
            StringComparison.OrdinalIgnoreCase);

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
