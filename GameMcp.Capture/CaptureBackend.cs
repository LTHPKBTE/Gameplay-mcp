using Fischless.GameCapture;
using Fischless.GameCapture.Graphics;
using GameMcp.Windowing;

namespace GameMcp.Capture;

/// <summary>
/// 采集后端，只保留 WGC v2 两个。上游 <c>Fischless.GameCapture</c> 里的其余实现本项目不接，也不改动它们：
/// <list type="bullet">
/// <item><c>WindowsGraphicsCapture</c> / <c>WindowsGraphicsCaptureHdr</c>（v1）：<c>FrameArrived</c> 推送式，
/// 要创建线程上有 DispatcherQueue 与消息泵，而本进程是控制台；另外它无法关掉鼠标捕获，且会画黄框。</item>
/// <item><c>BitBltCapture</c>：不支持 HDR，且改注册表 <c>UserGpuPreferences</c> 有全局副作用。</item>
/// <item><c>SharedSurfaceCapture</c>：只对以 DWM 共享表面呈现的窗口有效，普通窗口恒取不到帧。</item>
/// </list>
/// </summary>
public enum CaptureBackend
{
    /// <summary>按目标显示器的高级色彩状态在 <see cref="GraphicsCapture"/> 与 <see cref="GraphicsCaptureHdr"/> 之间选，默认。</summary>
    Auto,

    /// <summary>Windows Graphics Capture v2，自由线程帧池、拉取式取帧。帧池是 8 位，HDR 内容会被压白。</summary>
    GraphicsCapture,

    /// <summary>同 <see cref="GraphicsCapture"/>，但申请 FP16 HDR 表面并过一遍 HDR→SDR 计算着色器；申请失败自动退回 SDR。</summary>
    GraphicsCaptureHdr,
}

internal static class CaptureBackendFactory
{
    public static bool IsHdr(CaptureBackend backend) => backend == CaptureBackend.GraphicsCaptureHdr;

    /// <summary>
    /// 把请求的后端与显示器状态合成实际要用的后端，并给出需要提醒用户的原因。
    /// 只在显示器状态与请求矛盾时返回说明，不替用户改主意（显式指定的后端一律照用）。
    /// </summary>
    public static (CaptureBackend Backend, string? Notice) Resolve(CaptureBackend requested, DisplayColor? color)
        => requested switch
        {
            CaptureBackend.Auto when color is { AdvancedColorEnabled: true }
                => (CaptureBackend.GraphicsCaptureHdr, "自动选择 HDR 后端：显示器已启用高级色彩"),
            CaptureBackend.Auto when color is null
                => (CaptureBackend.GraphicsCapture, "自动选择 SDR 后端：读不到显示器的高级色彩状态"),
            CaptureBackend.Auto
                => (CaptureBackend.GraphicsCapture, "自动选择 SDR 后端：显示器未启用高级色彩"),
            CaptureBackend.GraphicsCaptureHdr when color is { AdvancedColorEnabled: false }
                => (CaptureBackend.GraphicsCaptureHdr, "显示器未启用高级色彩，HDR 通道会把画面压暗"),
            CaptureBackend.GraphicsCapture when color is { AdvancedColorEnabled: true }
                => (CaptureBackend.GraphicsCapture, "显示器已启用高级色彩，SDR 通道会把 HDR 内容压白"),
            _ => (requested, null),
        };

    /// <summary>取实际生效的 HDR 状态读取器；后端不支持时为 <c>null</c>。</summary>
    public static Func<bool>? HdrProbe(IGameCapture capture)
        => capture is GraphicsCaptureV2 v2 ? () => v2.IsHdrActive : null;

    public static IGameCapture Create(CaptureBackend backend, string? hdrToSdrShader = null) => backend switch
    {
        CaptureBackend.GraphicsCapture => new GraphicsCaptureV2(),
        CaptureBackend.GraphicsCaptureHdr => new GraphicsCaptureV2(captureHdr: true, hdrToSdrShader),
        CaptureBackend.Auto => throw new InvalidOperationException("Auto 必须先经 Resolve"),
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null),
    };
}
