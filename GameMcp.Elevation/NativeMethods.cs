using System.Runtime.InteropServices;

namespace GameMcp.Elevation;

internal static class NativeMethods
{
    internal const int StdInputHandle = -10;
    internal const int StdOutputHandle = -11;
    internal const int StdErrorHandle = -12;

    internal const uint TokenQuery = 0x0008;
    internal const int TokenUserInfo = 1;
    internal const int TokenElevationInfo = 20;

    internal const uint PipeAccessDuplex = 0x00000003;
    internal const uint PipeTypeByte = 0x00000000;
    internal const uint PipeReadModeByte = 0x00000000;
    internal const uint PipeWait = 0x00000000;

    // 只允许本机连接。它属于 dwPipeMode 位组，放进 dwOpenMode 会被判 ERROR_INVALID_PARAMETER
    internal const uint PipeRejectRemoteClients = 0x00000008;

    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint OpenExisting = 3;
    internal const uint Synchronize = 0x00100000;
    internal const uint Infinite = 0xFFFFFFFF;

    internal const uint SddlRevision1 = 1;
    internal const int SwHide = 0;

    internal const int ErrorCancelled = 1223;
    internal const int ErrorElevationRequired = 740;
    internal const int ErrorPipeConnected = 535;

    internal static readonly nint InvalidHandleValue = -1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityAttributes
    {
        public uint Length;
        public nint SecurityDescriptor;
        public int InheritHandle;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool OpenProcessToken(nint process, uint access, out nint token);

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool GetTokenInformation(nint token, int kind, nint info, uint length, out uint returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetStdHandle(int which, nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateNamedPipeW(
        string name,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint outBufferSize,
        uint inBufferSize,
        uint timeout,
        ref SecurityAttributes attributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ConnectNamedPipe(nint pipe, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DisconnectNamedPipe(nint pipe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateFileW(
        string name,
        uint access,
        uint share,
        nint attributes,
        uint creation,
        uint flags,
        nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint LocalFree(nint memory);

    // GetConsoleWindow 在 kernel32 里（user32 没有这个导出）
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ShowWindow(nint window, int command);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string sddl,
        uint revision,
        out nint descriptor,
        out uint size);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool ConvertSidToStringSidW(nint sid, out nint text);
}
