using System.ComponentModel;
using GameMcp.Input;
using GameMcp.Server.Services;
using GameMcp.Windowing;
using ModelContextProtocol.Server;

namespace GameMcp.Server.Tools;

[McpServerToolType]
public sealed class WindowTools(IWindowService windows, SessionService session, IInputService input)
{
    public sealed record WindowSummary(
        string Handle,
        string Title,
        string ClassName,
        PixelRect Bounds,
        PixelRect ClientBounds,
        bool IsMinimized,
        bool IsForeground);

    [McpServerTool(Name = "list_windows", ReadOnly = true, UseStructuredContent = true)]
    [Description("列出当前可见的顶层窗口。句柄为十六进制字符串，可直接传给 attach_window。")]
    public IReadOnlyList<WindowSummary> ListWindows(
        [Description("标题或类名的过滤子串，留空返回全部")] string? filter = null)
    {
        var result = new List<WindowSummary>();
        foreach (var window in windows.Enumerate())
        {
            if (!string.IsNullOrEmpty(filter)
                && !window.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !window.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new WindowSummary(
                HandleText.Format(window.Handle),
                window.Title,
                window.ClassName,
                window.WindowRect,
                windows.GetClientRectOnScreen(window.Handle),
                window.IsMinimized,
                window.IsForeground));
        }

        return result;
    }

    [McpServerTool(Name = "attach_window", UseStructuredContent = true)]
    [Description("绑定目标窗口，之后所有截图与输入都作用于它。最小化的窗口也能绑定，但无法截图。")]
    public AttachResult AttachWindow(
        [Description("list_windows 返回的句柄，形如 0x40D54")] string handle)
    {
        try
        {
            if (!HandleText.TryParse(handle, out var hwnd))
            {
                throw new ToolException("invalid_handle", $"无法解析句柄：{handle}", "句柄形如 0x40D54，或直接用十进制数字");
            }

            var info = session.Attach(hwnd);
            var client = windows.GetClientRectOnScreen(info.Handle);

            return new AttachResult
            {
                Ok = true,
                Handle = HandleText.Format(info.Handle),
                Title = info.Title,
                ClientWidth = client.Width,
                ClientHeight = client.Height,
                Dpi = windows.GetDpi(info.Handle),
                IsMinimized = info.IsMinimized,
            };
        }
        catch (ToolException e)
        {
            return new AttachResult { Ok = false, Error = e.Code, Message = e.Message, Hint = e.Hint };
        }
    }

    [McpServerTool(Name = "detach_window", UseStructuredContent = true)]
    [Description("解除窗口绑定，并释放已按下的键。")]
    public AttachResult DetachWindow()
    {
        session.Detach();
        return new AttachResult { Ok = true, Message = "已解除绑定" };
    }

    [McpServerTool(Name = "focus_window", Destructive = true, UseStructuredContent = true)]
    [Description("把已绑定窗口唤到前台；最小化时会先还原。前台输入模式需要目标在前台。")]
    public AttachResult FocusWindow()
    {
        try
        {
            input.EnsureInputAllowed();
            var info = session.RequireTarget();
            var ok = windows.Focus(info.Handle);

            return new AttachResult
            {
                Ok = ok,
                Error = ok ? null : "focus_failed",
                Message = ok ? "已置于前台" : "窗口未获得前台焦点",
                Hint = ok ? null : "Windows 限制非前台进程直接夺取焦点；可先点击任务栏上的目标窗口再重试",
                Handle = HandleText.Format(info.Handle),
                Title = info.Title,
            };
        }
        catch (InputException e)
        {
            return new AttachResult { Ok = false, Error = e.Code, Message = e.Message, Hint = e.Hint };
        }
        catch (ToolException e)
        {
            return new AttachResult { Ok = false, Error = e.Code, Message = e.Message, Hint = e.Hint };
        }
    }
}