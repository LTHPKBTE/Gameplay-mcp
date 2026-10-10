using System.Runtime.InteropServices;

namespace GameMcp.Elevation;

/// <summary>当前进程的令牌状态。</summary>
public static class ElevationState
{
    /// <summary>当前进程是否以管理员（提升后的令牌）运行。</summary>
    public static bool IsElevated
    {
        get
        {
            if (!NativeMethods.OpenProcessToken(
                    NativeMethods.GetCurrentProcess(),
                    NativeMethods.TokenQuery,
                    out var token))
            {
                return false;
            }

            try
            {
                var buffer = Marshal.AllocHGlobal(sizeof(uint));
                try
                {
                    return NativeMethods.GetTokenInformation(
                               token,
                               NativeMethods.TokenElevationInfo,
                               buffer,
                               sizeof(uint),
                               out _)
                           && Marshal.ReadInt32(buffer) != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
    }

    /// <summary>当前用户 SID 字符串；取不到时返回 <c>null</c>。</summary>
    public static string? CurrentUserSid
    {
        get
        {
            if (!NativeMethods.OpenProcessToken(
                    NativeMethods.GetCurrentProcess(),
                    NativeMethods.TokenQuery,
                    out var token))
            {
                return null;
            }

            try
            {
                var buffer = Marshal.AllocHGlobal(256);
                try
                {
                    if (!NativeMethods.GetTokenInformation(
                            token,
                            NativeMethods.TokenUserInfo,
                            buffer,
                            256,
                            out var returned)
                        || returned < nint.Size)
                    {
                        return null;
                    }

                    // TOKEN_USER 的首字段就是 SID 指针
                    var sid = Marshal.ReadIntPtr(buffer);
                    if (!NativeMethods.ConvertSidToStringSidW(sid, out var text))
                    {
                        return null;
                    }

                    try
                    {
                        return Marshal.PtrToStringUni(text);
                    }
                    finally
                    {
                        NativeMethods.LocalFree(text);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
    }
}
