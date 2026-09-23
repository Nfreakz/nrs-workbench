using System.Runtime.InteropServices;

namespace NRS.Workbench.App.Services;

// Samples the host PC, including jobs from all runners and other applications.
public sealed class SystemResourceService
{
    private ulong _lastIdle;
    private ulong _lastTotal;

    public SystemResourceSnapshot Read()
    {
        double? cpu = null;
        if (GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            var idle = idleTime.Value;
            var total = kernelTime.Value + userTime.Value;
            if (_lastTotal != 0 && total > _lastTotal && idle >= _lastIdle)
            {
                var elapsed = total - _lastTotal;
                cpu = Math.Clamp(100d * (1d - (double)(idle - _lastIdle) / elapsed), 0, 100);
            }
            _lastIdle = idle;
            _lastTotal = total;
        }

        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        ulong? usedMemory = null;
        ulong? totalMemory = null;
        if (GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0)
        {
            totalMemory = memory.TotalPhysical;
            usedMemory = memory.TotalPhysical - Math.Min(memory.AvailablePhysical, memory.TotalPhysical);
        }

        long? usedDisk = null;
        long? totalDisk = null;
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.TotalSize > 0)
                {
                    totalDisk = drive.TotalSize;
                    usedDisk = drive.TotalSize - drive.TotalFreeSpace;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Keep the CPU and RAM figures when the system drive is unavailable.
        }

        return new SystemResourceSnapshot(cpu, Environment.ProcessorCount, usedMemory, totalMemory, usedDisk, totalDisk);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out SystemTime idle, out SystemTime kernel, out SystemTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}

public sealed record SystemResourceSnapshot(
    double? CpuPercent, int LogicalProcessors,
    ulong? UsedMemoryBytes, ulong? TotalMemoryBytes,
    long? UsedDiskBytes, long? TotalDiskBytes);
