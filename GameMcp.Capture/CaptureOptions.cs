namespace GameMcp.Capture;

public sealed class CaptureOptions
{
    public CaptureBackend Backend { get; set; } = CaptureBackend.Auto;

    /// <summary>送达模型前等比缩放的画布长边上限，仅缩小不放大。</summary>
    public int CanvasMax { get; set; } = 1920;

    public CaptureImageFormat Format { get; set; } = CaptureImageFormat.Png;

    public int WebPQuality { get; set; } = 90;

    /// <summary>帧池最短更新间隔（毫秒），0 表示不限制。</summary>
    public int MinUpdateIntervalMs { get; set; }

    /// <summary>回读后用 CPU <c>CvtColor</c> 转 BGR；<c>false</c> 走 GPU 打包 BGR24。</summary>
    public bool UseCpuConvert { get; set; }

    /// <summary>关闭「到达侧写入」，改为消费时同步取帧。用于 A/B 对比。</summary>
    public bool DisableDrawWrite { get; set; }
}
