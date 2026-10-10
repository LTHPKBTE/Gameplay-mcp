using System.Diagnostics;
using System.Text;

namespace GameMcp.Elevation;

/// <summary>
/// 非管理员进程这一侧：用 <c>runas</c> 拉起自己的提权副本，再把 stdin/stdout/stderr 与三条命名管道对接。
/// 子进程的存活由子进程自己盯父进程的进程句柄。
/// </summary>
internal static class ParentRelay
{
    private const int BufferSize = 64 * 1024;

    public static ElevationResult Run(string[] args)
    {
        var token = Guid.NewGuid().ToString("N");
        var names = PipeNames.For(token);

        using var down = new PipeServer(names.In);
        using var up = new PipeServer(names.Out);
        using var err = new PipeServer(names.Err);

        var startInfo = BuildStartInfo(args, token);
        if (startInfo is null)
        {
            return new ElevationResult(
                ElevationOutcome.Continue,
                0,
                ElevationStatus.LaunchFailed,
                "无法确定自身可执行文件路径");
        }

        Console.Error.WriteLine("[gamemcp] 正在请求管理员权限，请在 UAC 弹窗上点「是」…");

        // 子进程吐的是 UTF-8 字节，但提权子进程没有自己的控制台，改不了这里的代码页，
        // 所以得由父进程把控制台切到 65001，否则转发出来的中文会是乱码
        if (!Console.IsOutputRedirected)
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        var launch = new ElevationLaunch(startInfo);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => launch.Abandon();

        // 用户可能在 UAC 上想很久，但 MCP 客户端不会等，握手时间必须有上限
        var deadline = Environment.TickCount64 + (long)ElevationHost.HandshakeWait.TotalMilliseconds;
        var started = launch.Wait(Remaining(deadline));

        if (started != LaunchResult.Started)
        {
            launch.Abandon();
            return new ElevationResult(
                ElevationOutcome.Continue,
                0,
                started switch
                {
                    LaunchResult.Declined => ElevationStatus.UserDeclined,
                    LaunchResult.Abandoned => ElevationStatus.PromptTimeout,
                    _ => ElevationStatus.LaunchFailed,
                },
                launch.Error);
        }

        var child = launch.Process!;

        try
        {
            using var downStream = down.Connect(Remaining(deadline));
            using var upStream = up.Connect(Remaining(deadline));
            using var errStream = err.Connect(Remaining(deadline));

            var threads = new List<Thread>(3);

            // 交互式运行时 stdin 是控制台，没有转发的意义，也不该把按键喂给子进程
            if (Console.IsInputRedirected)
            {
                threads.Add(Pump(Console.OpenStandardInput(), downStream, closeTarget: true));
            }

            threads.Add(Pump(upStream, Console.OpenStandardOutput()));
            threads.Add(Pump(errStream, Console.OpenStandardError()));

            child.WaitForExit();

            foreach (var thread in threads)
            {
                thread.Join(TimeSpan.FromSeconds(5));
            }

            return new ElevationResult(
                ElevationOutcome.Relayed,
                child.ExitCode,
                ElevationStatus.RelayedParent,
                null);
        }
        catch (TimeoutException e)
        {
            launch.Abandon();
            return new ElevationResult(ElevationOutcome.Continue, 0, ElevationStatus.RelayFailed, e.Message);
        }
    }

    private static TimeSpan Remaining(long deadline)
    {
        var left = deadline - Environment.TickCount64;
        return TimeSpan.FromMilliseconds(left > 0 ? left : 0);
    }

    private static ProcessStartInfo? BuildStartInfo(string[] args, string token)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return null;
        }

        // dotnet run / dotnet app.dll 启动时 ProcessPath 是 dotnet.exe，要显式带上程序集路径
        var hosted = Path.GetFileNameWithoutExtension(executable)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        var command = new List<string>();
        if (hosted)
        {
            command.Add(Quote(Environment.GetCommandLineArgs()[0]));
        }

        command.AddRange(args.Select(Quote));
        command.Add($"{ElevationOptions.RelayPrefix}{token}");
        command.Add($"{ElevationOptions.ParentPrefix}{Environment.ProcessId}");

        return new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(' ', command),
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
    }

    private static Thread Pump(Stream from, Stream to, bool closeTarget = false)
    {
        var thread = new Thread(() =>
        {
            var buffer = new byte[BufferSize];
            try
            {
                int read;
                while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
                {
                    to.Write(buffer, 0, read);

                    // 写端不能缓冲，否则 MCP 的一问一答会卡在缓冲区里
                    to.Flush();
                }
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                if (closeTarget)
                {
                    // 关掉这一端，子进程读到 EOF，于是自行收尾退出
                    to.Dispose();
                }
            }
        })
        {
            IsBackground = true,
            Name = "GameMcp.Elevation.Pump",
        };

        thread.Start();
        return thread;
    }

    private static string Quote(string argument)
        => argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
}
