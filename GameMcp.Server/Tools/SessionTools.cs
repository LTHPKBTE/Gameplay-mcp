using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace GameMcp.Server.Tools;

[McpServerToolType]
public sealed class SessionTools
{
    /// <summary>多数 MCP 宿主对单次工具调用约 60 秒超时，留足余量。</summary>
    private const int DefaultMaxWaitMs = 30_000;

    public sealed record WaitResult
    {
        public bool Ok { get; init; }

        public int WaitedMs { get; init; }

        public bool Interrupted { get; init; }

        public string? Hint { get; init; }
    }

    [McpServerTool(Name = "wait", ReadOnly = true, UseStructuredContent = true)]
    [Description("阻塞等待指定时长后返回。用于等战斗结束、等过场，避免反复截图轮询浪费上下文。")]
    public async Task<WaitResult> WaitAsync(
        [Description("等待毫秒数")] int ms,
        [Description("本次等待的上限毫秒数，默认 30000")] int? maxMs = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(maxMs ?? DefaultMaxWaitMs, 1, DefaultMaxWaitMs);
        var target = Math.Clamp(ms, 0, limit);
        var requestedMore = ms > target;

        var watch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(target, cancellationToken).ConfigureAwait(false);
            return new WaitResult
            {
                Ok = true,
                WaitedMs = (int)watch.ElapsedMilliseconds,
                Interrupted = false,
                Hint = requestedMore ? $"请求的 {ms}ms 超过上限，实际等待 {target}ms，可再次调用继续等待" : null,
            };
        }
        catch (OperationCanceledException)
        {
            return new WaitResult
            {
                Ok = true,
                WaitedMs = (int)watch.ElapsedMilliseconds,
                Interrupted = true,
            };
        }
    }
}
