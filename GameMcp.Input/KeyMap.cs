using Vanara.PInvoke;

namespace GameMcp.Input;

/// <summary>
/// 键名与 <see cref="User32.VK"/> 的互转。大小写、空格、连字符、下划线均不敏感。
/// 键名既接受具名形式（<c>enter</c> / <c>pageup</c> / <c>numpad3</c>），也接受单个字符（<c>a</c> / <c>7</c> / <c>;</c>）。
/// 鼠标键的 VK 取值与键盘共用一张表（<c>VK_LBUTTON</c> 等）。
/// </summary>
public static class KeyMap
{
    /// <summary>规范化后的键名 → 虚拟键码。数值直接取自 Windows 虚拟键码表。</summary>
    private static readonly Dictionary<string, User32.VK> Named = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>单字符符号 → 虚拟键码。</summary>
    private static readonly Dictionary<char, User32.VK> Symbols = new()
    {
        [';'] = (User32.VK)0xBA, [':'] = (User32.VK)0xBA,
        ['='] = (User32.VK)0xBB, ['+'] = (User32.VK)0xBB,
        [','] = (User32.VK)0xBC, ['<'] = (User32.VK)0xBC,
        ['-'] = (User32.VK)0xBD, ['_'] = (User32.VK)0xBD,
        ['.'] = (User32.VK)0xBE, ['>'] = (User32.VK)0xBE,
        ['/'] = (User32.VK)0xBF, ['?'] = (User32.VK)0xBF,
        ['`'] = (User32.VK)0xC0, ['~'] = (User32.VK)0xC0,
        ['['] = (User32.VK)0xDB, ['{'] = (User32.VK)0xDB,
        ['\\'] = (User32.VK)0xDC, ['|'] = (User32.VK)0xDC,
        [']'] = (User32.VK)0xDD, ['}'] = (User32.VK)0xDD,
        ['\''] = (User32.VK)0xDE, ['"'] = (User32.VK)0xDE,
        ['*'] = (User32.VK)0x6A,
    };

    static KeyMap()
    {
        void Add(User32.VK vk, params string[] names)
        {
            foreach (var name in names)
            {
                Named[name] = vk;
            }
        }

        Add((User32.VK)0x01, "mouseleft", "lbutton", "mouse1");
        Add((User32.VK)0x02, "mouseright", "rbutton", "mouse2");
        Add((User32.VK)0x04, "mousemiddle", "mbutton", "mouse3");
        Add((User32.VK)0x05, "mousex1", "xbutton1", "mouse4");
        Add((User32.VK)0x06, "mousex2", "xbutton2", "mouse5");

        Add((User32.VK)0x08, "backspace", "back", "bs");
        Add((User32.VK)0x09, "tab");
        Add((User32.VK)0x0C, "clear");
        Add((User32.VK)0x0D, "enter", "return", "numpadenter");
        Add((User32.VK)0x10, "shift");
        Add((User32.VK)0x11, "ctrl", "control");
        Add((User32.VK)0x12, "alt", "menu");
        Add((User32.VK)0x13, "pause", "break");
        Add((User32.VK)0x14, "capslock", "caps");
        Add((User32.VK)0x1B, "escape", "esc");
        Add((User32.VK)0x20, "space", "spacebar");
        Add((User32.VK)0x21, "pageup", "pgup", "prior");
        Add((User32.VK)0x22, "pagedown", "pgdn", "next");
        Add((User32.VK)0x23, "end");
        Add((User32.VK)0x24, "home");
        Add((User32.VK)0x25, "left", "arrowleft");
        Add((User32.VK)0x26, "up", "arrowup");
        Add((User32.VK)0x27, "right", "arrowright");
        Add((User32.VK)0x28, "down", "arrowdown");
        Add((User32.VK)0x29, "select");
        Add((User32.VK)0x2A, "print");
        Add((User32.VK)0x2B, "execute");
        Add((User32.VK)0x2C, "printscreen", "prtsc", "snapshot");
        Add((User32.VK)0x2D, "insert", "ins");
        Add((User32.VK)0x2E, "delete", "del");
        Add((User32.VK)0x2F, "help");

        for (var i = 0; i <= 9; i++)
        {
            Add((User32.VK)(0x60 + i), $"numpad{i}");
        }

        Add((User32.VK)0x6B, "numpadadd", "add");
        Add((User32.VK)0x6C, "separator");
        Add((User32.VK)0x6D, "numpadsubtract", "numpadsub", "subtract");
        Add((User32.VK)0x6E, "numpaddecimal", "numpaddec", "decimal");
        Add((User32.VK)0x6F, "numpaddivide", "numpaddiv", "divide");

        for (var i = 1; i <= 24; i++)
        {
            Add((User32.VK)(0x70 + i - 1), $"f{i}");
        }

        Add((User32.VK)0x5B, "lwin", "win", "lmeta", "meta", "super", "cmd");
        Add((User32.VK)0x5C, "rwin", "rmeta");
        Add((User32.VK)0x5D, "apps", "contextmenu");
        Add((User32.VK)0x5F, "sleep");
        Add((User32.VK)0x90, "numlock");
        Add((User32.VK)0x91, "scrolllock");
        Add((User32.VK)0xA0, "lshift");
        Add((User32.VK)0xA1, "rshift");
        Add((User32.VK)0xA2, "lctrl", "lcontrol");
        Add((User32.VK)0xA3, "rctrl", "rcontrol");
        Add((User32.VK)0xA4, "lalt", "lmenu");
        Add((User32.VK)0xA5, "ralt", "rmenu");

        Add((User32.VK)0xA6, "browserback");
        Add((User32.VK)0xA7, "browserforward");
        Add((User32.VK)0xA8, "browserrefresh");
        Add((User32.VK)0xA9, "browserstop");
        Add((User32.VK)0xAA, "browsersearch");
        Add((User32.VK)0xAB, "browserfavorites");
        Add((User32.VK)0xAC, "browserhome");
        Add((User32.VK)0xAD, "volumemute");
        Add((User32.VK)0xAE, "volumedown");
        Add((User32.VK)0xAF, "volumeup");
        Add((User32.VK)0xB0, "medianexttrack");
        Add((User32.VK)0xB1, "mediaprevtrack");
        Add((User32.VK)0xB2, "mediastop");
        Add((User32.VK)0xB3, "mediaplaypause");
        Add((User32.VK)0xB4, "launchmail");
        Add((User32.VK)0xB5, "launchmedia");
        Add((User32.VK)0xB6, "launchapp1");
        Add((User32.VK)0xB7, "launchapp2");

        Add((User32.VK)0xBA, "oem1");
        Add((User32.VK)0xBB, "oemplus");
        Add((User32.VK)0xBC, "oemcomma");
        Add((User32.VK)0xBD, "oemminus");
        Add((User32.VK)0xBE, "oemperiod");
        Add((User32.VK)0xBF, "oem2");
        Add((User32.VK)0xC0, "oem3");
        Add((User32.VK)0xDB, "oem4");
        Add((User32.VK)0xDC, "oem5");
        Add((User32.VK)0xDD, "oem6");
        Add((User32.VK)0xDE, "oem7");
        Add((User32.VK)0xDF, "oem8");
        Add((User32.VK)0xE2, "oem102");
        Add((User32.VK)0xE5, "processkey");
        Add((User32.VK)0xF6, "attn");
        Add((User32.VK)0xF7, "crsel");
        Add((User32.VK)0xF8, "exsel");
        Add((User32.VK)0xF9, "ereof");
        Add((User32.VK)0xFA, "play");
        Add((User32.VK)0xFB, "zoom");
        Add((User32.VK)0xFD, "pa1");
        Add((User32.VK)0xFE, "oemclear");
    }

    /// <summary>键名参考文本，用于工具描述。</summary>
    public const string NameHint =
        "字母/数字用单字符（a、7），符号用单字符（; , . / - = [ ] \\ ` '），" +
        "其余用具名：enter、escape、tab、space、backspace、delete、insert、home、end、pageup、pagedown、" +
        "up、down、left、right、shift、ctrl、alt、win、capslock、f1~f24、numpad0~numpad9、" +
        "numpadadd、numpadsubtract、numpadmultiply、numpaddivide、numpaddecimal、" +
        "mouseleft、mouseright、mousemiddle、mousex1、mousex2、" +
        "修饰键可指定左右：lshift/rshift、lctrl/rctrl、lalt/ralt、lwin/rwin";

    public static bool TryParse(string? name, out User32.VK vk)
    {
        vk = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var key = Normalize(name);
        if (key.Length == 0)
        {
            return false;
        }

        if (key.Length == 1)
        {
            var c = key[0];
            if (c is >= 'a' and <= 'z')
            {
                vk = (User32.VK)(0x41 + (c - 'a'));
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                vk = (User32.VK)(0x30 + (c - '0'));
                return true;
            }

            if (Symbols.TryGetValue(c, out vk))
            {
                return true;
            }
        }

        return Named.TryGetValue(key, out vk);
    }

    /// <summary>判断该键是否属于鼠标键（<c>VK_LBUTTON</c> 一族的取值）。</summary>
    public static bool IsMouseKey(User32.VK vk) => (int)vk is >= 0x01 and <= 0x06;

    private static string Normalize(string name)
    {
        var trimmed = name.Trim().ToLowerInvariant();
        if (trimmed.Length == 1)
        {
            return trimmed;
        }

        var buffer = new System.Text.StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            buffer.Append(c);
        }

        var normalized = buffer.ToString();
        return normalized.Length > 0 ? normalized : trimmed;
    }
}
