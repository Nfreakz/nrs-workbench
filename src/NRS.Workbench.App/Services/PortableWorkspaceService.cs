using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed record PortableWorkspaceActivationResult(
    string DataDirectory,
    string VolumeRoot,
    int RecordedRunners);

public sealed class PortableWorkspaceService
{
    public PortableWorkspaceActivationResult ActivateCurrentCopy(RunnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (AppDataPaths.IsPreview)
            throw new InvalidOperationException("Portable mode cannot be activated from the PREVIEW profile.");
        if (AppDataPaths.IsPortable)
            throw new InvalidOperationException("This NRS Workbench copy is already running in portable mode.");

        EnsureApplicationDirectoryWritable();

        Directory.CreateDirectory(AppDataPaths.PortableDataDirectory);
        var portableSettings = new SettingsService(
            AppDataPaths.PortableDataDirectory,
            AppDataPaths.PortableVolumeRoot);
        portableSettings.Save(settings);

        File.WriteAllText(
            AppDataPaths.PortableMarkerPath,
            "NRS Workbench portable workspace\r\nSchema=1\r\n");

        var store = new PortableWorkspaceManifestStore(
            AppDataPaths.PortableDataDirectory,
            AppDataPaths.PortableVolumeRoot);

        var recorded = 0;
        foreach (var folder in portableSettings.DetectRunnerFolders(settings.RunnerRoots))
        {
            if (!IsOnPortableVolume(folder)) continue;
            if (File.Exists(Path.Combine(folder, ".service"))) continue;
            var registration = PortableRunnerMetadataReader.Read(folder);
            if (registration is null) continue;
            store.RecordPrepared(folder, registration.GitHubUrl, registration.AgentName);
            recorded++;
        }

        return new PortableWorkspaceActivationResult(
            AppDataPaths.PortableDataDirectory,
            AppDataPaths.PortableVolumeRoot,
            recorded);
    }

    private static bool IsOnPortableVolume(string path)
    {
        var encoded = PortablePathCodec.Encode(path, AppDataPaths.PortableVolumeRoot);
        return PortablePathCodec.IsPortableToken(encoded);
    }

    private static void EnsureApplicationDirectoryWritable()
    {
        var testPath = Path.Combine(
            AppDataPaths.ApplicationDirectory,
            ".nrs-portable-write-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(testPath, "test");
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(
                "The NRS Workbench folder is not writable. Portable mode needs to create its marker and Data folder beside the application.",
                ex);
        }
        finally
        {
            try { if (File.Exists(testPath)) File.Delete(testPath); } catch { }
        }
    }
}
