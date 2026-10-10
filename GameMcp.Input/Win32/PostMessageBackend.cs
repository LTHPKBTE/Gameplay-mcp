using GameMcp.Windowing;
using Vanara.PInvoke;

namespace GameMcp.Input.Win32;

/// <summary>
/// 后台通道：向目标窗口投递合成消息，目标无需前台、真实光标不动。
/// 坐标即客户区坐标，不做换算。不支持相对移动、滚轮、中键、侧键。
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

        var target = Prepare(clientX, clientY);

        var times = Math.Max(1, count);
        for (var i = 0; i < times; i++)
        {
            PostMessageSimulator.ButtonDown(target, button, clientX, clientY);
            Thread.Sleep(10);
            PostMessageSimulator.ButtonUp(target, button, clientX, clientY);

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
        var hWnd = EnsureTarget();
        ActivateIfNeeded();

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
            PostMessageSimulator.KeyUp(_handle, vk);
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

    /// <summary>目标非前台时先补发 WM_ACTIVATE：它是兼容性补丁，不构成真正的窗口激活。</summary>
    private void ActivateIfNeeded()
    {
        if (_options.BackgroundActivateHint && !_windows.IsForeground(_handle))
        {
            PostMessageSimulator.Activate(EnsureTarget());
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

        ActivateIfNeeded();
        action(_handle, vk);
        SetPressed(vk, down);
    }

    /// <summary>
    /// 解析该客户区坐标应投递到的 HWND：先找点处的子窗口，命中且属于目标窗口时投给它，
    /// 否则投给顶层窗口。结果按坐标缓存。
    /// </summary>
    private nint Prepare(int clientX, int clientY)
    {
        var target = ResolveTarget(clientX, clientY);
        ActivateIfNeeded();
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
