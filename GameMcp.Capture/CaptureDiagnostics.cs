namespace GameMcp.Capture;

/// <summary>采集过程的累加计数与耗时，用于泄漏与性能诊断。</summary>
public sealed class CaptureDiagnostics
{
    private long _elapsedTicks;

    public long Frames { get; private set; }

    /// <summary>未取到新帧的累计次数。</summary>
    public long EmptyFrames { get; private set; }

    public long Crops { get; private set; }

    public long Resizes { get; private set; }

    /// <summary>捕获面未裁剪、且无法确定客户区偏移，只能返回整幅。</summary>
    public long UncroppedFallbacks { get; private set; }

    /// <summary>帧尺寸与实时客户区不一致（窗口刚改过尺寸）。</summary>
    public long GeometryMismatches { get; private set; }

    /// <summary>因目标窗口被 DWM 遮蔽而丢掉的帧。</summary>
    public long CloakedFrames { get; private set; }

    public double LastTotalMs { get; private set; }

    public double AverageTotalMs => Frames == 0 ? 0 : _elapsedTicks / (double)Frames / TimeSpan.TicksPerMillisecond;

    public string? LastError { get; private set; }

    public void RecordFrame(TimeSpan elapsed)
    {
        Frames++;
        _elapsedTicks += elapsed.Ticks;
        LastTotalMs = elapsed.TotalMilliseconds;
    }

    public void RecordEmpty() => EmptyFrames++;

    public void RecordCrop() => Crops++;

    public void RecordResize() => Resizes++;

    public void RecordUncroppedFallback() => UncroppedFallbacks++;

    public void RecordGeometryMismatch() => GeometryMismatches++;

    public void RecordCloaked() => CloakedFrames++;

    public void RecordError(Exception exception) => LastError = exception.Message;

    public void Reset()
    {
        Frames = 0;
        EmptyFrames = 0;
        Crops = 0;
        Resizes = 0;
        UncroppedFallbacks = 0;
        GeometryMismatches = 0;
        CloakedFrames = 0;
        LastTotalMs = 0;
        _elapsedTicks = 0;
        LastError = null;
    }
}
