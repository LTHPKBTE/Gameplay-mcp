using GameMcp.Input;
using GameMcp.Windowing;

namespace GameMcp.Server.Services;

/// <summary>
/// 会话状态：当前绑定窗口与坐标换算系数。
/// <see cref="ScaleX"/> / <see cref="ScaleY"/> 目前恒为 1，对外坐标等同于客户区物理像素。
/// </summary>
public sealed class SessionService(IWindowService windows, IInputService input)
{
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

        input.Attach(nint.Zero);
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
}
