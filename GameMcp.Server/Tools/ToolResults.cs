namespace GameMcp.Server.Tools;

/// <summary>工具的统一返回。成功时 <see cref="Ok"/> 为 true；失败时 <see cref="Error"/> 给出机器可读错误码。</summary>
public sealed record InputActionResult
{
    public bool Ok { get; init; }

    public string? Error { get; init; }

    public string? Message { get; init; }

    public string? Hint { get; init; }

    /// <summary>执行后的输入模式，便于调用方跟踪状态变化。</summary>
    public string? Mode { get; init; }

    public int? ClientX { get; init; }

    public int? ClientY { get; init; }

    public int? WaitedMs { get; init; }

    public bool? Interrupted { get; init; }

    /// <summary>是否处于用户急停状态。</summary>
    public bool? Stopped { get; init; }

    /// <summary>配置的急停热键，供模型向用户转述。</summary>
    public string? PanicKey { get; init; }

    /// <summary>急停热键是否已注册到系统；<c>false</c> 表示急停不可用。</summary>
    public bool? PanicActive { get; init; }
}

/// <summary>窗口绑定结果。</summary>
public sealed record AttachResult
{
    public bool Ok { get; init; }

    public string? Error { get; init; }

    public string? Message { get; init; }

    public string? Hint { get; init; }

    public string? Handle { get; init; }

    public string? Title { get; init; }

    public int? ClientWidth { get; init; }

    public int? ClientHeight { get; init; }

    public uint? Dpi { get; init; }

    public bool? IsMinimized { get; init; }
}
