namespace GameMcp.Input;

/// <summary>输入层的可预期失败。调用方按 <see cref="Code"/> 转成结构化错误。</summary>
public sealed class InputException(string code, string message, string? hint = null) : Exception(message)
{
    /// <summary>机器可读的错误码，直接作为 MCP 结果里的 <c>error</c> 字段。</summary>
    public string Code { get; } = code;

    /// <summary>给模型看的下一步建议。</summary>
    public string? Hint { get; } = hint;

    public static InputException UnsupportedInBackground(string operation) => new(
        "unsupported_in_background",
        $"后台模式不支持：{operation}",
        "改用前台模式（把 background 设为 false），或先调用 focus_window 再切换到前台模式");

    public static InputException NoTarget() => new(
        "no_target_window",
        "尚未绑定目标窗口",
        "先调用 attach_window 绑定窗口");

    public static InputException UnknownKey(string key) => new(
        "unknown_key",
        $"无法识别的键名：{key}",
        KeyMap.NameHint);

    public static InputException StoppedByUser(string panicKey) => new(
        "stopped_by_user",
        "用户按下了急停键，输入已被暂停",
        $"等用户再次按下 {panicKey} 解除；在此之前不要重试，也不要绕道其他工具操作目标窗口");
}
