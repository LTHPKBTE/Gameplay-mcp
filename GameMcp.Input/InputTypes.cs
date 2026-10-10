namespace GameMcp.Input;

/// <summary>输入投递通道。</summary>
public enum InputMode
{
    /// <summary>向目标窗口投递合成消息，目标无需前台。</summary>
    Background,

    /// <summary>通过 <c>SendInput</c> 注入真实输入，目标必须前台。</summary>
    Foreground,
}

/// <summary>前台按键注入方式。</summary>
public enum KeySendMode
{
    /// <summary>同时给出虚拟键码与扫描码，扩展键带扩展标志。</summary>
    VkScan,

    /// <summary>纯扫描码注入。</summary>
    ScanCode,
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

/// <summary>鼠标滚轮方向。</summary>
public enum ScrollDirection
{
    Vertical,
    Horizontal,
}
