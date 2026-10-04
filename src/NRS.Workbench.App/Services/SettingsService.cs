using System.Text.Json;
using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

public sealed class SettingsService
{
    private const int PortableSettingsSchemaVersion = 1;
    private const long MaxPortableSettingsBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string? _portableRoot;

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }
    public bool UsesPortableStorage => !string.IsNullOrWhiteSpace(_portableRoot);

    public SettingsService(string? settingsDirectory = null, string? portableRoot = null)
    {
        SettingsDirectory = settingsDirectory ?? AppDataPaths.SettingsDirectory;
        _portableRoot = portableRoot ??
            (settingsDirectory is null && AppDataPaths.IsPortable ? AppDataPaths.PortableVolumeRoot : null);
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
            var settings = JsonSerializer.Deserialize<RunnerSettings>(json, JsonOptions) ?? CreateDefaults();
            DecodeWorkspacePaths(settings);
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

        var persisted = JsonSerializer.Deserialize<RunnerSettings>(
            JsonSerializer.Serialize(settings, JsonOptions),
            JsonOptions) ?? throw new InvalidOperationException("Could not prepare settings for persistence.");
        EncodeWorkspacePaths(persisted);

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(persisted, JsonOptions));
    }

    public void ExportPortableSettings(RunnerSettings settings, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));

        var portable = PortableSettingsData.FromSettings(settings);
        var envelope = new PortableSettingsEnvelope
        {
            SchemaVersion = PortableSettingsSchemaVersion,
            Product = "NRS Workbench",
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Settings = portable
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(destinationPath, JsonSerializer.Serialize(envelope, JsonOptions));
    }

    public RunnerSettings ReadPortableSettings(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A source path is required.", nameof(sourcePath));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The configuration file does not exist.", sourcePath);

        var info = new FileInfo(sourcePath);
        if (info.Length <= 0 || info.Length > MaxPortableSettingsBytes)
            throw new InvalidDataException("The configuration file is empty or unexpectedly large.");

        var json = File.ReadAllText(sourcePath);
        var envelope = JsonSerializer.Deserialize<PortableSettingsEnvelope>(json, JsonOptions)
            ?? throw new InvalidDataException("The configuration file is not valid NRS Workbench JSON.");

        if (!string.Equals(envelope.Product, "NRS Workbench", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file is not an NRS Workbench configuration export.");
        if (envelope.SchemaVersion != PortableSettingsSchemaVersion)
            throw new InvalidDataException($"Unsupported configuration schema version: {envelope.SchemaVersion}.");
        if (envelope.Settings is null)
            throw new InvalidDataException("The configuration export does not contain settings.");

        var settings = envelope.Settings.ToSettings();
        Normalize(settings);
        return settings;
    }

    private void TryMigrateLegacySettings()
    {
        if (File.Exists(SettingsPath) || AppDataPaths.IsPreview || UsesPortableStorage) return;

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
        _ = folderPattern; // Legacy portable-setting compatibility. Detection validates runner contents, not names.
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First inspect every ancestor of the executable, INCLUDING the drive root.
        // This covers both development builds and a portable app placed near runners.
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (RunnerFolderDiscovery.ContainsRunnerFolder(current.FullName))
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
                if (RunnerFolderDiscovery.ContainsRunnerFolder(root))
                    roots.Add(Path.GetFullPath(root));

                // Also inspect one directory level below the drive root. This finds
                // layouts such as C:\actions-github\<runner> without relying on a
                // folder-name prefix and without recursively crawling the drive.
                foreach (var candidateRoot in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    if (RunnerFolderDiscovery.ContainsRunnerFolder(candidateRoot))
                        roots.Add(Path.GetFullPath(candidateRoot));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Could not inspect drive '{drive.Name}' for runner roots", ex);
            }
        }

        return roots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> DetectRunnerFolders(IEnumerable<string> roots)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots ?? [])
            folders.UnionWith(RunnerFolderDiscovery.FindRunnerFolders(root));

        return folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
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

    private string FindFallbackRoot()
    {
        if (UsesPortableStorage) return Path.GetFullPath(_portableRoot!);

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current.Parent is not null) current = current.Parent;
        return current.FullName;
    }

    private void DecodeWorkspacePaths(RunnerSettings settings)
    {
        if (!UsesPortableStorage) return;
        settings.RunnerRoots = DecodePaths(settings.RunnerRoots);
        settings.RepositoryPaths = DecodePaths(settings.RepositoryPaths);
        settings.RunnerDisplayOrder = DecodePaths(settings.RunnerDisplayOrder);
        settings.RunnerQueueIncludedPaths = DecodePaths(settings.RunnerQueueIncludedPaths);
    }

    private void EncodeWorkspacePaths(RunnerSettings settings)
    {
        if (!UsesPortableStorage) return;
        settings.RunnerRoots = EncodePaths(settings.RunnerRoots);
        settings.RepositoryPaths = EncodePaths(settings.RepositoryPaths);
        settings.RunnerDisplayOrder = EncodePaths(settings.RunnerDisplayOrder);
        settings.RunnerQueueIncludedPaths = EncodePaths(settings.RunnerQueueIncludedPaths);
    }

    private List<string> DecodePaths(IEnumerable<string>? paths) =>
        (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => PortablePathCodec.Decode(path, _portableRoot!))
            .ToList();

    private List<string> EncodePaths(IEnumerable<string>? paths) =>
        (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => PortablePathCodec.Encode(path, _portableRoot!))
            .ToList();

    private static bool AnyConfiguredRootContainsRunner(RunnerSettings settings)
    {
        return settings.RunnerRoots.Any(RunnerFolderDiscovery.ContainsRunnerFolder);
    }

    private void Normalize(RunnerSettings settings)
    {
        settings.RunnerRoots ??= [];
        settings.RepositoryPaths ??= [];
        settings.RunnerDisplayOrder ??= [];
        settings.RunnerQueueIncludedPaths ??= [];
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
        settings.RunnerDisplayOrder = settings.RunnerDisplayOrder
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.RunnerQueueIncludedPaths = settings.RunnerQueueIncludedPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.RunnerSortMode = RunnerListOrganizer.NormalizeMode(settings.RunnerSortMode);
        settings.RunnerQueueLimit = Math.Clamp(settings.RunnerQueueLimit, 1, 2);
        settings.RunnerQueueCpuStartThreshold = Math.Clamp(settings.RunnerQueueCpuStartThreshold, 50, 100);
        settings.RunnerQueueMemoryStartThreshold = Math.Clamp(settings.RunnerQueueMemoryStartThreshold, 50, 100);
        if (string.IsNullOrWhiteSpace(settings.FolderPattern)) settings.FolderPattern = "actions-runner*";
        if (settings.RunnerRoots.Count == 0) settings.RunnerRoots.Add(FindFallbackRoot());
        settings.RefreshIntervalSeconds = Math.Clamp(settings.RefreshIntervalSeconds, 2, 300);
        settings.Language = settings.Language?.ToLowerInvariant() switch
        {
            "en" => "en",
            "ca" => "ca",
            _ => "es"
        };
    }

    private sealed class PortableSettingsEnvelope
    {
        public int SchemaVersion { get; set; }
        public string Product { get; set; } = string.Empty;
        public DateTimeOffset ExportedAtUtc { get; set; }
        public PortableSettingsData? Settings { get; set; }
    }

    // Explicit allow-list. Runner registration files, credentials, tokens and diagnostics
    // are never read or serialized by the configuration transfer feature.
    private sealed class PortableSettingsData
    {
        public List<string> RunnerRoots { get; set; } = [];
        public string FolderPattern { get; set; } = "actions-runner*";
        public int RefreshIntervalSeconds { get; set; } = 5;
        public bool ConfirmStopBusy { get; set; } = true;
        public bool KeepInTray { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool NotifyJobStarted { get; set; } = true;
        public bool NotifyJobCompleted { get; set; } = true;
        public bool NotifyRunnerIssues { get; set; } = true;
        public bool NotifyOnlyWhenHidden { get; set; } = true;
        public List<string> RepositoryPaths { get; set; } = [];
        public string Language { get; set; } = "es";
        public string RunnerSortMode { get; set; } = "manual";
        public List<string> RunnerDisplayOrder { get; set; } = [];
        public bool RunnerQueueEnabled { get; set; }
        public bool RunnerQueueUseAllRunners { get; set; } = true;
        public List<string> RunnerQueueIncludedPaths { get; set; } = [];
        public int RunnerQueueLimit { get; set; } = 2;
        public bool RunnerQueueResourceGuardEnabled { get; set; } = true;
        public int RunnerQueueCpuStartThreshold { get; set; } = 85;
        public int RunnerQueueMemoryStartThreshold { get; set; } = 90;

        public static PortableSettingsData FromSettings(RunnerSettings settings) => new()
        {
            RunnerRoots = settings.RunnerRoots?.ToList() ?? [],
            FolderPattern = settings.FolderPattern,
            RefreshIntervalSeconds = settings.RefreshIntervalSeconds,
            ConfirmStopBusy = settings.ConfirmStopBusy,
            KeepInTray = settings.KeepInTray,
            NotificationsEnabled = settings.NotificationsEnabled,
            NotifyJobStarted = settings.NotifyJobStarted,
            NotifyJobCompleted = settings.NotifyJobCompleted,
            NotifyRunnerIssues = settings.NotifyRunnerIssues,
            NotifyOnlyWhenHidden = settings.NotifyOnlyWhenHidden,
            RepositoryPaths = settings.RepositoryPaths?.ToList() ?? [],
            Language = settings.Language,
            RunnerSortMode = settings.RunnerSortMode,
            RunnerDisplayOrder = settings.RunnerDisplayOrder?.ToList() ?? [],
            RunnerQueueEnabled = settings.RunnerQueueEnabled,
            RunnerQueueUseAllRunners = settings.RunnerQueueUseAllRunners,
            RunnerQueueIncludedPaths = settings.RunnerQueueIncludedPaths?.ToList() ?? [],
            RunnerQueueLimit = settings.RunnerQueueLimit,
            RunnerQueueResourceGuardEnabled = settings.RunnerQueueResourceGuardEnabled,
            RunnerQueueCpuStartThreshold = settings.RunnerQueueCpuStartThreshold,
            RunnerQueueMemoryStartThreshold = settings.RunnerQueueMemoryStartThreshold
        };

        public RunnerSettings ToSettings() => new()
        {
            RunnerRoots = RunnerRoots?.ToList() ?? [],
            FolderPattern = FolderPattern,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            ConfirmStopBusy = ConfirmStopBusy,
            KeepInTray = KeepInTray,
            NotificationsEnabled = NotificationsEnabled,
            NotifyJobStarted = NotifyJobStarted,
            NotifyJobCompleted = NotifyJobCompleted,
            NotifyRunnerIssues = NotifyRunnerIssues,
            NotifyOnlyWhenHidden = NotifyOnlyWhenHidden,
            RepositoryPaths = RepositoryPaths?.ToList() ?? [],
            Language = Language,
            RunnerSortMode = RunnerSortMode,
            RunnerDisplayOrder = RunnerDisplayOrder?.ToList() ?? [],
            RunnerQueueEnabled = RunnerQueueEnabled,
            RunnerQueueUseAllRunners = RunnerQueueUseAllRunners,
            RunnerQueueIncludedPaths = RunnerQueueIncludedPaths?.ToList() ?? [],
            RunnerQueueLimit = RunnerQueueLimit,
            RunnerQueueResourceGuardEnabled = RunnerQueueResourceGuardEnabled,
            RunnerQueueCpuStartThreshold = RunnerQueueCpuStartThreshold,
            RunnerQueueMemoryStartThreshold = RunnerQueueMemoryStartThreshold
        };
    }
}
