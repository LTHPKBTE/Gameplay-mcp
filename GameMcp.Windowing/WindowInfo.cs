namespace GameMcp.Windowing;

public sealed record WindowInfo(
    nint Handle,
    string Title,
    string ClassName,
    PixelRect WindowRect,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsForeground,
    bool IsTopMost);
