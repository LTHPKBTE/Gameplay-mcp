using Vanara.PInvoke;

namespace GameMcp.Input.Win32;

/// <summary>
/// 向指定窗口投递合成鼠标/键盘消息。坐标一律为客户区坐标。
/// <para>
/// 只做原子投递，不含任何等待；时序由调用方决定。
/// </para>
/// </summary>
internal static class PostMessageSimulator
{
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;

    /// <summary>
    /// 按键消息的 lParam：重复次数 1、扫描码 <c>MapVirtualKey(vk) &lt;&lt; 16</c>、
    /// 扩展键置第 24 位、抬起时置第 30/31 位。
    /// </summary>
    public static nint MakeKeyLParam(User32.VK vk, bool keyUp)
    {
        var scan = User32.MapVirtualKey((uint)vk, 0) & 0xFFu;
        var lParam = 1u | (scan << 16);
        if (ExtendedKeys.IsExtended(vk))
        {
            lParam |= 1u << 24;
        }

        if (keyUp)
        {
            lParam |= (1u << 30) | (1u << 31);
        }

        return unchecked((nint)lParam);
    }

    /// <summary>客户区坐标打包成鼠标消息的 lParam。</summary>
    public static nint MakeLParam(int x, int y) => (y << 16) | (x & 0xFFFF);

    /// <summary>向窗口投递 <c>WM_ACTIVATE</c>。目标非前台时先补一条，作为兼容性补丁。</summary>
    public static void Activate(nint hWnd) => User32.PostMessage(hWnd, User32.WindowMessage.WM_ACTIVATE, 1, 0);

    public static void KeyDown(nint hWnd, User32.VK vk) =>
        User32.PostMessage(hWnd, User32.WindowMessage.WM_KEYDOWN, (nint)vk, MakeKeyLParam(vk, false));

    public static void KeyUp(nint hWnd, User32.VK vk) =>
        User32.PostMessage(hWnd, User32.WindowMessage.WM_KEYUP, (nint)vk, MakeKeyLParam(vk, true));

    /// <summary>字符消息：重复次数 1、无扫描码。</summary>
    public static void Char(nint hWnd, char c) => User32.PostMessage(hWnd, User32.WindowMessage.WM_CHAR, c, 1);

    /// <summary>悬停。wParam 为 0（无按键按下）。</summary>
    public static void MouseMove(nint hWnd, int x, int y) =>
        User32.PostMessage(hWnd, WM_MOUSEMOVE, 0, MakeLParam(x, y));

    public static void ButtonDown(nint hWnd, MouseButton button, int x, int y) =>
        User32.PostMessage(hWnd, button == MouseButton.Right ? WM_RBUTTONDOWN : WM_LBUTTONDOWN, 1, MakeLParam(x, y));

    public static void ButtonUp(nint hWnd, MouseButton button, int x, int y) =>
        User32.PostMessage(hWnd, button == MouseButton.Right ? WM_RBUTTONUP : WM_LBUTTONUP, 0, MakeLParam(x, y));
}
