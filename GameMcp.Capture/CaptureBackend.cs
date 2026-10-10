using Fischless.GameCapture;
using Fischless.GameCapture.BitBlt;
using Fischless.GameCapture.DwmSharedSurface;
using Fischless.GameCapture.Graphics;

namespace GameMcp.Capture;

/// <summary>
/// 采集后端。不含 <c>Fischless.GameCapture</c> 自带的
/// <c>WindowsGraphicsCapture</c> / <c>WindowsGraphicsCaptureHdr</c>：它们是 <c>FrameArrived</c> 推送式的，
/// 要创建线程上有 DispatcherQueue 与消息泵，而本进程是控制台，没有消息泵。
/// </summary>
public enum CaptureBackend
{
    /// <summary>Windows Graphics Capture v2，自由线程帧池、拉取式取帧。默认后端。</summary>
    GraphicsCapture,

    /// <summary>同 <see cref="GraphicsCapture"/>，但申请 FP16 HDR 表面并过一遍 HDR→SDR 计算着色器；申请失败自动退回 SDR。</summary>
    GraphicsCaptureHdr,

    /// <summary>DWM 共享表面。只对以 DWM 共享表面呈现的 D3D 窗口有效，普通窗口恒取不到帧。</summary>
    SharedSurface,

    /// <summary>GDI BitBlt。最慢，硬件加速窗口可能整幅纯黑，仅作最后的兼容兜底。</summary>
    BitBlt,
}

internal static class CaptureBackendFactory
{
    public static bool IsHdr(CaptureBackend backend) => backend == CaptureBackend.GraphicsCaptureHdr;

    /// <summary>取实际生效的 HDR 状态读取器；后端不支持时为 <c>null</c>。</summary>
    public static Func<bool>? HdrProbe(IGameCapture capture)
        => capture is GraphicsCaptureV2 v2 ? () => v2.IsHdrActive : null;

    public static IGameCapture Create(CaptureBackend backend) => backend switch
    {
        CaptureBackend.GraphicsCapture => new GraphicsCaptureV2(),
        CaptureBackend.GraphicsCaptureHdr => new GraphicsCaptureV2(captureHdr: true),
        CaptureBackend.SharedSurface => new SharedSurfaceCapture(),
        CaptureBackend.BitBlt => new BitBltCapture(),
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null),
    };
}
