using GameMcp.Annotate;
using GameMcp.Capture;
using GameMcp.Elevation;
using GameMcp.Input;
using GameMcp.Overlay;
using GameMcp.Server.Services;
using GameMcp.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

// 非管理员且未给 --no-admin 时，这里会拉起提权副本并把 stdio 中继过去，控制权不再回来
var elevation = ElevationHost.Run(args);
if (elevation.Outcome == ElevationOutcome.Relayed)
{
    return elevation.ExitCode;
}

args = ElevationOptions.StripRuntimeFlags(args);

var builder = Host.CreateApplicationBuilder(args);

// stdout 只承载 JSON-RPC，日志一律走 stderr 与文件。
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "gamemcp-.log"),
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: false);

if (elevation.Notice is { } notice)
{
    Log.Warning("{Notice}", notice);
}

builder.Services.Configure<CaptureOptions>(builder.Configuration.GetSection("Capture"));
builder.Services.Configure<InputOptions>(builder.Configuration.GetSection("Input"));
builder.Services.Configure<AnnotateOptions>(builder.Configuration.GetSection("Annotate"));
builder.Services.Configure<OverlayOptions>(builder.Configuration.GetSection("Overlay"));

builder.Services.AddSingleton<IWindowService, WindowService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<ICaptureService>(sp => new CaptureService(
    sp.GetRequiredService<IWindowService>(),
    sp.GetRequiredService<IOptions<CaptureOptions>>().Value));
builder.Services.AddSingleton<IInputService>(sp => new InputService(
    sp.GetRequiredService<IWindowService>(),
    sp.GetRequiredService<IOptions<InputOptions>>().Value));

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

try
{
    var host = builder.Build();

    // 提前构造输入服务，让急停热键在第一次工具调用前就挂上系统
    var input = host.Services.GetRequiredService<IInputService>();
    if (!string.IsNullOrWhiteSpace(input.PanicKey) && !input.IsPanicHotkeyActive)
    {
        Log.Warning("急停热键未生效：{Reason}", input.PanicHotkeyError ?? "未知原因");
    }

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "服务异常退出");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
