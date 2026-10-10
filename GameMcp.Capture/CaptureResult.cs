using GameMcp.Windowing;
using OpenCvSharp;

namespace GameMcp.Capture;

/// <summary>编码格式。</summary>
public enum CaptureImageFormat
{
    Png,
    WebP,
}

/// <summary>
/// 一次采集的结果。<see cref="Bgr"/> 以客户区左上角为原点，已缩放到基准画布，类型为 <c>CV_8UC3</c>。
/// </summary>
public sealed record CaptureResult(Mat Bgr, PixelRect ClientRect, bool IsHdrActive) : IDisposable
{
    public int Width => Bgr.Cols;

    public int Height => Bgr.Rows;

    public int ClientWidth => ClientRect.Width;

    public int ClientHeight => ClientRect.Height;

    /// <summary>画布 / 客户区 的水平比。AI 坐标 = 客户区像素 × 本值。</summary>
    public double ScaleX => ClientRect.Width <= 0 ? 1 : Width / (double)ClientRect.Width;

    public double ScaleY => ClientRect.Height <= 0 ? 1 : Height / (double)ClientRect.Height;

    public void Dispose() => Bgr.Dispose();
}
