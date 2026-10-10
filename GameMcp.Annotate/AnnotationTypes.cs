using GameMcp.Windowing;

namespace GameMcp.Annotate;

/// <summary>元素类别。</summary>
public enum ElementKind
{
    Icon,
    Text,
}

/// <summary>标注元素。坐标空间为返回图像素。</summary>
public sealed record AnnotationElement(
    int Index,
    ElementKind Kind,
    string? Text,
    PixelRect Box,
    double Confidence)
{
    public int CenterX => Box.X + (Box.Width / 2);

    public int CenterY => Box.Y + (Box.Height / 2);
}

/// <summary>一次标注的元素表，与快照号绑定。</summary>
public sealed record AnnotationResult(
    string SnapshotId,
    IReadOnlyList<AnnotationElement> Elements,
    long ElapsedMs);

public sealed class AnnotateOptions
{
    public bool Enabled { get; set; }

    /// <summary>是否同时跑 OCR。</summary>
    public bool OcrEnabled { get; set; } = true;

    public float ConfidenceThreshold { get; set; } = 0.05f;

    public float IouThreshold { get; set; } = 0.45f;
}
