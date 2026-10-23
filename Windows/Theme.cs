using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Copilume;

internal static class Theme
{
    internal static bool IsDark { get; private set; }
    internal static bool Apply(string appearance)
    {
        bool dark = appearance == "Escuro";
        if (appearance == "Sistema")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        void Brush(string name, string light, string night) => Application.Current.Resources[name] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        Brush("Surface", "#E7EEF2", "#252F38");
        Brush("WindowTint", "#CCE7EEF2", "#CC252F38");
        Brush("Glass", "#BBD8EEF4", "#B3415968");
        Brush("Card", "#DBFFFFFF", "#B52C343C");
        Brush("Ink", "#123043", "#EFF7FC");
        Brush("Muted", "#526A7B", "#B2C2CE");
        Brush("Accent", "#147D9A", "#63CDF4");
        Brush("Hairline", "#45809EAD", "#477E9AAE");
        Brush("Hover", "#197B9AAE", "#248FB5C9");
        Brush("Selection", "#405EB5D2", "#7850778C");
        Brush("SegmentSelected", "#95FFFFFF", "#587CA4B8");
        Brush("Field", "#78FFFFFF", "#45425562");
        if (SystemParameters.HighContrast)
        {
            foreach (string name in new[] { "Surface", "WindowTint", "Glass", "Card", "Field" }) Application.Current.Resources[name] = SystemColors.WindowBrush;
            foreach (string name in new[] { "Ink", "Muted", "Accent", "Hairline" }) Application.Current.Resources[name] = SystemColors.WindowTextBrush;
            foreach (string name in new[] { "Selection", "SegmentSelected", "Hover" }) Application.Current.Resources[name] = SystemColors.ControlBrush;
        }
        IsDark = dark;
        return dark;
    }
}
