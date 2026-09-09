using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NRS.Workbench.App.Services;

public enum NativeServiceState
{
    Unknown = 0,
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7
}

public sealed class WindowsServiceController
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint ServiceControlStop = 0x00000001;
    private const int ScStatusProcessInfo = 0;

    public bool Exists(string serviceName)
    {
        IntPtr scm = IntPtr.Zero, service = IntPtr.Zero;
        try
        {
            scm = OpenSCManager(null, null, ScManagerConnect);
            if (scm == IntPtr.Zero) return false;
            service = OpenService(scm, serviceName, ServiceQueryStatus);
            return service != IntPtr.Zero;
        }
        finally
        {
            if (service != IntPtr.Zero) CloseServiceHandle(service);
            if (scm != IntPtr.Zero) CloseServiceHandle(scm);
        }
    }

    public NativeServiceState GetState(string serviceName)
    {
        using var handles = Open(serviceName, ServiceQueryStatus);
        return Query(handles.Service);
    }

    public void Start(string serviceName)
    {
        using var handles = Open(serviceName, ServiceStart | ServiceQueryStatus);
        var state = Query(handles.Service);
        if (state == NativeServiceState.Running) return;
        if (!StartService(handles.Service, 0, null))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1056) throw new Win32Exception(error);
        }
        WaitFor(handles.Service, NativeServiceState.Running, TimeSpan.FromSeconds(20));
    }

    public void Stop(string serviceName)
    {
        using var handles = Open(serviceName, ServiceStop | ServiceQueryStatus);
        var state = Query(handles.Service);
        if (state == NativeServiceState.Stopped) return;
        if (!ControlService(handles.Service, ServiceControlStop, out _))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1062) throw new Win32Exception(error);
        }
        WaitFor(handles.Service, NativeServiceState.Stopped, TimeSpan.FromSeconds(20));
    }

    private static HandlePair Open(string serviceName, uint access)
    {
        var scm = OpenSCManager(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var service = OpenService(scm, serviceName, access);
        if (service == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            CloseServiceHandle(scm);
            throw new Win32Exception(error);
        }
        return new HandlePair(scm, service);
    }

    private static NativeServiceState Query(IntPtr service)
    {
        var size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!QueryServiceStatusEx(service, ScStatusProcessInfo, buffer, size, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var status = Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buffer);
            return (NativeServiceState)status.dwCurrentState;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void WaitFor(IntPtr service, NativeServiceState target, TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            if (Query(service) == target) return;
            Thread.Sleep(250);
        }
        throw new TimeoutException($"The Windows service did not reach state {target} within {timeout.TotalSeconds:N0}s.");
    }

    private sealed class HandlePair : IDisposable
    {
        public IntPtr Scm { get; }
        public IntPtr Service { get; }
        public HandlePair(IntPtr scm, IntPtr service) { Scm = scm; Service = service; }
        public void Dispose()
        {
            if (Service != IntPtr.Zero) CloseServiceHandle(Service);
            if (Scm != IntPtr.Zero) CloseServiceHandle(Scm);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr scm, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartService(IntPtr service, int numServiceArgs, string[]? serviceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(IntPtr service, uint control, out SERVICE_STATUS serviceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int infoLevel, IntPtr buffer, int bufferSize, out int bytesNeeded);
}
