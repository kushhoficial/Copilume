using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Copilume;

internal static class WindowMaterial
{
    internal static bool EffectsEnabled { get; private set; } = true;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
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
        Attribute(2, 1); // DWMWA_NCRENDERING_POLICY / DWMNCRP_DISABLED: no classic frame on focus loss.
        Attribute(20, dark ? 1 : 0); // DWMWA_USE_IMMERSIVE_DARK_MODE
        Attribute(33, 2); // DWMWCP_ROUND; unsupported attributes are harmless on Windows 10.
        Attribute(34, -2); // Hide the system's rectangular/small-radius border; WPF draws our rim.
        EffectsEnabled = TransparencyEnabled();
        bool acrylic = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && EffectsEnabled;
        if (acrylic)
        {
            int material = 3; // DWMSBT_TRANSIENTWINDOW: public Desktop Acrylic API.
            acrylic = DwmSetWindowAttribute(handle, 38, ref material, sizeof(int)) >= 0;
        }
        if (!acrylic) Attribute(38, 1); // DWMSBT_NONE
        var margins = acrylic ? new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 } : default;
        DwmExtendFrameIntoClientArea(handle, ref margins);
        // Never paint a rectangular fallback behind the rounded WPF surface.
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        WindowContour.Refresh(window);
        return acrylic;
    }
}
