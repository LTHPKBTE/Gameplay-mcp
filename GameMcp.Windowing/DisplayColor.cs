using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace GameMcp.Windowing;

/// <summary>
/// 目标显示器的高级色彩（Windows HDR）状态与 SDR 参考白电平。
/// <para>
/// HDR 打开时桌面以 scRGB 线性合成，规定 1.0 = 80 nit；SDR 内容被抬到「SDR 内容亮度」滑块对应的电平
/// （该值即 <see cref="SdrWhiteLevel"/>/1000*80 nit）。因此把 scRGB 值还原成 SDR 要乘 1000/<see cref="SdrWhiteLevel"/>。
/// </para>
/// </summary>
public sealed record DisplayColor(bool AdvancedColorEnabled, uint SdrWhiteLevel)
{
    /// <summary>SDR 白电平读不到时的系数，等价于 SDRWhiteLevel = 4000（320 nit）。</summary>
    public const float DefaultSdrScale = 0.25f;

    /// <summary>SDR 内容亮度（nit）。</summary>
    public float SdrWhiteLevelNits => SdrWhiteLevel / 1000f * 80f;

    /// <summary>scRGB 线性值退回 SDR 的系数。滑块范围 80～480 nit 对应 1.0～0.1667。</summary>
    public float SdrScale => SdrWhiteLevel > 0 ? 1000f / SdrWhiteLevel : DefaultSdrScale;
}

/// <summary>读取窗口所在显示器的色彩状态。走 <c>QueryDisplayConfig</c>，不需要切屏或改系统设置。</summary>
public static class DisplayColorReader
{
    /// <summary>查询失败（显示器名匹配不上、或系统不支持该接口）时返回 <c>null</c>。</summary>
    public static DisplayColor? Query(nint windowHandle)
    {
        var device = MonitorDevice(windowHandle);
        if (device is null)
        {
            return null;
        }

        if (User32.GetDisplayConfigBufferSizes(User32.QDC.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount).Failed)
        {
            return null;
        }

        var paths = new Gdi32.DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new Gdi32.DISPLAYCONFIG_MODE_INFO[modeCount];

        // 最后一个参数必须传 NULL：传 topology 指针会稳定得到 ERROR_INVALID_PARAMETER
        if (User32.QueryDisplayConfig(
                User32.QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero).Failed)
        {
            return null;
        }

        foreach (var path in paths)
        {
            if (!string.Equals(SourceDevice(path), device, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Read(path);
        }

        return null;
    }

    private static DisplayColor? Read(Gdi32.DISPLAYCONFIG_PATH_INFO path)
    {
        try
        {
            var advanced = User32.DisplayConfigGetDeviceInfo<Gdi32.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(
                path.targetInfo.adapterId, path.targetInfo.id);

            var white = User32.DisplayConfigGetDeviceInfo<Gdi32.DISPLAYCONFIG_SDR_WHITE_LEVEL>(
                path.targetInfo.adapterId, path.targetInfo.id);

            var enabled = IsSet(advanced.value, Gdi32.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_VALUE.advancedColorEnabled);
            return new DisplayColor(enabled, white.SDRWhiteLevel);
        }
        catch (Exception)
        {
            // 结构体大小或类型与系统不匹配时该调用会抛异常，视为读不到
            return null;
        }
    }

    private static string? SourceDevice(Gdi32.DISPLAYCONFIG_PATH_INFO path)
    {
        try
        {
            return User32.DisplayConfigGetDeviceInfo<Gdi32.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(
                path.sourceInfo.adapterId, path.sourceInfo.id).viewGdiDeviceName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? MonitorDevice(nint windowHandle)
    {
        var monitor = User32.MonitorFromWindow(new HWND(windowHandle), User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
        if (monitor.IsNull)
        {
            return null;
        }

        var info = new User32.MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFOEX>() };
        return User32.GetMonitorInfo(monitor, ref info) ? info.szDevice : null;
    }

    private static bool IsSet(
        Gdi32.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_VALUE value,
        Gdi32.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_VALUE mask) => ((uint)value & (uint)mask) != 0;
}
