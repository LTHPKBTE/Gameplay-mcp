using GameMcp.Elevation;

namespace GameMcp.Server.Services;

/// <summary>
/// 启动期确定、运行期不变的信息，供工具层读取。
/// <see cref="ElevationResult"/> 是结构体，装一层才能作为单例注入。
/// </summary>
public sealed record HostState(ElevationResult Elevation);
