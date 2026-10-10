namespace GameMcp.Capture;

/// <summary>单窗口的持续采集会话。坐标为屏幕空间物理像素。</summary>
public interface ICaptureService : IDisposable
{
    bool IsAttached { get; }

    nint TargetHandle { get; }

    /// <summary>当前是否真在走 HDR 通道，未附着或申请失败时为 false；返回的 <see cref="CaptureResult"/> 始终是 8 位 SDR。</summary>
    bool IsHdrActive { get; }

    CaptureDiagnostics Diagnostics { get; }

    void Attach(nint windowHandle);

    void Detach();

    /// <summary>取当前帧，已裁剪到客户区并缩放到基准画布。窗口尺寸变化时内部自恢复。</summary>
    CaptureResult? Capture();
}
