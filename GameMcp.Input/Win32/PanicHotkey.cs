using Fischless.HotkeyCapture;
using Vanara.PInvoke;

namespace GameMcp.Input.Win32;
/// <summary>
/// 全局急停热键。走系统 <c>RegisterHotKey</c>（带 <c>MOD_NOREPEAT</c>），专用 STA 线程跑消息泵。
/// <para>
/// 不用低级键盘钩子：按文档，钩子回调由系统投递到安装钩子的线程上，本进程一忙就应答不上。
/// </para>
/// 参考：https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc
/// </summary>
internal sealed class PanicHotkey : IDisposable
{
    private readonly PanicGate _gate;
    private readonly Action _onTrip;
    private readonly Action _onReset;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _stop = new(false);

    private string? _failure;

    /// <summary>是否已成功注册。为 <c>false</c> 时急停不可用。</summary>
    public bool IsActive { get; private set; }

    public string? Failure => _failure;

    /// <param name="spec">形如 <c>ctrl+alt+q</c>。必须带至少一个修饰键，否则会与正常输入冲突。</param>
    public PanicHotkey(PanicGate gate, string spec, Action onTrip, Action onReset)
    {
        _gate = gate;
        _onTrip = onTrip;
        _onReset = onReset;
        var combo = Parse(spec);

        _thread = new Thread(() => Pump(combo))
        {
            IsBackground = true,
            Name = "GameMcp.PanicHotkey",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(3)))
        {
            _failure = "注册急停热键超时";
        }
    }

    public void Dispose()
    {
        _stop.Set();
        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _stop.Dispose();
        _ready.Dispose();
    }

    /// <summary>把 <c>ctrl+alt+q</c> 这类写法解析为主键与修饰键。</summary>
    public static (System.Windows.Forms.Keys Key, User32.HotKeyModifiers Modifier) Parse(string spec)
    {
        User32.VK? main = null;
        var modifier = User32.HotKeyModifiers.MOD_NONE;

        foreach (var part in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!KeyMap.TryParse(part, out var vk))
            {
                throw new FormatException($"无法识别的键名：{part}");
            }

            switch (vk)
            {
                case User32.VK.VK_CONTROL or User32.VK.VK_LCONTROL or User32.VK.VK_RCONTROL:
                    modifier |= User32.HotKeyModifiers.MOD_CONTROL;
                    continue;
                case User32.VK.VK_MENU or User32.VK.VK_LMENU or User32.VK.VK_RMENU:
                    modifier |= User32.HotKeyModifiers.MOD_ALT;
                    continue;
                case User32.VK.VK_SHIFT or User32.VK.VK_LSHIFT or User32.VK.VK_RSHIFT:
                    modifier |= User32.HotKeyModifiers.MOD_SHIFT;
                    continue;
                case User32.VK.VK_LWIN or User32.VK.VK_RWIN:
                    modifier |= User32.HotKeyModifiers.MOD_WIN;
                    continue;
            }

            if (main is not null)
            {
                throw new FormatException("急停热键只能有一个主键");
            }

            main = vk;
        }

        if (main is null)
        {
            throw new FormatException("急停热键缺少主键");
        }

        if (modifier == User32.HotKeyModifiers.MOD_NONE)
        {
            throw new FormatException("急停热键至少要带一个修饰键，否则会与正常输入冲突");
        }

        return ((System.Windows.Forms.Keys)(int)main.Value, modifier | User32.HotKeyModifiers.MOD_NOREPEAT);
    }

    private void Pump((System.Windows.Forms.Keys Key, User32.HotKeyModifiers Modifier) combo)
    {
        HotkeyHook? hook = null;
        try
        {
            hook = new HotkeyHook();
            hook.KeyPressed += (_, _) => Toggle();
            hook.RegisterHotKey(combo.Modifier, combo.Key);
            IsActive = true;
        }
        catch (Exception e)
        {
            _failure = $"注册 {combo.Modifier} + {combo.Key} 失败：{e.Message}";
            hook?.Dispose();
            _ready.Set();
            return;
        }

        _ready.Set();

        // HotkeyHook 用的是窗口消息，必须有消息泵；MOD_NOREPEAT 已挡住长按重复
        while (!_stop.IsSet)
        {
            while (User32.PeekMessage(out var msg, HWND.NULL, 0, 0, User32.PM.PM_REMOVE))
            {
                User32.TranslateMessage(msg);
                User32.DispatchMessage(msg);
            }

            _stop.Wait(16);
        }

        hook.Dispose();
        IsActive = false;
    }

    private void Toggle()
    {
        if (!_gate.IsTripped)
        {
            if (_gate.Trip())
            {
                _onTrip();
            }

            return;
        }

        if (_gate.Reset())
        {
            _onReset();
        }
    }
}
