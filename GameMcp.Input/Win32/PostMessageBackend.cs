using System.Runtime.InteropServices;
using GameMcp.Windowing;
using Vanara.PInvoke;

namespace GameMcp.Input.Win32;

/// <summary>
/// 后台通道：向目标窗口投递合成消息，目标无需前台、真实光标不动。
/// 输入坐标是目标顶层窗口的客户区坐标；消息路由到子窗口时会换算到该子窗口的客户区坐标系。
/// 不支持相对移动、滚轮、中键、侧键。
/// </summary>
internal sealed class PostMessageBackend : IInputBackend
{
    /// <summary>子窗口路由结果的缓存上限，超出即整体清空。</summary>
    private const int TargetCacheLimit = 512;

    private readonly IWindowService _windows;
    private readonly InputOptions _options;
    private readonly nint _handle;

    private readonly Dictionary<long, nint> _targets = [];
    private readonly HashSet<User32.VK> _pressed = [];
    private readonly object _pressedLock = new();

    public PostMessageBackend(IWindowService windows, nint handle, InputOptions options)
    {
        _windows = windows;
        _options = options;
        _handle = handle;
    }

    public InputMode Mode => InputMode.Background;

    public bool SupportsRelativeMove => false;

    public bool SupportsScroll => false;

    public bool SupportsMiddleAndSideButtons => false;

    /// <summary>后台不移动真实光标，改为向目标投递悬停消息。</summary>
    public void MoveTo(int clientX, int clientY)
    {
        PostMessageSimulator.MouseMove(EnsureTarget(), clientX, clientY);
    }

    public void MoveBy(int dx, int dy) => throw InputException.UnsupportedInBackground("相对移动");

    public void Click(MouseButton button, int clientX, int clientY, int count)
    {
        if (button is not (MouseButton.Left or MouseButton.Right))
        {
            throw InputException.UnsupportedInBackground($"后台鼠标键 {button}");
        }

        var target = Prepare(clientX, clientY, out var x, out var y);

        var times = Math.Max(1, count);
        for (var i = 0; i < times; i++)
        {
            PostMessageSimulator.ButtonDown(target, button, x, y);
            Thread.Sleep(10);
            PostMessageSimulator.ButtonUp(target, button, x, y);

            if (i < times - 1)
            {
                Thread.Sleep(40);
            }
        }
    }

    public void Scroll(int delta, ScrollDirection direction) =>
        throw InputException.UnsupportedInBackground(direction == ScrollDirection.Vertical ? "滚轮" : "横向滚轮");

    public void KeyDown(string key) => Dispatch(key, static (hWnd, vk) => PostMessageSimulator.KeyDown(hWnd, vk), down: true);

    public void KeyUp(string key) => Dispatch(key, static (hWnd, vk) => PostMessageSimulator.KeyUp(hWnd, vk), down: false);

    public void TypeText(string text)
    {
        var hWnd = KeyTarget();
        ActivateIfNeeded(hWnd);

        foreach (var c in text)
        {
            PostMessageSimulator.Char(hWnd, c);
        }
    }

    public void ReleaseAll()
    {
        User32.VK[] keys;
        lock (_pressedLock)
        {
            keys = [.. _pressed];
            _pressed.Clear();
        }

        if (_handle == nint.Zero)
        {
            return;
        }

        foreach (var vk in keys)
        {
            PostMessageSimulator.KeyUp(KeyTarget(), vk);
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

    public void Dispose()
    {
        ReleaseAll();
        _targets.Clear();
    }

    /// <summary>子窗口路由缓存需在窗口重建或尺寸变化后失效。</summary>
    public void InvalidateTargets() => _targets.Clear();

    private nint EnsureTarget() => _handle == nint.Zero ? throw InputException.NoTarget() : _handle;

    /// <summary>
    /// 键盘消息的落点：持有焦点的子控件（Scintilla、Edit 等）在自己的 WndProc 里消费
    /// <c>WM_CHAR</c> / <c>WM_KEYDOWN</c>，投给顶层窗口不会被转发，因此必须定位到子窗口。
    /// 目标线程即使不是前台，其线程局部输入状态里的 <c>hwndFocus</c> 仍然保留，且该查询是只读的。
    /// </summary>
    private nint KeyTarget()
    {
        var top = new HWND(EnsureTarget());
        var threadId = User32.GetWindowThreadProcessId(top, out _);
        var info = new User32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<User32.GUITHREADINFO>() };
        if (threadId != 0 && User32.GetGUIThreadInfo(threadId, ref info))
        {
            var focus = info.hwndFocus;
            if (!focus.IsNull && (focus == top || User32.IsChild(top, focus)))
            {
                return focus.DangerousGetHandle();
            }
        }

        // 拿不到焦点就退到客户区最大的可见叶子窗口；顶层窗口没有子窗口时这里返回顶层，
        // 正好适配自绘/引擎类单窗口应用。
        var fallback = LargestLeafChild(top);
        return fallback.IsNull ? top.DangerousGetHandle() : fallback.DangerousGetHandle();
    }

    /// <summary>回退策略：客户区面积最大的可见叶子窗口（工具栏、状态栏通常比编辑区小）。</summary>
    private static HWND LargestLeafChild(HWND top)
    {
        var best = HWND.NULL;
        var bestArea = 0L;

        User32.EnumChildWindows(top, (child, _) =>
        {
            if (!User32.IsWindowVisible(child) || !User32.GetWindow(child, User32.GetWindowCmd.GW_CHILD).IsNull)
            {
                return true;
            }

            if (User32.GetClientRect(child, out var rect))
            {
                var area = (long)rect.Width * rect.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = child;
                }
            }

            return true;
        }, IntPtr.Zero);

        return best;
    }

    /// <summary>
    /// 目标非前台时补发 <c>WM_ACTIVATE</c>。<c>WM_ACTIVATE</c> 是顶层窗口专属通知，子控件不处理它，
    /// 所以只在消息本身就投给顶层窗口（单窗口引擎应用）时才补。
    /// </summary>
    private void ActivateIfNeeded(nint target)
    {
        if (_options.BackgroundActivateHint && target == _handle && !_windows.IsForeground(_handle))
        {
            PostMessageSimulator.Activate(target);
        }
    }

    private void Dispatch(string key, Action<nint, User32.VK> action, bool down)
    {
        if (!KeyMap.TryParse(key, out var vk))
        {
            throw InputException.UnknownKey(key);
        }

        if (KeyMap.IsMouseKey(vk))
        {
            throw new InputException(
                "mouse_key_requires_position",
                $"后台模式下的鼠标键需要坐标，不能通过 {key} 直接按下",
                $"改用 click_absolute(x, y, button: \"{key}\")");
        }

        var target = KeyTarget();
        ActivateIfNeeded(target);
        action(target, vk);
        SetPressed(vk, down);
    }

    /// <summary>
    /// 解析该客户区坐标应投递到的 HWND：先找点处的子窗口，命中且属于目标窗口时投给它，
    /// 否则投给顶层窗口。结果按坐标缓存。
    /// </summary>
    private nint Prepare(int clientX, int clientY, out int x, out int y)
    {
        var target = ResolveTarget(clientX, clientY);

        // 路由到子窗口后必须换坐标系：子窗口的客户区原点通常不等于顶层窗口的，原样投递会让
        // 消息落在子窗口里偏离 (dx, dy) 的位置，表现出来像「点击偏移」或「没点中」。
        x = clientX;
        y = clientY;
        var screen = new POINT(clientX, clientY);
        if (target != _handle
            && User32.ClientToScreen(new HWND(_handle), ref screen)
            && User32.ScreenToClient(new HWND(target), ref screen))
        {
            x = screen.x;
            y = screen.y;
        }

        ActivateIfNeeded(target);
        return target;
    }

    private nint ResolveTarget(int clientX, int clientY)
    {
        var cacheKey = ((long)clientX << 32) | (uint)clientY;
        if (_targets.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var screen = new POINT(clientX, clientY);
        if (!User32.ClientToScreen(new HWND(_handle), ref screen))
        {
            return _handle;
        }

        var hit = User32.WindowFromPoint(screen);
        var target = _handle;
        if (hit != HWND.NULL)
        {
            var root = User32.GetAncestor(hit, User32.GetAncestorFlag.GA_ROOT);
            if (root == new HWND(_handle) && !hit.IsNull && hit != new HWND(_handle))
            {
                target = hit.DangerousGetHandle();
            }
        }

        if (_targets.Count >= TargetCacheLimit)
        {
            _targets.Clear();
        }

        _targets[cacheKey] = target;
        return target;
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
