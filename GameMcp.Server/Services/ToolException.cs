namespace GameMcp.Server.Services;

/// <summary>工具层可预期的失败。工具方法统一把它转成结构化错误结果。</summary>
public sealed class ToolException(string code, string message, string? hint = null) : Exception(message)
{
    public string Code { get; } = code;

    public string? Hint { get; } = hint;
}
