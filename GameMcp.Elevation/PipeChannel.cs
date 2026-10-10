using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GameMcp.Elevation;

/// <summary>
/// 命名管道的服务端端点。先建好管道再拉起提权子进程，随后 <see cref="Connect"/> 等它接入。
/// DACL 收紧到当前用户 SID 并拒绝远程连接，避免本机其他账户或远端劫持这条通道。
/// </summary>
internal sealed class PipeServer : IDisposable
{
    private readonly string _name;
    private nint _handle;

    public PipeServer(string name)
    {
        _name = name;

        var sid = ElevationState.CurrentUserSid
            ?? throw new InvalidOperationException("无法取得当前用户 SID，无法为管道设置访问控制");

        // D:P = 不被继承的 DACL；只放行当前用户（提权与否都是同一个用户 SID）
        var sddl = $"D:P(A;;GA;;;{sid})";
        if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl,
                NativeMethods.SddlRevision1,
                out var descriptor,
                out _))
        {
            throw new InvalidOperationException(
                $"无法构造管道安全描述符（错误 {Marshal.GetLastWin32Error()}）");
        }

        nint pipe;
        int error;
        try
        {
            var attributes = new NativeMethods.SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                SecurityDescriptor = descriptor,
                InheritHandle = 0,
            };

            pipe = NativeMethods.CreateNamedPipeW(
                name,
                NativeMethods.PipeAccessDuplex,
                NativeMethods.PipeTypeByte | NativeMethods.PipeReadModeByte | NativeMethods.PipeWait
                | NativeMethods.PipeRejectRemoteClients,
                1,
                64 * 1024,
                64 * 1024,
                0,
                ref attributes);

            // 必须在这里取：finally 里的 LocalFree 会覆盖 last error
            error = Marshal.GetLastWin32Error();
        }
        finally
        {
            NativeMethods.LocalFree(descriptor);
        }

        if (pipe == NativeMethods.InvalidHandleValue || pipe == nint.Zero)
        {
            throw new InvalidOperationException($"创建命名管道 {name} 失败（错误 {error}）");
        }

        _handle = pipe;
    }

    /// <summary>等客户端接入，成功则把句柄交给返回的流，本对象不再持有它。</summary>
    public FileStream Connect(TimeSpan timeout)
    {
        var connected = new ManualResetEventSlim(false);
        var failed = false;

        var thread = new Thread(() =>
        {
            // 客户端可能已经先连上了，此时返回 ERROR_PIPE_CONNECTED，同样算连上
            if (!NativeMethods.ConnectNamedPipe(_handle, 0)
                && Marshal.GetLastWin32Error() != NativeMethods.ErrorPipeConnected)
            {
                failed = true;
            }

            connected.Set();
        })
        {
            IsBackground = true,
            Name = "GameMcp.Elevation.Connect",
        };

        thread.Start();

        if (!connected.Wait(timeout) || failed)
        {
            NativeMethods.DisconnectNamedPipe(_handle);
            throw new TimeoutException($"等待 {_name} 的连接失败");
        }

        var handle = _handle;
        _handle = nint.Zero;
        return new FileStream(new SafeFileHandle(handle, ownsHandle: true), FileAccess.ReadWrite, 1, false);
    }

    public void Dispose()
    {
        if (_handle == nint.Zero)
        {
            return;
        }

        NativeMethods.DisconnectNamedPipe(_handle);
        NativeMethods.CloseHandle(_handle);
        _handle = nint.Zero;
    }
}

internal static class PipeClient
{
    public static FileStream Connect(string name, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;

        while (true)
        {
            var handle = NativeMethods.CreateFileW(
                name,
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                0,
                0,
                NativeMethods.OpenExisting,
                0,
                0);

            if (handle != NativeMethods.InvalidHandleValue && handle != nint.Zero)
            {
                return new FileStream(new SafeFileHandle(handle, ownsHandle: true), FileAccess.ReadWrite, 1, false);
            }

            if (Environment.TickCount64 >= deadline)
            {
                throw new InvalidOperationException(
                    $"连接命名管道 {name} 失败（错误 {Marshal.GetLastWin32Error()}）");
            }

            Thread.Sleep(10);
        }
    }
}
