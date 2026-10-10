namespace GameMcp.Windowing;

/// <summary>窗口枚举、几何与 DPI 查询。坐标为屏幕空间物理像素。</summary>
public interface IWindowService
{
    IReadOnlyList<WindowInfo> Enumerate();

    WindowInfo? Describe(nint handle);

    WindowInfo? Foreground { get; }

    /// <summary>客户区在屏幕空间的物理像素矩形。</summary>
    PixelRect GetClientRectOnScreen(nint handle);

    /// <summary>
    /// 含 DWM 边框的窗口矩形；调用失败时返回 <c>null</c>。
    /// </summary>
    PixelRect? GetExtendedFrameBounds(nint handle);

    uint GetDpi(nint handle);

    bool IsMinimized(nint handle);

    /// <summary>窗口是否被 DWM 遮蔽。遮蔽后的窗口对用户不可见，但仍由 DWM 合成。</summary>
    bool IsCloaked(nint handle);

    bool IsForeground(nint handle);

    bool Focus(nint handle);
}
