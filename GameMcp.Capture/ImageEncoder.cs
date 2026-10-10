using OpenCvSharp;

namespace GameMcp.Capture;

/// <summary>把画布编码为字节流。</summary>
public static class ImageEncoder
{
    public static byte[] Encode(Mat bgr, CaptureImageFormat format, int webpQuality = 90)
    {
        var extension = format == CaptureImageFormat.WebP ? ".webp" : ".png";

        if (format != CaptureImageFormat.WebP)
        {
            Cv2.ImEncode(extension, bgr, out var png);
            return png;
        }

        Cv2.ImEncode(
            extension,
            bgr,
            out var webp,
            new ImageEncodingParam(ImwriteFlags.WebPQuality, Math.Clamp(webpQuality, 1, 100)));
        return webp;
    }

    public static string MimeType(CaptureImageFormat format)
        => format == CaptureImageFormat.WebP ? "image/webp" : "image/png";
}
