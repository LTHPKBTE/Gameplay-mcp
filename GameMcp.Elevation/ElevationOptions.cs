namespace GameMcp.Elevation;

/// <summary>提权相关的命令行开关。</summary>
public sealed class ElevationOptions
{
    /// <summary>跳过提权请求，就地以当前权限运行。</summary>
    public const string SkipFlag = "--no-admin";

    public const string RelayPrefix = "--elevation-relay=";
    public const string ParentPrefix = "--elevation-parent=";

    private ElevationOptions()
    {
    }

    public bool Skip { get; private init; }

    /// <summary>本进程是父进程拉起的提权子进程时，中继 token。</summary>
    public string? RelayToken { get; private init; }

    /// <summary>父进程 PID，用来在中继断开时自我了断。</summary>
    public int ParentProcessId { get; private init; }

    public bool IsRelayedChild => RelayToken is not null;

    public static ElevationOptions Parse(string[] args)
    {
        var skip = false;
        string? token = null;
        var parent = 0;

        foreach (var arg in args)
        {
            if (arg == SkipFlag)
            {
                skip = true;
            }
            else if (arg.StartsWith(RelayPrefix, StringComparison.Ordinal))
            {
                token = arg[RelayPrefix.Length..];
            }
            else if (arg.StartsWith(ParentPrefix, StringComparison.Ordinal)
                     && int.TryParse(arg[ParentPrefix.Length..], out var pid))
            {
                parent = pid;
            }
        }

        return new ElevationOptions
        {
            Skip = skip,
            RelayToken = token,
            ParentProcessId = parent,
        };
    }

    /// <summary>去掉提权层自己的开关与中继参数，交给应用层解析。</summary>
    public static string[] StripRuntimeFlags(string[] args)
        => args.Where(arg => arg != SkipFlag
                             && !arg.StartsWith(RelayPrefix, StringComparison.Ordinal)
                             && !arg.StartsWith(ParentPrefix, StringComparison.Ordinal))
            .ToArray();
}
