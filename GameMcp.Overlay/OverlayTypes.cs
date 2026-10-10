using GameMcp.Windowing;

namespace GameMcp.Overlay;

public sealed class OverlayOptions
{
    public bool Enabled { get; set; }

    public bool ShowTarget { get; set; } = true;

    /// <summary>标出目标后到真正执行的等待时间（毫秒），0 表示不等待。</summary>
    public int PreActionDelayMs { get; set; }

    /// <summary>提示滞留时长（毫秒）。</summary>
    public int HoldMs { get; set; } = 300;
}

/// <summary>覆盖在目标窗口之上的操作可视化层。坐标为客户区物理像素。</summary>
public interface IOverlayService : IDisposable
{
    bool IsRunning { get; }

    void Start(nint windowHandle);

    void Stop();

    /// <summary>把目标矩形换算到悬浮窗坐标后显示。</summary>
    void ShowTarget(PixelRect clientRect, string label);

    void Clear();
}
