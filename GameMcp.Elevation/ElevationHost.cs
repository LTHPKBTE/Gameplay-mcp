namespace GameMcp.Elevation;

/// <summary>启动时权限处置的结果。</summary>
public enum ElevationStatus
{
    /// <summary>当前进程已经是管理员。</summary>
    Elevated,

    /// <summary>本进程是提权子进程，stdin/stdout/stderr 已接到中继管道。</summary>
    RelayedChild,

    /// <summary>本进程是非管理员父进程，本次调用已中继给提权子进程。</summary>
    RelayedParent,

    /// <summary>命令行给了 <c>--no-admin</c>，按降级模式就地运行。</summary>
    SkippedByFlag,

    /// <summary>用户在 UAC 上点了「否」。</summary>
    UserDeclined,

    /// <summary>UAC 提示迟迟没有结果，握手超时，本次不再等。</summary>
    PromptTimeout,

    /// <summary>提权进程没能启动。</summary>
    LaunchFailed,

    /// <summary>提权子进程起来了但没接上中继。</summary>
    RelayFailed,
}

/// <summary>调用方下一步该做什么。</summary>
public enum ElevationOutcome
{
    /// <summary>在当前进程继续。</summary>
    Continue,

    /// <summary>本次调用已交给提权子进程，调用方立刻返回 <see cref="ElevationResult.ExitCode"/>。</summary>
    Relayed,
}

public readonly record struct ElevationResult(
    ElevationOutcome Outcome,
    int ExitCode,
    ElevationStatus Status,
    string? Detail)
{
    /// <summary>当前进程权限不完整。</summary>
    public bool IsDegraded => Status is ElevationStatus.SkippedByFlag
        or ElevationStatus.UserDeclined
        or ElevationStatus.PromptTimeout
        or ElevationStatus.LaunchFailed
        or ElevationStatus.RelayFailed;

    /// <summary>非管理员运行时的一句话汇总提示，正常情况为 <c>null</c>。</summary>
    public string? Notice => IsDegraded ? BuildNotice(Status, Detail) : null;

    private static string BuildNotice(ElevationStatus status, string? detail)
    {
        var reason = status switch
        {
            ElevationStatus.SkippedByFlag => "命令行指定了 --no-admin",
            ElevationStatus.UserDeclined => "UAC 提权被拒绝",
            ElevationStatus.PromptTimeout => "等待 UAC 结果超时",
            ElevationStatus.LaunchFailed => $"提权进程启动失败：{detail}",
            ElevationStatus.RelayFailed => $"提权子进程未能接入中继：{detail}",
            _ => detail ?? "未知原因",
        };

        var recovery = status == ElevationStatus.SkippedByFlag
            ? "去掉 --no-admin 重新启动即可恢复完整能力"
            : "以管理员身份重新启动，或直接由 MCP 客户端启动本程序（默认会自动请求提权）";

        return $"""
            [gamemcp] 当前以非管理员权限运行（{reason}）。以下能力不可用：
                      · 向以管理员权限运行的程序发送输入：UIPI 会拒绝 SendInput 与 PostMessage
                      · 捕获以管理员权限运行的窗口：可能取不到帧或整幅黑
                      · 读写受保护进程的句柄与内存：会被拒绝
                      {recovery}
            """;
    }
}

/// <summary>提权子进程接不上中继时的退出码，父进程据此区分「子进程失败」与「子进程正常返回」。</summary>
public static class ElevationExitCodes
{
    public const int RelayAttachFailed = 2;
}

/// <summary>
/// 权限入口。在 <c>Main</c> 的第一行调用，之后再碰 <see cref="Console"/>。
/// 非管理员且未给 <c>--no-admin</c> 时用 <c>runas</c> 把自己重新拉起为管理员进程，
/// 再通过命名管道双向中继 stdio：<c>runas</c> 走 ShellExecuteEx，标准句柄不会继承过来。
/// </summary>
public static class ElevationHost
{
    /// <summary>从请求提权到子进程接入中继的总上限；MCP 客户端不会为 UAC 无限期等待。</summary>
    internal static readonly TimeSpan HandshakeWait = TimeSpan.FromSeconds(60);

    public static ElevationResult Run(string[] args)
    {
        var options = ElevationOptions.Parse(args);

        if (options.IsRelayedChild)
        {
            ChildRelay.Attach(options);
            return new ElevationResult(ElevationOutcome.Continue, 0, ElevationStatus.RelayedChild, null);
        }

        if (ElevationState.IsElevated)
        {
            return new ElevationResult(ElevationOutcome.Continue, 0, ElevationStatus.Elevated, null);
        }

        if (options.Skip)
        {
            return new ElevationResult(ElevationOutcome.Continue, 0, ElevationStatus.SkippedByFlag, null);
        }

        return ParentRelay.Run(args);
    }
}
