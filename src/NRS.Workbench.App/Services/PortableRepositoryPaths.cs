using NRS.Workbench.Core.Models;

namespace NRS.Workbench.App.Services;

// Conservative and testable drive-letter-only migration for existing Git working trees.
public static class PortableRepositoryPaths
{
    public static int Repair(RunnerSettings settings, IReadOnlyList<string>? destinationRoots = null, Func<string, string?>? sourceRootResolver = null)
    {
        var driveRoots = destinationRoots?.ToList() ?? FindLocalDriveRoots();

        var repaired = 0;
        for (var index = 0; index < settings.RepositoryPaths.Count; index++)
        {
            var originalPath = settings.RepositoryPaths[index];
            if (!Path.IsPathFullyQualified(originalPath) || Directory.Exists(originalPath)) continue;

            string? sourceRoot;
            try
            {
                sourceRoot = sourceRootResolver is null ? Path.GetPathRoot(Path.GetFullPath(originalPath)) : sourceRootResolver(originalPath);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(sourceRoot)) continue;

            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(sourceRoot, originalPath);
            }
            catch
            {
                continue;
            }

            if (Path.IsPathRooted(relativePath) ||
                relativePath.Equals("..", StringComparison.Ordinal) ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            var matches = new List<string>();
            foreach (var driveRoot in driveRoots)
            {
                if (string.Equals(driveRoot, sourceRoot, StringComparison.OrdinalIgnoreCase)) continue;

                string candidate;
                try
                {
                    candidate = Path.GetFullPath(Path.Combine(driveRoot, relativePath));
                }
                catch
                {
                    continue;
                }

                if (!Directory.Exists(candidate)) continue;
                var gitMarker = Path.Combine(candidate, ".git");
                if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker)) continue;

                if (!matches.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    matches.Add(candidate);
            }

            if (matches.Count != 1) continue;
            settings.RepositoryPaths[index] = matches[0];
            repaired++;
        }

        settings.RepositoryPaths = settings.RepositoryPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return repaired;
    }


    public static bool RepairRunnerRoots(RunnerSettings imported, IReadOnlyList<string> detectedRoots)
    {
        if (imported.RunnerRoots.Count == 0 ||
            imported.RunnerRoots.Any(Directory.Exists) ||
            detectedRoots.Count == 0)
            return false;

        imported.RunnerRoots = detectedRoots
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return imported.RunnerRoots.Count > 0;
    }

    public static int PreserveExisting(RunnerSettings imported, IEnumerable<string> existingPaths)
    {
        var seen = new HashSet<string>(imported.RepositoryPaths, StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var path in existingPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var trimmed = path.Trim();
            if (!seen.Add(trimmed)) continue;
            imported.RepositoryPaths.Add(trimmed);
            count++;
        }
        return count;
    }

    private static List<string> FindLocalDriveRoots()
    {
        var driveRoots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                driveRoots.Add(drive.RootDirectory.FullName);
            }
            catch
            {
                // A drive can disappear while the import dialog is open. Skip it.
            }
        }

        return driveRoots;
    }
}
