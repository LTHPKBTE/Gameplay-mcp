using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Fischless.GameCapture;
using GameMcp.Capture;
using GameMcp.Windowing;

namespace GameMcp.Tools;

internal static class CaptureCommand
{
    private const uint GR_GDIOBJECTS = 0;
    private const uint GR_USEROBJECTS = 1;

    public static int RunModes()
    {
        foreach (var backend in Enum.GetValues<CaptureBackend>())
        {
            Console.WriteLine($"{backend}");
        }

        return 0;
    }

    public static int Run(IWindowService windows, string[] args)
    {
        var options = CliOptions.Parse(args);
        var handle = options.Handle is { } explicitHandle
            ? explicitHandle
            : WindowCommand.FindByTitle(windows, options.Title!);

        var info = windows.Describe(handle)
            ?? throw new InvalidOperationException($"窗口 0x{handle:X} 不存在或不可见");

        using var service = new CaptureService(windows, new CaptureOptions
        {
            Backend = options.Backend,
            CanvasMax = options.CanvasMax,
            Format = options.Format,
            WebPQuality = options.Quality,
            MinUpdateIntervalMs = options.Interval,
        });

        Console.WriteLine($"窗口        {HandleText.Format(handle)} {info.Title}");
        Console.WriteLine($"类名        {info.ClassName}");
        Console.WriteLine($"捕获后端    {options.Backend}{(service.IsHdrActive ? "（HDR）" : string.Empty)}");
        Console.WriteLine($"客户区      {WindowCommand.Format(windows.GetClientRectOnScreen(handle))}（物理像素）");
        Console.WriteLine($"最小化      {info.IsMinimized}   DWM 遮蔽 {info.IsCloaked}");

        var directory = Path.GetFullPath(options.Out);
        if (options.Save)
        {
            Directory.CreateDirectory(directory);
        }

        using var process = Process.GetCurrentProcess();
        var baselineManaged = GC.GetTotalMemory(false);
        var baselinePrivate = process.PrivateMemorySize64;
        var baselineGdi = GuiResources(process.Handle, GR_GDIOBJECTS);
        var baselineUser = GuiResources(process.Handle, GR_USEROBJECTS);

        service.Attach(handle);

        var started = Stopwatch.StartNew();
        var target = options.Count > 0
            ? options.Count
            : options.Duration > 0 ? int.MaxValue : 1;
        var frames = 0;
        var saved = 0;
        var lastReport = TimeSpan.Zero;
        string? lastFile = null;
        CaptureResult? last = null;

        while (true)
        {
            var result = service.Capture();

            if (result is not null)
            {
                frames++;

                if (options.Save && options.SaveEvery > 0 && frames % options.SaveEvery == 0)
                {
                    lastFile = Save(result, directory, options, frames);
                    saved++;
                }

                last?.Dispose();
                last = result;
            }

            var elapsed = started.Elapsed;
            if (elapsed - lastReport >= TimeSpan.FromSeconds(options.ReportEvery))
            {
                lastReport = elapsed;
                Report(service, process, frames, saved, elapsed, lastFile,
                    baselineManaged, baselinePrivate, baselineGdi, baselineUser);
            }

            if (frames >= target || (options.Duration > 0 && elapsed >= TimeSpan.FromSeconds(options.Duration)))
            {
                break;
            }

            if (frames == 0 && elapsed >= TimeSpan.FromSeconds(options.FirstTimeout))
            {
                Console.Error.WriteLine($"等待首帧超时（{options.FirstTimeout}s），未取到任何帧");
                return 1;
            }

            Thread.Sleep(options.Interval);
        }

        if (options.Save && last is not null && (options.SaveEvery <= 0 || frames % options.SaveEvery != 0))
        {
            lastFile = Save(last, directory, options, frames);
            saved++;
        }

        last?.Dispose();

        Console.WriteLine();
        Report(service, process, frames, saved, started.Elapsed, lastFile,
            baselineManaged, baselinePrivate, baselineGdi, baselineUser);
        Console.WriteLine($"结束时客户区 {WindowCommand.Format(windows.GetClientRectOnScreen(handle))}");
        Console.WriteLine($"结束时窗口   {WindowCommand.Format(windows.Describe(handle)?.WindowRect ?? PixelRect.Empty)}");
        return 0;
    }

    private static string Save(CaptureResult result, string directory, CliOptions options, int frames)
    {
        var path = Path.Combine(
            directory,
            $"shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{frames.ToString("D5", CultureInfo.InvariantCulture)}.{options.Extension}");

        File.WriteAllBytes(path, ImageEncoder.Encode(result.Bgr, options.Format, options.Quality));
        return path;
    }

    private static void Report(
        CaptureService service,
        Process process,
        int frames,
        int saved,
        TimeSpan elapsed,
        string? lastFile,
        long baselineManaged,
        long baselinePrivate,
        uint baselineGdi,
        uint baselineUser)
    {
        var d = service.Diagnostics;
        process.Refresh();

        Console.WriteLine(
            $"[{elapsed.TotalSeconds,7:0.0}s] 帧 {frames}  空 {d.EmptyFrames}  裁剪 {d.Crops}  缩放 {d.Resizes}  " +
            $"未裁剪回退 {d.UncroppedFallbacks}  几何不一致 {d.GeometryMismatches}  已遮蔽 {d.CloakedFrames}  " +
            $"平均 {d.AverageTotalMs:0.00}ms  最近 {d.LastTotalMs:0.00}ms");

        Console.WriteLine(
            $"{"",10} 工作集 {Delta(process.PrivateMemorySize64 - baselinePrivate)}" +
            $"  托管 {Delta(GC.GetTotalMemory(false) - baselineManaged)}" +
            $"  GDI {Signed(GuiResources(process.Handle, GR_GDIOBJECTS) - baselineGdi)}" +
            $"  USER {Signed(GuiResources(process.Handle, GR_USEROBJECTS) - baselineUser)}" +
            $"  落盘 {saved}");

        if (d.LastError is not null)
        {
            Console.WriteLine($"{"",10} 最近错误 {d.LastError}");
        }

        if (lastFile is not null)
        {
            Console.WriteLine($"{"",10} {lastFile}  {new FileInfo(lastFile).Length / 1024.0:0.0} KB");
        }
    }

    private static string Delta(long bytes)
        => $"{(bytes >= 0 ? "+" : "-")}{Math.Abs(bytes) / 1024.0 / 1024.0:0.0}MB";

    private static string Signed(long value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>进程当前持有的 GDI / USER 对象数，用来发现句柄泄漏。</summary>
    private static uint GuiResources(nint process, uint flags) => GetGuiResources(process, flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetGuiResources(nint process, uint flags);

    private sealed class CliOptions
    {
        public nint? Handle { get; private init; }

        public string? Title { get; private init; }

        public CaptureBackend Backend { get; private init; } = CaptureBackend.GraphicsCapture;

        public int CanvasMax { get; private init; } = 1920;

        public CaptureImageFormat Format { get; private init; } = CaptureImageFormat.Png;

        public int Quality { get; private init; } = 90;

        public int Interval { get; private init; } = 100;

        public int Duration { get; private init; }

        public int Count { get; private init; }

        public string Out { get; private init; } = "shots";

        public int SaveEvery { get; private init; }

        public int ReportEvery { get; private init; } = 30;

        public int FirstTimeout { get; private init; } = 5;

        public bool Save { get; private init; } = true;

        public string Extension => Format == CaptureImageFormat.WebP ? "webp" : "png";

        public static CliOptions Parse(string[] args)
        {
            nint? handle = null;
            string? title = null;
            var backend = CaptureBackend.GraphicsCapture;
            var canvas = 1920;
            var format = CaptureImageFormat.Png;
            var quality = 90;
            var interval = 100;
            var duration = 0;
            var count = 0;
            var outDir = "shots";
            var saveEvery = 0;
            var reportEvery = 30;
            var firstTimeout = 5;
            var save = true;

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];

                string Next()
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new InvalidOperationException($"{key} 缺少参数");
                    }

                    return args[++i];
                }

                switch (key)
                {
                    case "--handle":
                        if (!WindowCommand.TryParseHandle(Next(), out var parsed))
                        {
                            throw new InvalidOperationException("无法解析 --handle");
                        }

                        handle = parsed;
                        break;
                    case "--title":
                        title = Next();
                        break;
                    case "--mode":
                        backend = Enum.Parse<CaptureBackend>(Next(), ignoreCase: true);
                        break;
                    case "--canvas":
                        canvas = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--format":
                        format = Enum.Parse<CaptureImageFormat>(Next(), ignoreCase: true);
                        break;
                    case "--quality":
                        quality = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--interval":
                        interval = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--duration":
                        duration = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--count":
                        count = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--out":
                        outDir = Next();
                        break;
                    case "--save-every":
                        saveEvery = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--report-every":
                        reportEvery = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--first-timeout":
                        firstTimeout = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--no-save":
                        save = false;
                        break;
                    default:
                        throw new InvalidOperationException($"未知参数：{key}");
                }
            }

            if (handle is null && title is null)
            {
                throw new InvalidOperationException("需要 --handle 或 --title");
            }

            return new CliOptions
            {
                Handle = handle,
                Title = title,
                Backend = backend,
                CanvasMax = canvas,
                Format = format,
                Quality = quality,
                Interval = Math.Max(0, interval),
                Duration = Math.Max(0, duration),
                Count = Math.Max(0, count),
                Out = outDir,
                SaveEvery = Math.Max(0, saveEvery),
                ReportEvery = Math.Max(1, reportEvery),
                FirstTimeout = Math.Max(1, firstTimeout),
                Save = save,
            };
        }
    }
}
