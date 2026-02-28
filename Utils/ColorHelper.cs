using System.Runtime.InteropServices;

using Microsoft.Win32;

using Spectre.Console;

namespace RanaPdfTool.Utils;

public static class ColorHelper
{
    public static Color GetWindowsAccentColor(Color defaultColor)
    {
        // 1. 检测是否为 Windows 平台
        if (!OperatingSystem.IsWindows())
            return defaultColor;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int colorValue)
            {
                // 注册表中的格式是 0xffBBGGRR
                // 我们需要提取 R, G, B 分量
                byte r = (byte)((colorValue >> 0) & 0xFF);
                byte g = (byte)((colorValue >> 8) & 0xFF);
                byte b = (byte)((colorValue >> 16) & 0xFF);

                return new Color(r, g, b);
            }
        }
        catch
        { /* 忽略错误 */ }

        return defaultColor;
    }
}
