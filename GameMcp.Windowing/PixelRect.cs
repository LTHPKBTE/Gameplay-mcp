using System.Text.Json.Serialization;

namespace GameMcp.Windowing;

/// <summary>物理像素矩形。</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    [JsonIgnore]
    public int Right => X + Width;

    [JsonIgnore]
    public int Bottom => Y + Height;

    [JsonIgnore]
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static readonly PixelRect Empty = new(0, 0, 0, 0);
}
