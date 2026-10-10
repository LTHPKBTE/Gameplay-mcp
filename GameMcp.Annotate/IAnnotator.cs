using OpenCvSharp;

namespace GameMcp.Annotate;

/// <summary>交互元素检测与文字识别。输入为返回图像素，输出元素表同样以返回图像素计量。</summary>
public interface IAnnotator : IDisposable
{
    /// <summary>模型是否已就绪。未启用或初始化失败时为 <c>false</c>。</summary>
    bool IsAvailable { get; }

    /// <summary>当前实际使用的执行提供程序名称，用于诊断。</summary>
    string ProviderName { get; }

    AnnotationResult Annotate(Mat bgr, string snapshotId);
}
