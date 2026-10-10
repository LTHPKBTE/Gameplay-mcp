using System.Runtime.InteropServices;

namespace GameMcp.Windowing;

/// <summary>
/// 进程必须声明为 PerMonitorV2，且要在任何窗口/几何 API 之前调用。
/// 不感知 DPI 的进程读到的窗口和客户区几何会被系统按显示缩放虚拟化：本机 125% 下，
/// 一块真实 2560x1440 的无边框全屏窗口读出来是 2048x1152，而采集侧拿到的是真实物理像素，
/// 两者对不上会裁错区域、点错位置。DWM 的扩展边框属性不受虚拟化影响，所以只有它会和其余几何打架。
/// </summary>
public static class DpiAwareness
{
    private const nint PerMonitorAwareV2 = -4;

    public static void Enable()
    {
        SetProcessDpiAwarenessContext(PerMonitorAwareV2);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
