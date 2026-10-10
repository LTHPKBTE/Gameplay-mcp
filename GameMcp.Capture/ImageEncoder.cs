using OpenCvSharp;

namespace GameMcp.Capture;

/// <summary>把画布编码为字节流。</summary>
public static class ImageEncoder
{
    /// <summary>
    /// OpenCV 的 WebP 编码只认 <c>IMWRITE_WEBP_QUALITY</c> 一个参数：1..100 走有损，
    /// 大于 100 走 libwebp 的无损（<c>WebPEncodeLosslessBGR</c>）。
    /// </summary>
    public const int WebPLosslessQuality = 101;

    public static byte[] Encode(Mat bgr, CaptureImageFormat format, int webpQuality = 90)
    {
        if (format != CaptureImageFormat.WebP)
        {
            Cv2.ImEncode(".png", bgr, out var png);
            return png;
        }

        var value = webpQuality > 100
            ? WebPLosslessQuality
            : Math.Clamp(webpQuality, 1, 100);

        Cv2.ImEncode(
            ".webp",
            bgr,
            out var webp,
            new ImageEncodingParam(ImwriteFlags.WebPQuality, value));
        return webp;
    }

    public static string MimeType(CaptureImageFormat format)
        => format == CaptureImageFormat.WebP ? "image/webp" : "image/png";
}
