using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace GameMcp.Elevation;

/// <summary>
/// 提权子进程这一侧：把自己从 UAC 带来的新控制台里摘出来，再把标准句柄换到中继管道上。
/// 之后 <see cref="Console"/>、子进程继承、原生输出全都走管道，父进程负责转交给 MCP 客户端。
/// </summary>
internal static class ChildRelay
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    // 句柄归这两个流所有，必须留着，否则终结器会把管道关掉
    private static FileStream? _down;
    private static FileStream? _up;
    private static FileStream? _err;

    public static void Attach(ElevationOptions options)
    {
        var token = options.RelayToken!;
        var names = PipeNames.For(token);

        // runas 会把控制台应用放进一个新控制台，先藏再拆，中继期间不要多一个黑框
        var console = NativeMethods.GetConsoleWindow();
        if (console != nint.Zero)
        {
            NativeMethods.ShowWindow(console, NativeMethods.SwHide);
        }

        NativeMethods.FreeConsole();

        try
        {
            _down = PipeClient.Connect(names.In, ConnectTimeout);
            _up = PipeClient.Connect(names.Out, ConnectTimeout);
            _err = PipeClient.Connect(names.Err, ConnectTimeout);
        }
        catch (Exception)
        {
            // 这时还没有可用的 stderr，只能靠退出码告诉父进程
            Environment.Exit(ElevationExitCodes.RelayAttachFailed);
            return;
        }

        NativeMethods.SetStdHandle(NativeMethods.StdInputHandle, _down.SafeFileHandle.DangerousGetHandle());
        NativeMethods.SetStdHandle(NativeMethods.StdOutputHandle, _up.SafeFileHandle.DangerousGetHandle());
        NativeMethods.SetStdHandle(NativeMethods.StdErrorHandle, _err.SafeFileHandle.DangerousGetHandle());

        WatchParent(options.ParentProcessId);
    }

    /// <summary>父进程一死就跟着退出。</summary>
    private static void WatchParent(int parentProcessId)
    {
        if (parentProcessId <= 0)
        {
            return;
        }

        var parent = NativeMethods.OpenProcess(NativeMethods.Synchronize, false, parentProcessId);
        if (parent == nint.Zero)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            NativeMethods.WaitForSingleObject(parent, NativeMethods.Infinite);
            Environment.Exit(0);
        })
        {
            IsBackground = true,
            Name = "GameMcp.Elevation.WatchParent",
        };

        thread.Start();
    }
}
