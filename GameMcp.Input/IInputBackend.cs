namespace GameMcp.Input;

/// <summary>单条投递通道。坐标一律为客户区物理像素。</summary>
public interface IInputBackend : IDisposable
{
    InputMode Mode { get; }

    bool SupportsRelativeMove { get; }

    bool SupportsScroll { get; }

    bool SupportsMiddleAndSideButtons { get; }

    void MoveTo(int clientX, int clientY);

    void MoveBy(int dx, int dy);

    void Click(MouseButton button, int clientX, int clientY, int count);

    void Scroll(int delta, ScrollDirection direction);

    void KeyDown(string key);

    void KeyUp(string key);

    void TypeText(string text);

    /// <summary>释放本通道按下但未松开的按键与鼠标键。</summary>
    void ReleaseAll();
}
