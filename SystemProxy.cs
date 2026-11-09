using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MtdVpn;

/// Включает/выключает системный прокси Windows (для режима без TUN)
public static class SystemProxy
{
    public const string Address = "127.0.0.1:10808";
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    [DllImport("wininet.dll", SetLastError = true)]
    static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int length);

    public static void Set()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (k == null) return;
            k.SetValue("ProxyServer", Address);
            k.SetValue("ProxyOverride",
                "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;" +
                "172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*;<local>");
            k.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            Notify();
        }
        catch { }
    }

    /// onlyIfOurs: снимать прокси, только если он выставлен нашей программой
    public static void Clear(bool onlyIfOurs)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (k == null) return;
            if (onlyIfOurs && (k.GetValue("ProxyServer") as string) != Address) return;
            k.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            Notify();
        }
        catch { }
    }

    static void Notify()
    {
        InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);   // SETTINGS_CHANGED
        InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);   // REFRESH
    }
}
