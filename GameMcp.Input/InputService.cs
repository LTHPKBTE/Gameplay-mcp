using GameMcp.Input.Win32;
using GameMcp.Windowing;

namespace GameMcp.Input;

/// <summary>
/// 输入门面：按当前模式选通道，在切换模式或解绑窗口时释放按下状态。
/// 所有坐标为客户区物理像素。
/// </summary>
public sealed class InputService : IInputService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IWindowService _windows;
    private readonly InputOptions _options;
    private readonly object _gate = new();
    private readonly PanicGate _panic = new();
    private readonly PanicHotkey? _hotkey;

    private nint _handle;
    private InputMode _mode;
    private PostMessageBackend? _background;
    private SendInputBackend? _foreground;

    public InputService(IWindowService windows, InputOptions options)
    {
        _windows = windows;
        _options = options;
        _mode = options.Mode;

        if (!string.IsNullOrWhiteSpace(options.PanicKey))
        {
            try
            {
                _hotkey = new PanicHotkey(_panic, options.PanicKey, ReleaseAll, () => { });
                PanicHotkeyError = _hotkey.Failure;
            }
            catch (FormatException e)
            {
                PanicHotkeyError = $"急停热键配置无效：{e.Message}";
            }
        }
    }

    public InputMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    public nint TargetHandle
    {
        get
        {
            lock (_gate)
            {
                return _handle;
            }
        }
    }

    public IInputBackend Backend
    {
        get
        {
            lock (_gate)
            {
                return Current();
            }
        }
    }

    public bool IsTargetForeground
    {
        get
        {
            lock (_gate)
            {
                return _handle != nint.Zero && _windows.IsForeground(_handle);
            }
        }
    }

    public PixelRect ClientRectOnScreen
    {
        get
        {
            lock (_gate)
            {
                return _handle == nint.Zero ? PixelRect.Empty : _windows.GetClientRectOnScreen(_handle);
            }
        }
    }

    public bool IsPanicTripped => _panic.IsTripped;

    public string PanicKey => _options.PanicKey;

    public bool IsPanicHotkeyActive => _hotkey?.IsActive == true;

    public string? PanicHotkeyError { get; }

    public CancellationToken PanicToken => _panic.Token;

    public IReadOnlyList<string> PressedKeys
    {
        get
        {
            lock (_gate)
            {
                if (_background is { } background && _foreground is { } foreground)
                {
                    return [.. background.PressedKeys, .. foreground.PressedKeys];
                }

                return _background?.PressedKeys ?? _foreground?.PressedKeys ?? [];
            }
        }
    }

    public void EnsureInputAllowed()
    {
        if (_panic.IsTripped)
        {
            throw InputException.StoppedByUser(_options.PanicKey);
        }
    }

    public void ReleaseAll()
    {
        lock (_gate)
        {
            _background?.ReleaseAll();
            _foreground?.ReleaseAll();
        }
    }

    public void Attach(nint handle)
    {
        lock (_gate)
        {
            _background?.Dispose();
            _foreground?.Dispose();
            _background = null;
            _foreground = null;
            _handle = handle;
        }
    }

    public void SwitchMode(InputMode mode)
    {
        lock (_gate)
        {
            if (_mode == mode)
            {
                return;
            }

            _background?.ReleaseAll();
            _foreground?.ReleaseAll();
            _mode = mode;
        }
    }

    public async Task<bool> WaitForForegroundAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (IsTargetForeground)
        {
            return true;
        }

        var infinite = timeout <= TimeSpan.Zero;
        var deadline = DateTime.UtcNow + (infinite ? TimeSpan.Zero : timeout);

        while (infinite || DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            if (IsTargetForeground)
            {
                return true;
            }
        }

        return IsTargetForeground;
    }

    public void Dispose()
    {
        _hotkey?.Dispose();

        lock (_gate)
        {
            _background?.Dispose();
            _foreground?.Dispose();
            _background = null;
            _foreground = null;
            _handle = nint.Zero;
        }
    }

    private IInputBackend Current()
    {
        if (_mode == InputMode.Foreground)
        {
            return _foreground ??= new SendInputBackend(_windows, _handle, _options);
        }

        return _background ??= new PostMessageBackend(_windows, _handle, _options);
    }
}
