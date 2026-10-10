using System.Diagnostics;
using GameMcp.Capture;
using GameMcp.Input;
using GameMcp.Windowing;

namespace GameMcp.Server.Services;

/// <summary>
/// 会话状态：当前绑定窗口、采集会话与坐标换算系数。
/// <see cref="ScaleX"/> / <see cref="ScaleY"/> 目前恒为 1，对外坐标等同于客户区物理像素。
/// </summary>
public sealed class SessionService(IWindowService windows, IInputService input, ICaptureService capture)
{
    /// <summary>等首帧的上限，与 <c>GameMcp.Tools capture</c> 的 <c>--first-timeout</c> 默认值一致。</summary>
    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan FramePollInterval = TimeSpan.FromMilliseconds(100);

    private readonly object _gate = new();

    private WindowInfo? _target;

    public WindowInfo? Target
    {
        get
        {
            lock (_gate)
            {
                return _target;
            }
        }
    }

    public nint Handle
    {
        get
        {
            lock (_gate)
            {
                return _target?.Handle ?? nint.Zero;
            }
        }
    }

    /// <summary>画布宽 / 客户区物理像素宽。</summary>
    public double ScaleX { get; private set; } = 1;

    /// <summary>画布高 / 客户区物理像素高。</summary>
    public double ScaleY { get; private set; } = 1;

    public bool HasTarget
    {
        get
        {
            lock (_gate)
            {
                return _target is not null;
            }
        }
    }

    public WindowInfo Attach(nint handle)
    {
        if (handle == nint.Zero)
        {
            throw new ToolException("invalid_handle", "句柄为空", "先用 list_windows 取句柄");
        }

        var info = windows.Describe(handle)
            ?? throw new ToolException("window_not_found", $"找不到窗口 0x{handle:X}", "先用 list_windows 确认句柄");

        lock (_gate)
        {
            _target = info;
            ScaleX = 1;
            ScaleY = 1;
        }

        input.Attach(handle);

        // 最小化或被遮蔽的窗口照样允许绑定，取帧留到 get_state 时再报错
        TryStartCapture(handle);
        return info;
    }

    public void Detach()
    {
        lock (_gate)
        {
            _target = null;
            ScaleX = 1;
            ScaleY = 1;
        }

        capture.Detach();
        input.Attach(nint.Zero);
    }

    /// <summary>取一帧。窗口不可采集时抛 <see cref="ToolException"/>，不返回旧帧。</summary>
    public async Task<CaptureResult> CaptureAsync(CancellationToken cancellationToken)
    {
        var handle = Handle;
        if (handle == nint.Zero)
        {
            throw new ToolException("no_target_window", "尚未绑定目标窗口", "先调用 attach_window");
        }

        EnsureCapture(handle);

        // 绑定后第一帧不一定立刻可取（CLI 的 --first-timeout 就是为这个存在的），单次取不到不算失败
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            if (capture.Capture() is { } frame)
            {
                return frame;
            }

            if (Stopwatch.GetElapsedTime(started) >= FirstFrameTimeout)
            {
                throw new ToolException(
                    "capture_empty",
                    $"等待首帧超时（{FirstFrameTimeout.TotalSeconds:0}s），未取到图像",
                    "确认目标窗口可见、未最小化；也可能需要提高权限");
            }

            await Task.Delay(FramePollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public WindowInfo RequireTarget()
    {
        lock (_gate)
        {
            return _target ?? throw new ToolException("no_target_window", "尚未绑定目标窗口", "先调用 attach_window");
        }
    }

    /// <summary>对外坐标（返回图像素）→ 客户区物理像素。</summary>
    public (int X, int Y) ToClient(int x, int y)
    {
        lock (_gate)
        {
            return ((int)Math.Round(x / ScaleX), (int)Math.Round(y / ScaleY));
        }
    }

    private void TryStartCapture(nint handle)
    {
        try
        {
            capture.Attach(handle);
        }
        catch (InvalidOperationException)
        {
            // 这里不报错：窗口状态可能在 get_state 之前恢复
        }
    }

    private void EnsureCapture(nint handle)
    {
        if (capture.IsAttached && capture.TargetHandle == handle)
        {
            return;
        }

        try
        {
            capture.Attach(handle);
        }
        catch (InvalidOperationException e)
        {
            throw new ToolException("capture_unavailable", e.Message, "还原窗口后重试");
        }
    }
}
