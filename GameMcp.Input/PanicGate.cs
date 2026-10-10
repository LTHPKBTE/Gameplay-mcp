namespace GameMcp.Input;

/// <summary>
/// 用户急停闸门。触发后所有输入被拒绝，只有用户再次按下同一热键才能解除。
/// <see cref="Reset"/> 只由物理热键回调调用。
/// </summary>
public sealed class PanicGate
{
    private readonly object _gate = new();

    private CancellationTokenSource _cts = new();
    private bool _tripped;

    public bool IsTripped
    {
        get
        {
            lock (_gate)
            {
                return _tripped;
            }
        }
    }

    /// <summary>急停期间有效的中断信号；每次解除后换成新的。</summary>
    public CancellationToken Token
    {
        get
        {
            lock (_gate)
            {
                return _cts.Token;
            }
        }
    }

    /// <summary>触发急停，中断在途操作。已经是急停状态时返回 <c>false</c>。</summary>
    public bool Trip()
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_tripped)
            {
                return false;
            }

            _tripped = true;
            cts = _cts;
        }

        cts.Cancel();
        return true;
    }

    /// <summary>解除急停。本就未触发时返回 <c>false</c>。</summary>
    public bool Reset()
    {
        lock (_gate)
        {
            if (!_tripped)
            {
                return false;
            }

            _tripped = false;
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            return true;
        }
    }
}
