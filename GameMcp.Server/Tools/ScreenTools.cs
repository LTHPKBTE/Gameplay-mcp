using System.ComponentModel;
using System.Text.Json;
using GameMcp.Capture;
using GameMcp.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Server.Tools;

[McpServerToolType]
public sealed class ScreenTools(
    SessionService session,
    IOptions<CaptureOptions> options,
    ILogger<ScreenTools> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private CaptureOptions Options => options.Value;

    [McpServerTool(
        Name = "get_state",
        ReadOnly = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(StateResult))]
    [Description("截取绑定窗口并返回图像。所有坐标都以返回图的像素为准，每轮操作前先调用它。")]
    public async Task<CallToolResult> GetStateAsync(
        [Description("编码格式：png 或 webp，留空用配置")] string? format = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var imageFormat = ParseFormat(format);
            using var result = await session.CaptureAsync(cancellationToken).ConfigureAwait(false);

            var state = new StateResult
            {
                Width = result.Width,
                Height = result.Height,
                ScaleX = result.ScaleX,
                ScaleY = result.ScaleY,
                Format = imageFormat == CaptureImageFormat.WebP ? "webp" : "png",
                IsHdr = result.IsHdrActive,
            };

            return new CallToolResult
            {
                Content =
                [
                    ImageContentBlock.FromBytes(
                        ImageEncoder.Encode(result.Bgr, imageFormat, Options.WebPQuality),
                        ImageEncoder.MimeType(imageFormat)),
                    new TextContentBlock { Text = JsonSerializer.Serialize(state, Json) },
                ],
                StructuredContent = JsonSerializer.SerializeToElement(state, Json),
            };
        }
        catch (ToolException e)
        {
            return Failure(e.Code, e.Message, e.Hint);
        }
        catch (Exception e)
        {
            logger.LogError(e, "截图失败");
            return Failure("capture_failed", e.Message, null);
        }
    }

    private static CallToolResult Failure(string code, string message, string? hint)
    {
        var state = new StateResult { Error = code, Message = message, Hint = hint };

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(state, Json) }],
            StructuredContent = JsonSerializer.SerializeToElement(state, Json),
        };
    }

    private CaptureImageFormat ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return Options.Format;
        }

        return format.Trim().ToLowerInvariant() switch
        {
            "png" => CaptureImageFormat.Png,
            "webp" => CaptureImageFormat.WebP,
            _ => throw new ToolException("unknown_format", $"未知的编码格式：{format}", "可用 png / webp"),
        };
    }
}
