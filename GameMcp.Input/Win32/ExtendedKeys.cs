using Vanara.PInvoke;

namespace GameMcp.Input.Win32;

/// <summary>
/// 按真实键盘的行为判断按键是否为扩展键（<c>KEYEVENTF_EXTENDEDKEY</c>、按键消息 lParam 的第 24 位）。
/// <para>
/// 不用 <c>InputBuilder.IsExtendedKey</c>：它把左 Alt 以及泛指的 Alt / Ctrl 也算作扩展键，
/// 发出去会被识别成右 Alt / 右 Ctrl。
/// </para>
/// 参考：https://learn.microsoft.com/windows/win32/inputdev/about-keyboard-input#extended-key-flag
/// </summary>
internal static class ExtendedKeys
{
    public static bool IsExtended(User32.VK key) => key switch
    {
        // 右侧 Ctrl / Alt；左侧和泛指的 Ctrl / Alt 不是扩展键
        User32.VK.VK_RCONTROL or User32.VK.VK_RMENU => true,

        // 小键盘左侧的编辑区和方向键
        User32.VK.VK_INSERT or User32.VK.VK_DELETE
            or User32.VK.VK_HOME or User32.VK.VK_END
            or User32.VK.VK_PRIOR or User32.VK.VK_NEXT
            or User32.VK.VK_LEFT or User32.VK.VK_UP
            or User32.VK.VK_RIGHT or User32.VK.VK_DOWN => true,

        // 小键盘 / 与 NumLock，Break（Ctrl+Pause）与 PrintScreen
        User32.VK.VK_DIVIDE or User32.VK.VK_NUMLOCK
            or User32.VK.VK_CANCEL or User32.VK.VK_SNAPSHOT => true,

        // 左右 Win 与菜单键
        User32.VK.VK_LWIN or User32.VK.VK_RWIN or User32.VK.VK_APPS => true,

        _ => false,
    };
}
