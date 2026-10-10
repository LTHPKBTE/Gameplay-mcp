using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

namespace GameMcp.Windowing;

public sealed class WindowService : IWindowService
{
    private const int MaxTextLength = 512;

    public IReadOnlyList<WindowInfo> Enumerate()
    {
        var list = new List<WindowInfo>();
        User32.EnumWindows((hwnd, _) =>
        {
            var info = Describe(hwnd.DangerousGetHandle());
            if (info is not null)
            {
                list.Add(info);
            }

            return true;
        }, nint.Zero);
        return list;
    }

    public WindowInfo? Describe(nint handle)
    {
        var hwnd = ToHandle(handle);
        if (!User32.IsWindow(hwnd) || !User32.IsWindowVisible(hwnd))
        {
            return null;
        }

        var title = GetText(hwnd, User32.GetWindowText);
        var className = GetText(hwnd, User32.GetClassName);
        if (title.Length == 0 && className.Length == 0)
        {
            return null;
        }

        return new WindowInfo(
            handle,
            title,
            className,
            GetWindowRect(hwnd),
            IsVisible: true,
            IsMinimized: User32.IsIconic(hwnd),
            IsCloaked: IsCloaked(handle),
            IsForeground: IsForeground(handle),
            IsTopMost: IsTopMost(hwnd));
    }

    public WindowInfo? Foreground
    {
        get
        {
            var hwnd = User32.GetForegroundWindow();
            return hwnd.IsNull ? null : Describe(hwnd.DangerousGetHandle());
        }
    }

    public PixelRect GetClientRectOnScreen(nint handle)
    {
        var hwnd = ToHandle(handle);
        if (!User32.GetClientRect(hwnd, out var client))
        {
            return PixelRect.Empty;
        }

        var origin = new POINT { x = 0, y = 0 };
        if (!User32.ClientToScreen(hwnd, ref origin))
        {
            return PixelRect.Empty;
        }

        return ToPixelRect(origin.x, origin.y, client.Width, client.Height);
    }

    public unsafe PixelRect? GetExtendedFrameBounds(nint handle)
    {
        var hwnd = ToHandle(handle);
        RECT bounds;
        var hr = DwmApi.DwmGetWindowAttribute(
            hwnd,
            DwmApi.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            (nint)(&bounds),
            Marshal.SizeOf<RECT>());

        if (hr.Failed)
        {
            return null;
        }

        var rect = ToPixelRect(bounds.left, bounds.top, bounds.Width, bounds.Height);
        return rect.IsEmpty ? null : rect;
    }

    public uint GetDpi(nint handle)
    {
        var dpi = User32.GetDpiForWindow(ToHandle(handle));
        return dpi != 0 ? dpi : GetMonitorDpi(ToHandle(handle));
    }

    public bool IsMinimized(nint handle) => User32.IsIconic(ToHandle(handle));

    public unsafe bool IsCloaked(nint handle)
    {
        int cloaked = 0;

        // 用非泛型重载：Vanara 的泛型版本在属性不可用时会抛异常而不是返回失败码
        var hr = DwmApi.DwmGetWindowAttribute(
            ToHandle(handle),
            DwmApi.DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            (nint)(&cloaked),
            sizeof(int));

        return hr.Succeeded && cloaked != 0;
    }

    public bool IsForeground(nint handle) => User32.GetForegroundWindow() == ToHandle(handle);

    public bool Focus(nint handle)
    {
        var hwnd = ToHandle(handle);
        if (User32.IsIconic(hwnd))
        {
            User32.ShowWindow(hwnd, ShowWindowCommand.SW_RESTORE);
        }

        User32.SetForegroundWindow(hwnd);
        User32.BringWindowToTop(hwnd);
        return IsForeground(handle);
    }

    private static uint GetMonitorDpi(HWND hwnd)
    {
        var monitor = User32.MonitorFromWindow(hwnd, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
        if (monitor.IsNull)
        {
            return 96;
        }

        if (SHCore.GetDpiForMonitor(monitor, SHCore.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Failed)
        {
            return 96;
        }

        return dpiX;
    }

    private static bool IsTopMost(HWND hwnd)
        => (User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE) & (int)User32.WindowStylesEx.WS_EX_TOPMOST) != 0;

    private static PixelRect GetWindowRect(HWND hwnd)
    {
        if (!User32.GetWindowRect(hwnd, out var rect))
        {
            return PixelRect.Empty;
        }

        return ToPixelRect(rect.left, rect.top, rect.Width, rect.Height);
    }

    private static PixelRect ToPixelRect(int x, int y, int width, int height)
        => width <= 0 || height <= 0 ? PixelRect.Empty : new PixelRect(x, y, width, height);

    private static string GetText(HWND hwnd, Func<HWND, StringBuilder, int, int> read)
    {
        var buffer = new StringBuilder(MaxTextLength);
        var length = read(hwnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : string.Empty;
    }

    private static HWND ToHandle(nint handle) => new((IntPtr)handle);
}
