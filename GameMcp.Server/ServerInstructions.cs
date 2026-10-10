namespace GameMcp.Server;

/// <summary>
/// <c>initialize</c> 返回给客户端的服务端说明，随会话固定不变。
/// </summary>
internal static class ServerInstructions
{
    public const string Text = """
        面向游戏窗口的自动化控制。所有坐标都是目标窗口的客户区坐标，与 get_state 返回的截图同一尺度。

        主循环：
        1. attach_window 绑定目标；不知句柄时先 list_windows。
        2. 每轮动作前先 get_state 取截图，看清当前画面再决定下一步。
        3. 点击用 click_absolute（客户区像素坐标）；相对移动用 move_relative（仅前台模式支持）。
        4. 动作后再 get_state 复核。不要凭上一帧的假设连续操作。

        出错时：
        - 结果里出现 error，先调 get_environment 再判断。它能区分权限降级、窗口不可见、通道不支持该操作这几种情况。
        - 后台模式不支持相对移动、滚轮、中键与侧键，这类调用会返回 unsupported_in_background，改前台模式或换操作。
        - 同一个调用失败两次就别再重试，先取状态。

        等待：等动画或加载时用一次 wait，不要用连续 get_state 轮询。

        用户急停：用户按 ctrl+alt+q 会挂起全部输入，之后输入类工具返回 stopped_by_user。这是用户主动行为，不是故障；
        不要尝试绕过，也不要改写配置，等用户再按一次即可恢复。
        """;
}
