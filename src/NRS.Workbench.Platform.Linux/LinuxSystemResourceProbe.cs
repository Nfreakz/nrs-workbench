namespace NRS.Workbench.Platform.Linux;

public sealed record LinuxCpuTimes(ulong Idle, ulong Total);

public sealed record LinuxMemorySnapshot(ulong UsedBytes, ulong TotalBytes);

public sealed record LinuxSystemResourceSnapshot(
    double? CpuPercent,
    int LogicalProcessors,
    ulong? UsedMemoryBytes,
    ulong? TotalMemoryBytes,
    long? UsedDiskBytes,
    long? TotalDiskBytes);

public sealed class LinuxSystemResourceProbe
{
    private LinuxCpuTimes? _previousCpu;

    public LinuxSystemResourceSnapshot Read()
    {
        double? cpu = null;
        try
        {
            var current = ParseCpuTimes(File.ReadAllText("/proc/stat"));
            if (_previousCpu is not null)
                cpu = CalculateCpuPercent(_previousCpu, current);
            _previousCpu = current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Return the other resource figures when /proc CPU data is unavailable.
        }

        ulong? usedMemory = null;
        ulong? totalMemory = null;
        try
        {
            var memory = ParseMemory(File.ReadAllText("/proc/meminfo"));
            usedMemory = memory.UsedBytes;
            totalMemory = memory.TotalBytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Keep CPU/disk figures when /proc memory data is unavailable.
        }

        long? usedDisk = null;
        long? totalDisk = null;
        try
        {
            var root = new DriveInfo("/");
            if (root.IsReady && root.TotalSize > 0)
            {
                totalDisk = root.TotalSize;
                usedDisk = root.TotalSize - root.TotalFreeSpace;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Keep CPU/RAM figures when the root filesystem is unavailable.
        }

        return new LinuxSystemResourceSnapshot(
            cpu,
            Environment.ProcessorCount,
            usedMemory,
            totalMemory,
            usedDisk,
            totalDisk);
    }

    public static LinuxCpuTimes ParseCpuTimes(string procStat)
    {
        var line = procStat
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x => x.StartsWith("cpu ", StringComparison.Ordinal));

        if (line is null) throw new InvalidDataException("/proc/stat does not contain aggregate CPU data.");

        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 5) throw new InvalidDataException("Aggregate CPU data is incomplete.");

        var values = fields.Skip(1).Take(8).Select(ParseUInt64).ToArray();
        var total = values.Aggregate(0UL, (sum, value) => checked(sum + value));
        var idle = values.ElementAtOrDefault(3) + values.ElementAtOrDefault(4);

        return new LinuxCpuTimes(idle, total);
    }

    public static double? CalculateCpuPercent(LinuxCpuTimes previous, LinuxCpuTimes current)
    {
        if (current.Total <= previous.Total || current.Idle < previous.Idle) return null;

        var elapsed = current.Total - previous.Total;
        if (elapsed == 0) return null;

        var idleElapsed = current.Idle - previous.Idle;
        return Math.Clamp(100d * (1d - (double)idleElapsed / elapsed), 0d, 100d);
    }

    public static LinuxMemorySnapshot ParseMemory(string memInfo)
    {
        var values = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var line in memInfo.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var key = line[..colon];
            var raw = line[(colon + 1)..].Trim();
            var first = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first is null || !ulong.TryParse(first, out var kib)) continue;
            values[key] = checked(kib * 1024UL);
        }

        if (!values.TryGetValue("MemTotal", out var total) || total == 0)
            throw new InvalidDataException("/proc/meminfo does not contain MemTotal.");

        ulong available;
        if (!values.TryGetValue("MemAvailable", out available))
        {
            values.TryGetValue("MemFree", out var free);
            values.TryGetValue("Buffers", out var buffers);
            values.TryGetValue("Cached", out var cached);
            available = Math.Min(total, free + buffers + cached);
        }

        available = Math.Min(available, total);
        return new LinuxMemorySnapshot(total - available, total);
    }

    private static ulong ParseUInt64(string value) =>
        ulong.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Invalid /proc numeric value: '{value}'.");
}
