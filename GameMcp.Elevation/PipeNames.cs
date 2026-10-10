namespace GameMcp.Elevation;

/// <summary>三条中继管道的名字，父进程与提权子进程各自按同一个 token 推出同样的名字。</summary>
internal sealed class PipeNames
{
    private const string Prefix = @"\\.\pipe\gamemcp-";

    private PipeNames(string token)
    {
        In = $"{Prefix}{token}-in";
        Out = $"{Prefix}{token}-out";
        Err = $"{Prefix}{token}-err";
    }

    public string In { get; }

    public string Out { get; }

    public string Err { get; }

    public static PipeNames For(string token) => new(token);
}
