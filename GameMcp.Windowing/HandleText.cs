using System.Globalization;

namespace GameMcp.Windowing;

/// <summary>窗口句柄在协议里用十六进制字符串表示，这里做双向转换。</summary>
public static class HandleText
{
    public static string Format(nint handle) => $"0x{handle:X}";

    public static bool TryParse(string? text, out nint handle)
    {
        handle = nint.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var isHex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (isHex)
        {
            trimmed = trimmed[2..];
        }

        var style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;
        if (!long.TryParse(trimmed, style, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        handle = (nint)value;
        return true;
    }
}
