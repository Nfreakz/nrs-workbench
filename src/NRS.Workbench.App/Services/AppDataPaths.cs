namespace NRS.Workbench.App.Services;

public static class AppDataPaths
{
    private const string PreviewProfile = "preview";

    public static bool IsPreview =>
        string.Equals(
            Environment.GetEnvironmentVariable("NRS_WORKBENCH_PROFILE"),
            PreviewProfile,
            StringComparison.OrdinalIgnoreCase);

    public static string SettingsDirectory
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, IsPreview ? "NRSWorkbenchPreview" : "NRSWorkbench");
        }
    }
}
