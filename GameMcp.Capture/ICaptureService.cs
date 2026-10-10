using GameMcp.Windowing;

namespace GameMcp.Capture;

/// <summary>单窗口的持续采集会话。坐标为屏幕空间物理像素。</summary>
public interface ICaptureService : IDisposable
{
    bool IsAttached { get; }

    nint TargetHandle { get; }

    /// <summary>实际生效的后端，永远不是 <see cref="CaptureBackend.Auto"/>。</summary>
    CaptureBackend Backend { get; }

    /// <summary>当前是否真在走 HDR 通道，未附着或申请失败时为 false；返回的 <see cref="CaptureResult"/> 始终是 8 位 SDR。</summary>
    bool IsHdrActive { get; }

    /// <summary>附着时读到的目标显示器色彩状态；读不到为 null。</summary>
    DisplayColor? DisplayColor { get; }

    /// <summary>HDR 后端交给着色器的曝光系数；非 HDR 后端为 null。是否真生效另看 <see cref="IsHdrActive"/>。</summary>
    float? HdrScale { get; }

    /// <summary>显示器状态与请求的后端矛盾时的提醒；无矛盾为 null。</summary>
    string? BackendNotice { get; }

    CaptureDiagnostics Diagnostics { get; }

    void Attach(nint windowHandle);

    void Detach();

    /// <summary>取当前帧，已裁剪到客户区并缩放到基准画布。窗口尺寸变化时内部自恢复。</summary>
    CaptureResult? Capture();
}
