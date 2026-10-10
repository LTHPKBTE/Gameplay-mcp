using System.Globalization;
using GameMcp.Windowing;

namespace GameMcp.Tools;

internal static class WindowCommand
{
    public static int Run(IWindowService windows, string[] args)
    {
        var filter = args.Length > 0 ? args[0] : null;

        Console.WriteLine($"{"HWND",-12} {"DPI",-5} {"客户区",-24} {"窗口",-24} 类名 / 标题");
        foreach (var window in windows.Enumerate())
        {
            if (!string.IsNullOrEmpty(filter)
                && !window.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !window.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.WriteLine(
                $"{window.Handle:X12} {windows.GetDpi(window.Handle),-5} {Format(windows.GetClientRectOnScreen(window.Handle)),-24} {Format(window.WindowRect),-24} {window.ClassName} / {window.Title}{Flags(window)}");
        }

        return 0;
    }

    private static string Flags(WindowInfo window)
    {
        var flags = new List<string>(3);
        if (window.IsMinimized)
        {
            flags.Add("最小化");
        }

        if (window.IsCloaked)
        {
            flags.Add("DWM 遮蔽");
        }

        if (window.IsTopMost)
        {
            flags.Add("置顶");
        }

        return flags.Count == 0 ? string.Empty : $"  [{string.Join(' ', flags)}]";
    }

    public static int RunClient(IWindowService windows, string[] args)
    {
        if (args.Length < 1 || !TryParseHandle(args[0], out var handle))
        {
            Console.Error.WriteLine("用法: GameMcp.Tools client <HWND>");
            return 2;
        }

        var info = windows.Describe(handle);
        if (info is null)
        {
            Console.Error.WriteLine($"窗口 {handle:X} 不存在或不可见");
            return 1;
        }

        Console.WriteLine($"标题        {info.Title}");
        Console.WriteLine($"类名        {info.ClassName}");
        Console.WriteLine($"窗口矩形    {Format(info.WindowRect)}");
        var frame = windows.GetExtendedFrameBounds(handle);
        Console.WriteLine($"DWM 边框    {(frame is null ? "(获取失败)" : Format(frame.Value))}");
        Console.WriteLine($"客户区      {Format(windows.GetClientRectOnScreen(handle))}");
        Console.WriteLine($"DPI         {windows.GetDpi(handle)}");
        Console.WriteLine($"最小化      {info.IsMinimized}");
        Console.WriteLine($"DWM 遮蔽    {info.IsCloaked}");
        Console.WriteLine($"置顶        {info.IsTopMost}");
        Console.WriteLine($"前台        {info.IsForeground}");
        return 0;
    }

    public static string Format(PixelRect rect)
        => rect.IsEmpty ? "-" : $"{rect.X},{rect.Y} {rect.Width}x{rect.Height}";

    /// <summary>句柄可以是 <c>0x1234</c> 或十进制。</summary>
    public static bool TryParseHandle(string text, out nint handle)
    {
        var span = text.AsSpan().Trim();
        if (span.StartsWith("0x") || span.StartsWith("0X"))
        {
            return nint.TryParse(span[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out handle);
        }

        return nint.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out handle);
    }

    /// <summary>按标题子串选窗口；最小化与被 DWM 遮蔽的排到最后。</summary>
    public static nint FindByTitle(IWindowService windows, string titleSubstring)
    {
        var candidates = windows.Enumerate()
            .Where(w => w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
            .OrderBy(w => w.IsCloaked)
            .ThenBy(w => w.IsMinimized)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"没有标题包含「{titleSubstring}」的窗口");
        }

        return candidates[0].Handle;
    }
}
