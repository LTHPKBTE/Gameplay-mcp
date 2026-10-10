using System.ComponentModel;
using System.Diagnostics;

namespace GameMcp.Elevation;

internal enum LaunchResult
{
    Started,
    Declined,
    Failed,
    Abandoned,
}

/// <summary>
/// 后台线程上发起 <c>runas</c>。<see cref="Process.Start(ProcessStartInfo)"/> 要等用户在 UAC 上作答才返回，
/// 占着主线程就会把调用方（MCP 客户端）挂死，所以主线程只等有限时间，超时就原地降级。
/// </summary>
internal sealed class ElevationLaunch
{
    private readonly ManualResetEventSlim _finished = new(false);
    private readonly object _gate = new();

    private Process? _process;
    private LaunchResult _result = LaunchResult.Failed;
    private bool _abandoned;

    public ElevationLaunch(ProcessStartInfo startInfo)
    {
        var thread = new Thread(() => Start(startInfo))
        {
            IsBackground = true,
            Name = "GameMcp.Elevation.Launch",
        };

        thread.Start();
    }

    public string? Error { get; private set; }

    public Process? Process
    {
        get
        {
            lock (_gate)
            {
                return _process;
            }
        }
    }

    public LaunchResult Wait(TimeSpan timeout)
        => _finished.Wait(timeout) ? _result : LaunchResult.Abandoned;

    /// <summary>放弃这次提权：用户在 UAC 上晚点「是」的话，进程一起来就杀掉。</summary>
    public void Abandon()
    {
        Process? process;
        lock (_gate)
        {
            _abandoned = true;
            process = _process;
        }

        if (process is not null)
        {
            Kill(process);
        }
    }

    private void Start(ProcessStartInfo startInfo)
    {
        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new Win32Exception(0, "runas 没有返回进程句柄");
        }
        catch (Win32Exception e)
        {
            var declined = e.NativeErrorCode == NativeMethods.ErrorCancelled;
            Error = declined
                ? "UAC 提示被取消"
                : $"提权进程启动失败（错误 {e.NativeErrorCode}：{e.Message}"
                  + (e.NativeErrorCode == NativeMethods.ErrorElevationRequired ? "，系统拒绝提权" : string.Empty)
                  + "）";
            _result = declined ? LaunchResult.Declined : LaunchResult.Failed;
            _finished.Set();
            return;
        }
        catch (Exception e) when (e is InvalidOperationException or PlatformNotSupportedException or FileNotFoundException)
        {
            Error = $"提权进程启动失败：{e.Message}";
            _result = LaunchResult.Failed;
            _finished.Set();
            return;
        }

        lock (_gate)
        {
            _process = process;
            if (_abandoned)
            {
                Kill(process);
                return;
            }
        }

        _result = LaunchResult.Started;
        _finished.Set();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}
