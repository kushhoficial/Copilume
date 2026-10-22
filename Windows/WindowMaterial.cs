using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;

namespace Copilume;

internal static class WindowMaterial
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    internal static bool TransparencyEnabled()
    {
        if (SystemParameters.HighContrast) return false;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") is not int value || value != 0;
    }
    internal static bool Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;
        void Attribute(int attribute, int value) => DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        Attribute(20, dark ? 1 : 0); // DWMWA_USE_IMMERSIVE_DARK_MODE
        Attribute(33, 2); // DWMWCP_ROUND; unsupported attributes are harmless on Windows 10.
        bool acrylic = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && TransparencyEnabled();
        if (acrylic)
        {
            int material = 3; // DWMSBT_TRANSIENTWINDOW: public Desktop Acrylic API.
            acrylic = DwmSetWindowAttribute(handle, 38, ref material, sizeof(int)) >= 0;
        }
        if (!acrylic) Attribute(38, 1); // DWMSBT_NONE
        if (WindowChrome.GetWindowChrome(window) is { } chrome)
            chrome.GlassFrameThickness = acrylic ? new Thickness(-1) : new Thickness(0);
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target) target.BackgroundColor = acrylic ? Colors.Transparent : ((SolidColorBrush)Application.Current.Resources["Surface"]).Color;
        return acrylic;
    }
}
