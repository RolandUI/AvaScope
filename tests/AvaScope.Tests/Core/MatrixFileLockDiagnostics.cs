using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AvaScope.Tests.Core;

internal static class MatrixFileLockDiagnostics
{
    // Failure-only observation of fixture files. A user is not necessarily the cause of
    // the original lock: it may have exited or released the file before this snapshot.
    public static string Capture(params string[] paths)
    {
        if (!OperatingSystem.IsWindows())
        {
            return "fileUsers=unavailable, reason=platform";
        }

        var elapsed = Stopwatch.StartNew();
        var details = new StringBuilder($"fileUsersObservedAt={DateTimeOffset.UtcNow:O}, observerPid={Environment.ProcessId}");
        try
        {
            if (paths.Length is 0 or > 16)
            {
                return "fileUsers=unavailable, reason=file_count";
            }

            details.Append($", paths=[{string.Join(" | ", paths)}]");
            var result = RmStartSession(out var session, 0, new StringBuilder(33));
            details.Append($", start={result}");
            if (result == 0)
            {
                try
                {
                    result = RmRegisterResources(session, (uint)paths.Length, paths, 0, IntPtr.Zero, 0, IntPtr.Zero);
                    details.Append($", register={result}");
                    if (result == 0)
                    {
                        // One bounded buffer/query; an overflow remains unavailable, never an empty success.
                        var users = new ProcessInfo[16];
                        var count = (uint)users.Length;
                        result = RmGetList(session, out var needed, ref count, users, out _);
                        details.Append($", list={result}, needed={needed}");
                        if (result == 0)
                        {
                            details.Append($", users={count}");
                            for (var index = 0; index < count; index++)
                            {
                                var process = users[index].Process;
                                var startFileTime = ((long)process.Started.dwHighDateTime << 32) | (uint)process.Started.dwLowDateTime;
                                details.Append($", [pid={process.Id}, startFileTime={startFileTime}]");
                            }
                        }
                    }
                }
                finally
                {
                    details.Append($", end={RmEndSession(session)}");
                }
            }
        }
        catch (Exception exception)
        {
            // Diagnostics must not replace the original image-access or cleanup exception.
            details.Append($", unavailable={exception.GetType().Name}, hresult=0x{exception.HResult:X8}");
        }

        details.Append($", elapsedMs={elapsed.ElapsedMilliseconds}");
        return details.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UniqueProcess
    {
        public uint Id;
        public System.Runtime.InteropServices.ComTypes.FILETIME Started;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceName;
        public uint AppType;
        public uint AppStatus;
        public uint SessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }

    // Only register/query/end APIs are imported; no process control or file contents.
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern uint RmStartSession(out uint session, uint flags, StringBuilder key);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern uint RmRegisterResources(uint session, uint count, string[] paths,
        uint appCount, IntPtr apps, uint serviceCount, IntPtr services);

    [DllImport("rstrtmgr.dll")]
    private static extern uint RmGetList(uint session, out uint needed, ref uint count,
        [In, Out] ProcessInfo[] users, out uint reasons);

    [DllImport("rstrtmgr.dll")]
    private static extern uint RmEndSession(uint session);
}
