using System.Runtime.InteropServices;
using System.Text;
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

    private const int MaxClassNameLength = 256;

    private const int WsTabStop = 0x00010000;

    /// <summary>典型可输入控件类名，命中即可作为键盘落点。</summary>
    private static readonly HashSet<string> InputClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scintilla",
        "Edit",
        "AVL_AVView",
    };

    /// <summary>只展示、不接受键盘输入的类名，按样式筛选时排除。</summary>
    private static readonly HashSet<string> DisplayClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Static",
        "SysHeader32",
    };

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
    /// 键盘消息的落点。持有焦点的子控件在自己的 WndProc 里消费 <c>WM_CHAR</c> / <c>WM_KEYDOWN</c>，
    /// 投给顶层窗口不会被转发到子控件。按四级顺序选：线程焦点 → 可输入类名 → <c>WS_TABSTOP</c>
    /// → 最大可见叶子窗口；都没有就用顶层窗口本身（自绘/单窗口应用）。
    /// </summary>
    private nint KeyTarget()
    {
        var top = new HWND(EnsureTarget());

        // 一级：线程局部输入状态记录的焦点控件。目标非前台时该值仍然保留，查询是只读的。
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

        var children = VisibleChildren(top);
        var pick = Largest(children.Where(c => IsInputClass(c.ClassName)));
        if (pick.IsNull)
        {
            pick = Largest(children.Where(c => c.TabStop && !DisplayClasses.Contains(c.ClassName)));
        }

        if (pick.IsNull)
        {
            pick = Largest(children.Where(c => c.Leaf));
        }

        return pick.IsNull ? top.DangerousGetHandle() : pick.DangerousGetHandle();
    }

    /// <summary>一次枚举收集所有可见且启用的子窗口，供三级回退共用。</summary>
    private static List<ChildWindow> VisibleChildren(HWND top)
    {
        var list = new List<ChildWindow>();

        User32.EnumChildWindows(top, (child, _) =>
        {
            if (!User32.IsWindowVisible(child) || !User32.IsWindowEnabled(child))
            {
                return true;
            }

            User32.GetClientRect(child, out var rect);
            var buffer = new StringBuilder(MaxClassNameLength);
            var length = User32.GetClassName(child, buffer, buffer.Capacity);
            var style = User32.GetWindowLong(child, User32.WindowLongFlags.GWL_STYLE);

            list.Add(new ChildWindow(
                child,
                length > 0 ? buffer.ToString() : string.Empty,
                (style & WsTabStop) != 0,
                User32.GetWindow(child, User32.GetWindowCmd.GW_CHILD).IsNull,
                (long)rect.Width * rect.Height));

            return true;
        }, IntPtr.Zero);

        return list;
    }

    private static HWND Largest(IEnumerable<ChildWindow> items)
    {
        var best = HWND.NULL;
        var bestArea = -1L;
        foreach (var item in items)
        {
            if (item.Area > bestArea)
            {
                bestArea = item.Area;
                best = item.Handle;
            }
        }

        return best;
    }

    private static bool IsInputClass(string className) =>
        InputClasses.Contains(className)
        || className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase);

    private readonly record struct ChildWindow(HWND Handle, string ClassName, bool TabStop, bool Leaf, long Area);

    /// <summary>
    /// 目标非前台时补发 <c>WM_ACTIVATE</c>。<c>WM_ACTIVATE</c> 是顶层窗口专属通知，子控件不处理它，
    /// 所以只在消息本身就投给顶层窗口（单窗口引擎应用）时才补。实测跨进程投递不改系统前景与 z 序，
    /// 只让目标线程把焦点设到自己的输入控件。
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
