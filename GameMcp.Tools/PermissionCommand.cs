using GameMcp.Elevation;

namespace GameMcp.Tools;

/// <summary>打印当前进程的权限状态，用来确认提权与中继是否生效。</summary>
internal static class PermissionCommand
{
    public static int Run(ElevationResult elevation)
    {
        Console.WriteLine($"权限        {(ElevationState.IsElevated ? "管理员" : "非管理员")}");
        Console.WriteLine($"用户 SID    {ElevationState.CurrentUserSid ?? "(取不到)"}");
        Console.WriteLine($"进程        {Environment.ProcessId}");
        Console.WriteLine($"提权状态    {elevation.Status}");

        return 0;
    }
}
