using System.Runtime.InteropServices;
using Fischless.WindowsInput;
using GameMcp.Windowing;
using Vanara.PInvoke;

namespace GameMcp.Input.Win32;

/// <summary>
/// 前台通道：<c>SendInput</c> 注入真实输入。客户区坐标换算到屏幕坐标后再按虚拟桌面归一化。
/// 能力全量（相对移动、滚轮、中/侧键）；要求目标窗口在前台。
/// </summary>
internal sealed class SendInputBackend : IInputBackend
{
    private readonly IWindowService _windows;
    private readonly InputOptions _options;
    private readonly nint _handle;
    private readonly InputSimulator _input = new();

    private readonly HashSet<User32.VK> _pressed = [];
    private readonly HashSet<MouseButton> _pressedButtons = [];
    private readonly object _pressedLock = new();

    public SendInputBackend(IWindowService windows, nint handle, InputOptions options)
    {
        _windows = windows;
        _handle = handle;
        _options = options;
    }

    public InputMode Mode => InputMode.Foreground;

    public bool SupportsRelativeMove => true;

    public bool SupportsScroll => true;

    public bool SupportsMiddleAndSideButtons => true;

    public void MoveTo(int clientX, int clientY)
    {
        EnsureTarget();
        var (nx, ny) = ToVirtualDesktop(clientX, clientY);
        _input.Mouse.MoveMouseToPositionOnVirtualDesktop(nx, ny);
    }

    public void MoveBy(int dx, int dy) => _input.Mouse.MoveMouseBy(dx, dy);

    public void Click(MouseButton button, int clientX, int clientY, int count)
    {
        MoveTo(clientX, clientY);

        var times = Math.Max(1, count);
        for (var i = 0; i < times; i++)
        {
            Button(button, down: true);
            Thread.Sleep(10);
            Button(button, down: false);

            if (i < times - 1)
            {
                Thread.Sleep(40);
            }
        }
    }

    public void Scroll(int delta, ScrollDirection direction)
    {
        if (direction == ScrollDirection.Vertical)
        {
            _input.Mouse.VerticalScroll(delta);
        }
        else
        {
            _input.Mouse.HorizontalScroll(delta);
        }
    }

    public void KeyDown(string key)
    {
        var vk = ParseKey(key);
        EmitKey(vk, keyUp: false);
        SetPressed(vk, true);
    }

    public void KeyUp(string key)
    {
        var vk = ParseKey(key);
        EmitKey(vk, keyUp: true);
        SetPressed(vk, false);
    }

    public void TypeText(string text) => _input.Keyboard.TextEntry(text);

    public void ReleaseAll()
    {
        User32.VK[] keys;
        MouseButton[] buttons;
        lock (_pressedLock)
        {
            keys = [.. _pressed];
            buttons = [.. _pressedButtons];
            _pressed.Clear();
            _pressedButtons.Clear();
        }

        foreach (var vk in keys)
        {
            EmitKey(vk, keyUp: true);
        }

        // 只抬起确实按下过的鼠标键。没有配对按下的一次孤立抬起会让光标下的窗口收到
        // WM_CONTEXTMENU（实测），即凭空弹出右键菜单。
        foreach (var button in buttons)
        {
            Button(button, down: false);
        }
    }

    public IReadOnlyList<string> PressedKeys
    {
        get
        {
            lock (_pressedLock)
            {
                return [.. _pressed.Select(KeyMap.Name)];
            }
        }
    }

    public void Dispose() => ReleaseAll();

    private void EmitKey(User32.VK vk, bool keyUp)
    {
        var extended = ExtendedKeys.IsExtended(vk);

        if (_options.KeySendMode == KeySendMode.VkScan)
        {
            if (keyUp)
            {
                _input.Keyboard.KeyUp(extended, vk);
            }
            else
            {
                _input.Keyboard.KeyDown(extended, vk);
            }

            return;
        }

        // 纯扫描码：只给 wScan，不填 wVk
        var scan = (ushort)(User32.MapVirtualKey((uint)vk, 0) & 0xFF);
        var flags = User32.KEYEVENTF.KEYEVENTF_SCANCODE;
        if (extended)
        {
            flags |= User32.KEYEVENTF.KEYEVENTF_EXTENDEDKEY;
        }

        if (keyUp)
        {
            flags |= User32.KEYEVENTF.KEYEVENTF_KEYUP;
        }

        User32.INPUT[] inputs =
        [
            new()
            {
                type = User32.INPUTTYPE.INPUT_KEYBOARD,
                ki = new User32.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scan,
                    dwFlags = flags,
                },
            },
        ];

        var sent = User32.SendInput(1, inputs, Marshal.SizeOf(typeof(User32.INPUT)));
        if (sent != 1)
        {
            throw new InputException("send_input_failed", $"扫描码注入失败：{vk}", "确认本程序未被安全软件拦截，且目标窗口为普通权限窗口");
        }
    }

    private void Button(MouseButton button, bool down)
    {
        lock (_pressedLock)
        {
            if (down)
            {
                _pressedButtons.Add(button);
            }
            else
            {
                _pressedButtons.Remove(button);
            }
        }

        var mouse = _input.Mouse;
        switch (button)
        {
            case MouseButton.Left:
                _ = down ? mouse.LeftButtonDown() : mouse.LeftButtonUp();
                break;
            case MouseButton.Right:
                _ = down ? mouse.RightButtonDown() : mouse.RightButtonUp();
                break;
            case MouseButton.Middle:
                _ = down ? mouse.MiddleButtonDown() : mouse.MiddleButtonUp();
                break;
            case MouseButton.X1:
                _ = down ? mouse.XButtonDown(0x0001) : mouse.XButtonUp(0x0001);
                break;
            case MouseButton.X2:
                _ = down ? mouse.XButtonDown(0x0002) : mouse.XButtonUp(0x0002);
                break;
        }
    }

    private User32.VK ParseKey(string key) =>
        KeyMap.TryParse(key, out var vk) ? vk : throw InputException.UnknownKey(key);

    private void EnsureTarget()
    {
        if (_handle == nint.Zero)
        {
            throw InputException.NoTarget();
        }
    }

    /// <summary>客户区物理像素 → 屏幕物理像素 → 全虚拟桌面归一化 0–65535。</summary>
    private (double X, double Y) ToVirtualDesktop(int clientX, int clientY)
    {
        var client = _windows.GetClientRectOnScreen(_handle);
        var screenX = client.X + clientX;
        var screenY = client.Y + clientY;

        var left = User32.GetSystemMetrics(User32.SystemMetric.SM_XVIRTUALSCREEN);
        var top = User32.GetSystemMetrics(User32.SystemMetric.SM_YVIRTUALSCREEN);
        var width = User32.GetSystemMetrics(User32.SystemMetric.SM_CXVIRTUALSCREEN);
        var height = User32.GetSystemMetrics(User32.SystemMetric.SM_CYVIRTUALSCREEN);

        // 用 width-1 / height-1：归一化上界要能取到虚拟桌面最右下那个像素
        var nx = (double)(screenX - left) * 65535 / Math.Max(1, width - 1);
        var ny = (double)(screenY - top) * 65535 / Math.Max(1, height - 1);

        return (Math.Clamp(nx, 0, 65535), Math.Clamp(ny, 0, 65535));
    }

    private void SetPressed(User32.VK vk, bool down)
    {
        lock (_pressedLock)
        {
            if (down)
            {
                _pressed.Add(vk);
            }
            else
            {
                _pressed.Remove(vk);
            }
        }
    }
}
