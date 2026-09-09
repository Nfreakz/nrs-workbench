using System.Text.Json;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public SettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        SettingsDirectory = Path.Combine(localAppData, "NRSWorkbench");
        SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    }

    public RunnerSettings Load()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            TryMigrateLegacySettings();
            if (!File.Exists(SettingsPath))
            {
                var defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<RunnerSettings>(json) ?? CreateDefaults();
            Normalize(settings);

            // v0.4.0-v0.4.4 could persist the build-output directory as the only
            // search root. Recover automatically when the configured roots do not
            // contain any runner but a top-level runner installation can be found.
            if (!AnyConfiguredRootContainsRunner(settings))
            {
                var detected = DetectRunnerRoots(settings.FolderPattern);
                if (detected.Count > 0)
                {
                    settings.RunnerRoots = detected.ToList();
                    Save(settings);
                    AppLogger.Info($"Recovered runner roots automatically: {string.Join(", ", detected)}");
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load settings", ex);
            return CreateDefaults();
        }
    }

    public void Save(RunnerSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private void TryMigrateLegacySettings()
    {
        if (File.Exists(SettingsPath)) return;

        try
        {
            var legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RunnerManager",
                "settings.json");
            if (!File.Exists(legacyPath)) return;

            File.Copy(legacyPath, SettingsPath, overwrite: false);
            AppLogger.Info("Migrated legacy RunnerManager settings to NRSWorkbench.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not migrate legacy RunnerManager settings", ex);
        }
    }

    public IReadOnlyList<string> DetectRunnerRoots(string? folderPattern = null)
    {
        var pattern = string.IsNullOrWhiteSpace(folderPattern) ? "actions-runner*" : folderPattern.Trim();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First inspect every ancestor of the executable, INCLUDING the drive root.
        // This covers both development builds and a portable app placed near runners.
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (ContainsRunnerFolder(current.FullName, pattern))
                roots.Add(Path.GetFullPath(current.FullName));
        }

        // Then inspect the top level of all ready local/removable drives. This is a
        // shallow operation: we do not recursively crawl the user's disks.
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;

                var root = drive.RootDirectory.FullName;
                if (ContainsRunnerFolder(root, pattern))
                    roots.Add(Path.GetFullPath(root));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Could not inspect drive '{drive.Name}' for runner roots", ex);
            }
        }

        return roots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private RunnerSettings CreateDefaults()
    {
        var detected = DetectRunnerRoots("actions-runner*");
        return new RunnerSettings
        {
            RunnerRoots = detected.Count > 0 ? detected.ToList() : [FindFallbackRoot()],
            FolderPattern = "actions-runner*",
            RefreshIntervalSeconds = 5,
            ConfirmStopBusy = true,
            KeepInTray = true,
            NotificationsEnabled = true,
            NotifyJobStarted = true,
            NotifyJobCompleted = true,
            NotifyRunnerIssues = true,
            NotifyOnlyWhenHidden = true,
            RepositoryPaths = []
        };
    }

    private static string FindFallbackRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current.Parent is not null) current = current.Parent;
        return current.FullName;
    }

    private static bool ContainsRunnerFolder(string root, string pattern)
    {
        try
        {
            if (!Directory.Exists(root)) return false;
            return Directory.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly)
                .Any(IsRunnerFolder);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRunnerFolder(string folder)
    {
        return File.Exists(Path.Combine(folder, ".runner")) ||
               File.Exists(Path.Combine(folder, "run.cmd"));
    }

    private static bool AnyConfiguredRootContainsRunner(RunnerSettings settings)
    {
        return settings.RunnerRoots.Any(root => ContainsRunnerFolder(root, settings.FolderPattern));
    }

    private static void Normalize(RunnerSettings settings)
    {
        settings.RunnerRoots ??= [];
        settings.RepositoryPaths ??= [];
        settings.RunnerRoots = settings.RunnerRoots
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.RepositoryPaths = settings.RepositoryPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (string.IsNullOrWhiteSpace(settings.FolderPattern)) settings.FolderPattern = "actions-runner*";
        if (settings.RunnerRoots.Count == 0) settings.RunnerRoots.Add(FindFallbackRoot());
        settings.RefreshIntervalSeconds = Math.Clamp(settings.RefreshIntervalSeconds, 2, 300);
    }
}
