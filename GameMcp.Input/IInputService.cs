using GameMcp.Windowing;

namespace GameMcp.Input;

/// <summary>对当前绑定窗口的输入门面。所有坐标为客户区物理像素。</summary>
public interface IInputService : IDisposable
{
    InputMode Mode { get; }

    IInputBackend Backend { get; }

    nint TargetHandle { get; }

    bool IsTargetForeground { get; }

    /// <summary>绑定目标窗口；传入 <c>0</c> 解除绑定。切换时会释放已按下的键。</summary>
    void Attach(nint handle);

    /// <summary>切换输入通道；切换时释放已按下状态。</summary>
    void SwitchMode(InputMode mode);

    /// <summary>前台模式下会切走输入注入，此处等待目标重新回到前台。</summary>
    Task<bool> WaitForForegroundAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>坐标换算用：客户区物理像素 → 屏幕物理像素。</summary>
    PixelRect ClientRectOnScreen { get; }

    /// <summary>用户急停是否已触发。</summary>
    bool IsPanicTripped { get; }

    /// <summary>配置的急停热键；空表示未启用。</summary>
    string PanicKey { get; }

    /// <summary>急停热键是否挂在系统上。若为 <c>false</c>，急停不可用。</summary>
    bool IsPanicHotkeyActive { get; }

    string? PanicHotkeyError { get; }

    /// <summary>急停期间有效的中断信号，供耗时操作提前退出。</summary>
    CancellationToken PanicToken { get; }

    /// <summary>急停已触发时抛 <c>stopped_by_user</c>。</summary>
    void EnsureInputAllowed();

    /// <summary>释放两个通道上按下的所有键与鼠标键。</summary>
    void ReleaseAll();
}
