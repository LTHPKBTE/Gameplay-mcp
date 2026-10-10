using System.Text;
using GameMcp.Elevation;
using GameMcp.Tools;
using GameMcp.Windowing;

var elevation = ElevationHost.Run(args);
if (elevation.Outcome == ElevationOutcome.Relayed)
{
    return elevation.ExitCode;
}

if (elevation.Notice is { } notice)
{
    Console.Error.WriteLine(notice);
}

args = ElevationOptions.StripRuntimeFlags(args);

// 终端里要把代码页切成 65001 才能正确显示中文；stdout 已经是管道（含提权中继）时改代码页会失败，
// 而 .NET 对重定向流本来就按 UTF-8 编码
if (!Console.IsOutputRedirected)
{
    Console.OutputEncoding = Encoding.UTF8;
}

IWindowService windows = new WindowService();
var command = args.Length == 0 ? "windows" : args[0];
var rest = args.Length == 0 ? [] : args[1..];

try
{
    return command switch
    {
        "windows" or "-w" => WindowCommand.Run(windows, rest),
        "client" or "-c" => WindowCommand.RunClient(windows, rest),
        "capture" or "-cap" => CaptureCommand.Run(windows, rest),
        "modes" => CaptureCommand.RunModes(),
        "permission" or "-p" => PermissionCommand.Run(elevation),
        "-h" or "--help" or "help" => Usage(),
        _ => Unknown(command),
    };
}
catch (Exception e)
{
    Console.Error.WriteLine($"失败：{e.Message}");
    return 1;
}

static int Usage()
{
    Console.WriteLine("""
        用法:
          GameMcp.Tools                              列出可见顶层窗口
          GameMcp.Tools client <HWND>                打印窗口几何与 DPI
          GameMcp.Tools modes                        列出采集后端
          GameMcp.Tools permission                   打印当前权限状态
          GameMcp.Tools capture [选项]               采集并落盘

        capture 选项:
          --handle 0x1234        目标窗口句柄
          --title <子串>         按标题子串选窗口（与 --handle 二选一）
          --mode <名称>          采集后端，默认 GraphicsCapture
          --canvas 1920          基准画布长边，0 表示不缩放
          --format png|webp      落盘格式，默认 png
          --quality 90           WebP 质量
          --interval 100         帧间隔毫秒
          --duration 0           持续秒数；0 表示采一帧
          --count 0              最大帧数；0 表示不限
          --out shots            输出目录
          --save-every 0         每 N 帧落盘一张；0 表示只存最后一帧
          --report-every 30      诊断上报间隔秒
          --first-timeout 5      等待首帧的秒数
          --no-save              不落盘
        """);
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"未知命令：{command}");
    return Usage();
}
