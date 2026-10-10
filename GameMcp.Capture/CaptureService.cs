using System.Diagnostics;
using Fischless.GameCapture;
using GameMcp.Windowing;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;

namespace GameMcp.Capture;

/// <summary>
/// 采集会话：复用 <c>Fischless.GameCapture</c> 取帧，自己做客户区裁剪与基准画布缩放。
/// 每帧只拷贝一次；客户区偏移优先采信捕获器给的矩形，拿不到时才用实时窗口几何推算。
/// </summary>
public sealed class CaptureService : ICaptureService
{
    private readonly IWindowService _windows;
    private readonly CaptureOptions _options;
    private readonly object _gate = new();

    private IGameCapture? _capture;
    private Func<bool>? _hdrProbe;
    private nint _handle;

    public CaptureService(IWindowService windows, CaptureOptions options)
    {
        _windows = windows;
        _options = options;
    }

    public bool IsAttached => _capture is { IsCapturing: true };

    public nint TargetHandle => _handle;

    public bool IsHdrActive => _hdrProbe?.Invoke() == true;

    public CaptureDiagnostics Diagnostics { get; } = new();

    public void Attach(nint windowHandle)
    {
        lock (_gate)
        {
            DetachCore();
            if (windowHandle == nint.Zero)
            {
                return;
            }

            // 遮蔽窗口是已关闭的 UWP 应用之类留下的幽灵窗口，列表里还在但看不见
            if (_windows.IsCloaked(windowHandle))
            {
                throw new InvalidOperationException("目标窗口已被 DWM 遮蔽（对用户不可见），不采集");
            }

            if (_windows.IsMinimized(windowHandle))
            {
                throw new InvalidOperationException("目标窗口已最小化，无法截图");
            }

            var capture = CaptureBackendFactory.Create(_options.Backend);
            var settings = new Dictionary<string, object>
            {
                ["MinUpdateIntervalMs"] = _options.MinUpdateIntervalMs,
                ["UseCpuConvert"] = _options.UseCpuConvert,
                ["DisableDrawWrite"] = _options.DisableDrawWrite,

                // BitBlt 后端只在 settings 里带这个键时才启动
                ["autoFixWin11BitBlt"] = _options.AutoFixWin11BitBlt,
            };

            try
            {
                capture.Start(windowHandle, settings);

                if (!capture.IsCapturing)
                {
                    throw new InvalidOperationException($"{_options.Backend} 后端未启动");
                }
            }
            catch
            {
                capture.Dispose();
                throw;
            }

            _capture = capture;
            _hdrProbe = CaptureBackendFactory.HdrProbe(capture);
            _handle = windowHandle;
            Diagnostics.Reset();
        }
    }

    public void Detach()
    {
        lock (_gate)
        {
            DetachCore();
        }
    }

    public CaptureResult? Capture()
    {
        var started = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            if (_capture is not { IsCapturing: true })
            {
                Diagnostics.RecordEmpty();
                return null;
            }

            // 运行期间窗口可能被遮蔽（收进托盘等），此时不再出帧
            if (_windows.IsCloaked(_handle))
            {
                Diagnostics.RecordCloaked();
                return null;
            }

            try
            {
                using var frame = _capture.Capture();
                var result = frame is null ? null : Build(frame);

                if (result is null)
                {
                    Diagnostics.RecordEmpty();
                }
                else
                {
                    Diagnostics.RecordFrame(Stopwatch.GetElapsedTime(started));
                }

                return result;
            }
            catch (Exception e)
            {
                Diagnostics.RecordError(e);
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DetachCore();
        }
    }

    private void DetachCore()
    {
        _capture?.Dispose();
        _capture = null;
        _hdrProbe = null;
        _handle = nint.Zero;
    }

    private CaptureResult? Build(GameCaptureFrame frame)
    {
        var source = frame.Frame;
        if (source.Empty())
        {
            return null;
        }

        Mat? converted = null;
        try
        {
            var bgr = source;
            if (source.Type() != MatType.CV_8UC3)
            {
                converted = source.CvtColor(ColorConversionCodes.BGRA2BGR);
                bgr = converted;
            }

            var client = ResolveClientRect(frame, bgr, out var roi);

            Mat canvas;
            if (roi is { } area)
            {
                using var view = new Mat(bgr, area);
                canvas = view.Clone();
                Diagnostics.RecordCrop();
            }
            else
            {
                canvas = bgr.Clone();
            }

            var scaled = ResizeToCanvas(canvas);
            if (!ReferenceEquals(scaled, canvas))
            {
                canvas.Dispose();
                Diagnostics.RecordResize();
            }

            return new CaptureResult(scaled, client, IsHdrActive);
        }
        finally
        {
            converted?.Dispose();
        }
    }

    /// <summary>确定客户区矩形；需要额外裁剪时给出 <paramref name="roi"/>。</summary>
    private PixelRect ResolveClientRect(GameCaptureFrame frame, Mat bgr, out Rect? roi)
    {
        roi = null;

        if (frame.CaptureRect is { } rect)
        {
            var client = new PixelRect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            if (client.Width != bgr.Cols || client.Height != bgr.Rows)
            {
                // 客户区几何以帧尺寸为准，窗口刚改过尺寸时这里会先差一帧
                Diagnostics.RecordGeometryMismatch();
                client = new PixelRect(client.X, client.Y, bgr.Cols, bgr.Rows);
            }

            return client;
        }

        var live = _windows.GetClientRectOnScreen(_handle);
        var surface = _windows.GetExtendedFrameBounds(_handle) ?? _windows.Describe(_handle)?.WindowRect;

        if (surface is not { } bounds || bounds.IsEmpty || live.IsEmpty)
        {
            Diagnostics.RecordUncroppedFallback();
            return new PixelRect(0, 0, bgr.Cols, bgr.Rows);
        }

        var dx = live.X - bounds.X;
        var dy = live.Y - bounds.Y;
        if (dx < 0 || dy < 0 || dx + live.Width > bgr.Cols || dy + live.Height > bgr.Rows)
        {
            Diagnostics.RecordUncroppedFallback();
            return new PixelRect(bounds.X, bounds.Y, bgr.Cols, bgr.Rows);
        }

        roi = new Rect(dx, dy, live.Width, live.Height);
        return live;
    }

    private Mat ResizeToCanvas(Mat canvas)
    {
        var longest = Math.Max(canvas.Cols, canvas.Rows);
        if (_options.CanvasMax <= 0 || longest <= _options.CanvasMax)
        {
            return canvas;
        }

        var ratio = _options.CanvasMax / (double)longest;
        var size = new Size(
            Math.Max(1, (int)Math.Round(canvas.Cols * ratio)),
            Math.Max(1, (int)Math.Round(canvas.Rows * ratio)));

        var resized = new Mat();
        Cv2.Resize(canvas, resized, size, 0, 0, InterpolationFlags.Area);
        return resized;
    }
}
