using System.ComponentModel;
using GameMcp.Input;
using GameMcp.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace GameMcp.Server.Tools;

[McpServerToolType]
public sealed class InputTools(
    IInputService input,
    SessionService session,
    IOptions<InputOptions> options,
    ILogger<InputTools> logger)
{
    private InputOptions Options => options.Value;

    [McpServerTool(Name = "get_input_state", ReadOnly = true, UseStructuredContent = true)]
    [Description("查看当前输入状态：模式、绑定窗口、目标是否在前台。每次切换模式后可调用它确认。")]
    public InputActionResult GetInputState()
    {
        var target = session.Target;
        var message = input.IsPanicTripped
            ? $"已急停：用户按下了 {input.PanicKey}，输入操作全部被拒绝，等用户再次按下该键解除"
            : target is null
                ? "尚未绑定窗口"
                : $"已绑定 {target.Title}（0x{target.Handle:X}），当前{(input.IsTargetForeground ? "在前台" : "不在前台")}";

        return new InputActionResult
        {
            Ok = true,
            Mode = ModeName(input.Mode),
            Stopped = input.IsPanicTripped,
            PanicKey = input.PanicKey,
            PanicActive = input.IsPanicHotkeyActive,
            Hint = input.IsPanicHotkeyActive ? null : input.PanicHotkeyError,
            Message = message,
        };
    }

    [McpServerTool(Name = "click_absolute", Destructive = true, UseStructuredContent = true)]
    [Description("在指定坐标点击。坐标为 get_state 返回图的像素；也可用 index 指定元素（需有效的 snapshot_id）。")]
    public Task<InputActionResult> ClickAbsoluteAsync(
        [Description("x 坐标（返回图像素）")] int x,
        [Description("y 坐标（返回图像素）")] int y,
        [Description("按键：left / right / middle / x1 / x2，默认 left")] string button = "left",
        [Description("点击次数，2 即双击")] int count = 1,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            var (cx, cy) = session.ToClient(x, y);
            input.Backend.Click(ParseButton(button), cx, cy, count);
            return new InputActionResult { ClientX = cx, ClientY = cy };
        });
    }

    [McpServerTool(Name = "move_absolute", Destructive = true, UseStructuredContent = true)]
    [Description("把光标移到指定坐标。前台模式下移动真实光标，后台模式只向目标投递悬停消息。")]
    public Task<InputActionResult> MoveAbsoluteAsync(
        [Description("x 坐标（返回图像素）")] int x,
        [Description("y 坐标（返回图像素）")] int y,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            var (cx, cy) = session.ToClient(x, y);
            input.Backend.MoveTo(cx, cy);
            return new InputActionResult { ClientX = cx, ClientY = cy };
        });
    }

    [McpServerTool(Name = "move_relative", Destructive = true, UseStructuredContent = true)]
    [Description("按相对位移移动光标，用于 3D 视角转向或拖拽。仅前台模式支持；位移量受系统指针速度与加速影响。")]
    public Task<InputActionResult> MoveRelativeAsync(
        [Description("水平位移量（鼠标单位，可为负）")] int dx,
        [Description("垂直位移量（鼠标单位，可为负）")] int dy,
        [Description("把位移分摊到该时长内发送；0 表示一次发出")] int durationMs = 0,
        CancellationToken cancellationToken = default)
    {
        return RunForegroundOnlyAsync(cancellationToken, () =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, input.PanicToken);
            var token = linked.Token;

            if (durationMs <= 0)
            {
                input.Backend.MoveBy(dx, dy);
                return new InputActionResult();
            }

            const int steps = 10;
            for (var i = 0; i < steps; i++)
            {
                input.EnsureInputAllowed();
                input.Backend.MoveBy(dx / steps, dy / steps);
                SleepInterruptible(Math.Max(1, durationMs / steps), token);
            }

            input.Backend.MoveBy(dx % steps, dy % steps);
            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "scroll", Destructive = true, UseStructuredContent = true)]
    [Description("滚动滚轮。仅前台模式支持；后台模式会返回 unsupported_in_background。")]
    public Task<InputActionResult> ScrollAsync(
        [Description("滚动量，正数向上/向右；1 相当于一格")] int delta,
        [Description("vertical（默认）或 horizontal")] string direction = "vertical",
        [Description("可选：先把光标移到该 x（返回图像素）")] int? x = null,
        [Description("可选：先把光标移到该 y（返回图像素）")] int? y = null,
        CancellationToken cancellationToken = default)
    {
        return RunForegroundOnlyAsync(cancellationToken, () =>
        {
            if (x is { } px && y is { } py)
            {
                var (cx, cy) = session.ToClient(px, py);
                input.Backend.MoveTo(cx, cy);
            }

            var axis = direction.Trim().ToLowerInvariant() switch
            {
                "vertical" or "v" => ScrollDirection.Vertical,
                "horizontal" or "h" => ScrollDirection.Horizontal,
                _ => throw new ToolException("unknown_direction", $"未知的滚动方向：{direction}", "可用 vertical 或 horizontal"),
            };

            input.Backend.Scroll(delta, axis);
            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "press_key", Destructive = true, UseStructuredContent = true)]
    [Description("按下并松开一个键。支持组合键语法，如 ctrl+shift+a。")]
    public Task<InputActionResult> PressKeyAsync(
        [Description(KeyMap.NameHint)] string key,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            var backend = input.Backend;
            var keys = SplitCombo(key);

            // 修饰键先按，主键后按；松开的顺序相反，与真实键盘一致
            foreach (var part in keys)
            {
                backend.KeyDown(part);
            }

            for (var i = keys.Count - 1; i >= 0; i--)
            {
                backend.KeyUp(keys[i]);
            }

            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "key_down", Destructive = true, UseStructuredContent = true)]
    [Description("按住一个键不松开，用于长按。务必在结束前调用 key_up 或 release_all。")]
    public Task<InputActionResult> KeyDownAsync(
        [Description(KeyMap.NameHint)] string key,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            input.Backend.KeyDown(key);
            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "key_up", Destructive = true, UseStructuredContent = true)]
    [Description("松开一个键。")]
    public Task<InputActionResult> KeyUpAsync(
        [Description(KeyMap.NameHint)] string key,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            input.Backend.KeyUp(key);
            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "type_text", Destructive = true, UseStructuredContent = true)]
    [Description("输入一段文本。前台用 Unicode 注入；后台逐字符投递 WM_CHAR，只对处理字符消息的窗口有效。")]
    public Task<InputActionResult> TypeTextAsync(
        [Description("要输入的文本")] string text,
        [Description("true 用后台通道，false 用前台通道；留空沿用当前模式")] bool? background = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(background, cancellationToken, () =>
        {
            input.Backend.TypeText(text);
            return new InputActionResult();
        });
    }

    [McpServerTool(Name = "release_all", Destructive = true, UseStructuredContent = true)]
    [Description("松开本服务按下过的所有键与鼠标键。异常或中断后的安全兜底。")]
    public InputActionResult ReleaseAll()
    {
        input.ReleaseAll();
        return new InputActionResult
        {
            Ok = true,
            Mode = ModeName(input.Mode),
            Stopped = input.IsPanicTripped,
            PanicKey = input.PanicKey,
        };
    }

    private async Task<InputActionResult> RunAsync(
        bool? background,
        CancellationToken cancellationToken,
        Func<InputActionResult> action)
    {
        try
        {
            session.RequireTarget();
            input.EnsureInputAllowed();
            await EnsureReadyAsync(background, cancellationToken).ConfigureAwait(false);

            if (Options.PreActionDelayMs > 0)
            {
                await Task.Delay(Options.PreActionDelayMs, cancellationToken).ConfigureAwait(false);
            }

            var result = action();
            return result with { Ok = true, Mode = ModeName(input.Mode) };
        }
        catch (InputException e)
        {
            return Fail(e.Code, e.Message, e.Hint);
        }
        catch (ToolException e)
        {
            return Fail(e.Code, e.Message, e.Hint);
        }
        catch (OperationCanceledException)
        {
            input.ReleaseAll();
            return input.IsPanicTripped
                ? Fail("stopped_by_user", "用户按下了急停键，输入已被暂停", $"等用户再次按下 {input.PanicKey} 解除")
                : Fail("cancelled", "请求已取消，已释放按下的键", null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "输入操作失败");
            input.ReleaseAll();
            return Fail("input_failed", e.Message, null);
        }
    }

    private async Task<InputActionResult> RunForegroundOnlyAsync(
        CancellationToken cancellationToken,
        Func<InputActionResult> action)
    {
        if (input.Mode != InputMode.Foreground)
        {
            return Fail(
                "unsupported_in_background",
                "该操作仅在前台模式下可用",
                "把 background 设为 false 切到前台模式，或先调用 focus_window");
        }

        return await RunAsync(null, cancellationToken, action).ConfigureAwait(false);
    }

    private async Task EnsureReadyAsync(bool? background, CancellationToken cancellationToken)
    {
        if (background is { } flag)
        {
            input.SwitchMode(flag ? InputMode.Background : InputMode.Foreground);
        }

        if (input.Mode == InputMode.Background)
        {
            return;
        }

        var timeout = Options.ForegroundWaitTimeoutMs <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(Options.ForegroundWaitTimeoutMs);

        if (!await input.WaitForForegroundAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw new ToolException(
                "foreground_timeout",
                $"等待目标窗口回到前台超时（{Options.ForegroundWaitTimeoutMs}ms）",
                "可调用 focus_window 把目标窗口唤到前台后重试");
        }
    }

    /// <summary>键名是单字符时不拆组合键，避免把 <c>+</c> 本身当成分隔符。</summary>
    private static IReadOnlyList<string> SplitCombo(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length <= 1)
        {
            return [trimmed];
        }

        return trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static MouseButton ParseButton(string button) => button.Trim().ToLowerInvariant() switch
    {
        "left" or "l" or "mouseleft" => MouseButton.Left,
        "right" or "r" or "mouseright" => MouseButton.Right,
        "middle" or "m" or "mousemiddle" => MouseButton.Middle,
        "x1" or "mousex1" => MouseButton.X1,
        "x2" or "mousex2" => MouseButton.X2,
        _ => throw new ToolException("unknown_button", $"未知的鼠标键：{button}", "可用 left / right / middle / x1 / x2"),
    };

    private static void SleepInterruptible(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds > 0 && cancellationToken.WaitHandle.WaitOne(milliseconds))
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static InputActionResult Fail(string code, string message, string? hint) =>
        new() { Ok = false, Error = code, Message = message, Hint = hint };

    private static string ModeName(InputMode mode) => mode == InputMode.Foreground ? "foreground" : "background";
}
