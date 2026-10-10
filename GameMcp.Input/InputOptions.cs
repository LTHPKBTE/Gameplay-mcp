namespace GameMcp.Input;

public sealed class InputOptions
{
    public InputMode Mode { get; set; } = InputMode.Background;

    public KeySendMode KeySendMode { get; set; } = KeySendMode.VkScan;

    /// <summary>
    /// 投递前是否补发一次激活提示。只对顶层窗口生效：单窗口引擎应用靠它判断自己是否被激活，
    /// 子控件不处理 <c>WM_ACTIVATE</c>。它是启发式补丁，不构成真正的窗口激活。
    /// </summary>
    public bool BackgroundActivateHint { get; set; } = true;

    /// <summary>前台模式等待目标回到前台的超时；0 表示无限等待。</summary>
    public int ForegroundWaitTimeoutMs { get; set; } = 10000;

    /// <summary>操作前置延迟，用于预览悬浮窗。</summary>
    public int PreActionDelayMs { get; set; }

    /// <summary>
    /// 用户急停热键，形如 <c>ctrl+alt+q</c>。按下后所有输入被拒绝并释放已按键，再次按下解除。
    /// 留空表示关闭。
    /// </summary>
    public string PanicKey { get; set; } = "ctrl+alt+q";
}
