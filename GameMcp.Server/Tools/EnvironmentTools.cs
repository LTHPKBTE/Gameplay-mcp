using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using GameMcp.Capture;
using GameMcp.Elevation;
using GameMcp.Input;
using GameMcp.Server.Services;
using GameMcp.Windowing;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace GameMcp.Server.Tools;

[McpServerToolType]
public sealed class EnvironmentTools(
    IWindowService windows,
    ICaptureService capture,
    IInputService input,
    SessionService session,
    HostState host,
    IOptions<CaptureOptions> captureOptions,
    IOptions<InputOptions> inputOptions)
{
    [McpServerTool(Name = "get_environment", ReadOnly = true, UseStructuredContent = true)]
    [Description(
        "查看运行环境与当前能力。失败前或结果异常时先调它：能看出是否因权限不足而降级、" +
        "目标窗口处于什么状态、采集与输入各支持哪些操作。")]
    public EnvironmentResult GetEnvironment()
    {
        return new EnvironmentResult
        {
            Runtime = new RuntimeInfo(
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessId),
            Elevation = new ElevationInfo(
                ElevationState.IsElevated,
                host.Elevation.Status.ToString(),
                host.Elevation.IsDegraded,
                Flatten(host.Elevation.Notice)),
            Target = DescribeTarget(),
            Capture = DescribeCapture(),
            Input = DescribeInput(),
        };
    }

    private TargetInfo? DescribeTarget()
    {
        if (session.Target is not { } target)
        {
            return null;
        }

        // session.Target 是绑定时的快照，前台、位置、最小化都会变，这里重新查一遍
        var live = windows.Describe(target.Handle) ?? target;
        var client = windows.GetClientRectOnScreen(target.Handle);

        return new TargetInfo(
            HandleText.Format(live.Handle),
            live.Title,
            live.ClassName,
            live.WindowRect,
            client,
            windows.GetDpi(live.Handle),
            live.IsMinimized,
            live.IsCloaked,
            live.IsForeground,
            live.IsTopMost);
    }

    private CaptureState DescribeCapture()
    {
        var options = captureOptions.Value;
        var diagnostics = capture.Diagnostics;

        return new CaptureState(
            capture.Backend.ToString(),
            capture.IsAttached,
            capture.IsHdrActive,
            capture.HdrScale,
            capture.BackendNotice,
            options.CanvasMax,
            options.Format.ToString().ToLowerInvariant(),
            options.WebPQuality,
            diagnostics.Frames,
            diagnostics.EmptyFrames,
            diagnostics.Crops,
            diagnostics.Resizes,
            diagnostics.UncroppedFallbacks,
            diagnostics.GeometryMismatches,
            diagnostics.CloakedFrames,
            diagnostics.LastTotalMs,
            diagnostics.AverageTotalMs,
            diagnostics.LastError);
    }

    private InputState DescribeInput()
    {
        var options = inputOptions.Value;
        var backend = input.Backend;

        return new InputState(
            input.Mode.ToString().ToLowerInvariant(),
            backend.SupportsRelativeMove,
            backend.SupportsScroll,
            backend.SupportsMiddleAndSideButtons,
            options.KeySendMode.ToString(),
            options.BackgroundActivateHint,
            options.ForegroundWaitTimeoutMs,
            options.PreActionDelayMs,
            input.PanicKey,
            input.IsPanicHotkeyActive,
            input.PanicHotkeyError,
            input.IsPanicTripped,
            input.PressedKeys);
    }

    /// <summary>提示原本是为 stderr 排版的，送给模型时压成一行。</summary>
    private static string? Flatten(string? notice)
        => notice is null ? null : string.Join(' ', notice.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary><c>get_environment</c> 的返回。</summary>
public sealed record EnvironmentResult
{
    public required RuntimeInfo Runtime { get; init; }

    public required ElevationInfo Elevation { get; init; }

    /// <summary>尚未绑定窗口时为 <c>null</c>。</summary>
    public TargetInfo? Target { get; init; }

    public required CaptureState Capture { get; init; }

    public required InputState Input { get; init; }
}

public sealed record RuntimeInfo(
    string Os,
    string Framework,
    string Architecture,
    int ProcessId);

/// <summary>
/// <see cref="Degraded"/> 为 <c>true</c> 表示当前不是管理员：向提权程序输入、捕获提权窗口都会失败。
/// </summary>
public sealed record ElevationInfo(
    bool Elevated,
    string Status,
    bool Degraded,
    string? Notice);

public sealed record TargetInfo(
    string Handle,
    string Title,
    string ClassName,
    PixelRect WindowRect,
    PixelRect ClientRect,
    uint Dpi,
    bool IsMinimized,
    bool IsCloaked,
    bool IsForeground,
    bool IsTopMost);

public sealed record CaptureState(
    string Backend,
    bool Attached,
    bool IsHdrActive,

    // HDR 后端交给着色器的曝光系数，非 HDR 后端为 null；是否真生效另看 IsHdrActive
    float? HdrScale,

    // 显示器状态与请求的后端矛盾时的提醒，如自动选择的原因
    string? BackendNotice,
    int CanvasMax,
    string Format,

    // 默认的 camelCase 策略会把它写成 webPQuality；1..100 有损，大于 100 无损
    [property: JsonPropertyName("webpQuality")] int WebPQuality,
    long Frames,
    long EmptyFrames,
    long Crops,
    long Resizes,
    long UncroppedFallbacks,
    long GeometryMismatches,
    long CloakedFrames,
    double LastTotalMs,
    double AverageTotalMs,
    string? LastError);

/// <summary>各 <c>Supports*</c> 描述当前通道的能力，后台通道的相对移动、滚轮与中侧键为 <c>false</c>。</summary>
public sealed record InputState(
    string Mode,
    bool SupportsRelativeMove,
    bool SupportsScroll,
    bool SupportsMiddleAndSideButtons,
    string KeySendMode,
    bool BackgroundActivateHint,
    int ForegroundWaitTimeoutMs,
    int PreActionDelayMs,
    string PanicKey,
    bool PanicHotkeyActive,
    string? PanicHotkeyError,
    bool PanicTripped,
    IReadOnlyList<string> PressedKeys);
