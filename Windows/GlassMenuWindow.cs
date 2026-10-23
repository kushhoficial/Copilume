using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Copilume;

internal sealed record MenuAction(string Title, string Icon, Action Invoke, string Shortcut = "", bool Enabled = true, bool Destructive = false);

// Both tray and clipboard item menus use the same non-layered, natively clipped
// glass window. No WinForms popup or rectangular popup background underneath it.
internal sealed class GlassMenuWindow : Window
{
    private readonly List<Button> actions = new();
    private readonly GlassBorder surface;
    private bool closing;
    internal bool ActionInvoked { get; private set; }
    internal bool RestoreOwnerFocus { get; private set; }
    internal GlassMenuWindow(string title, string subtitle, IEnumerable<MenuAction?> entries)
    {
        Title = title;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Width = 310; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.Manual;
        WindowContour.Attach(this, 18);
        var panel = new StackPanel { Margin = new Thickness(7) };
        var header = new StackPanel { Margin = new Thickness(11, 10, 11, 12) };
        header.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold });
        var detail = new TextBlock { Text = subtitle, FontSize = 10, Margin = new Thickness(0, 4, 0, 0) };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); header.Children.Add(detail); panel.Children.Add(header);
        foreach (var entry in entries)
        {
            if (entry == null)
            {
                var line = new Border { Height = 1, Margin = new Thickness(11, 6, 11, 6) };
                line.SetResourceReference(Border.BackgroundProperty, "Hairline"); panel.Children.Add(line); continue;
            }
            var button = new Button { Style = (Style)FindResource("GlassMenuButton"), IsEnabled = entry.Enabled, Tag = entry };
            System.Windows.Automation.AutomationProperties.SetName(button, entry.Title);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, Data = (Geometry)FindResource(entry.Icon) };
            row.Children.Add(icon);
            var label = new TextBlock { Text = entry.Title, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 1); row.Children.Add(label);
            var hint = new TextBlock { Text = entry.Shortcut, FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Grid.SetColumn(hint, 2); row.Children.Add(hint);
            button.Content = row;
            if (entry.Destructive && !SystemParameters.HighContrast)
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.IsDark ? "#FF9DA6" : "#AD3547"));
            button.Click += (_, _) => Execute(entry);
            button.MouseEnter += (_, _) => { if (button.IsEnabled) button.Focus(); };
            actions.Add(button); panel.Children.Add(button);
        }
        surface = new GlassBorder { CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Child = panel };
        surface.SetResourceReference(Border.BackgroundProperty, "Surface"); surface.SetResourceReference(Border.BorderBrushProperty, "Hairline");
        Content = surface;
        SourceInitialized += (_, _) => RefreshMaterial();
        ContentRendered += (_, _) => actions.FirstOrDefault(x => x.IsEnabled)?.Focus();
        Closing += (_, _) => closing = true;
        Deactivated += (_, _) => { if (!closing) Close(); };
        PreviewKeyDown += MenuKey;
    }
    private void Execute(MenuAction entry)
    {
        if (!entry.Enabled || closing) return;
        ActionInvoked = true;
        Close();
        // Restore focus to the owner before a clipboard operation or modal window.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(entry.Invoke));
    }
    private void MenuKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { RestoreOwnerFocus = true; Close(); e.Handled = true; return; }
        var enabled = actions.Where(x => x.IsEnabled).ToArray();
        if (enabled.Length == 0) return;
        int selected = Array.FindIndex(enabled, x => x.IsKeyboardFocusWithin);
        if (e.Key is Key.Down or Key.Up or Key.Home or Key.End)
        {
            int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? enabled.Length - 1 :
                (selected + (e.Key == Key.Down ? 1 : -1) + enabled.Length) % enabled.Length;
            enabled[next].Focus(); e.Handled = true;
        }
        else if (e.Key == Key.Enter && selected >= 0)
        { Execute((MenuAction)enabled[selected].Tag); e.Handled = true; }
    }
    internal void RefreshMaterial() => surface.SetResourceReference(Border.BackgroundProperty, WindowMaterial.Apply(this, Theme.IsDark) ? "WindowTint" : "Surface");
    internal void SolidPreview() => surface.SetResourceReference(Border.BackgroundProperty, "Surface");
    internal void ShowAt(Point screenPixels, bool animate = true)
    {
        Show(); UpdateLayout();
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromPoint(new PixelPoint { X = (int)screenPixels.X, Y = (int)screenPixels.Y }, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
        {
            // Moving onto another monitor can change DPI and resize the popup.
            // Clamp again using the resulting physical size rather than the old monitor's DPI.
            for (int pass = 0; pass < 2; pass++)
            {
                GetWindowRect(hwnd, out var bounds);
                var position = Place(screenPixels, new Size(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top), new Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top));
                SetWindowPos(hwnd, new IntPtr(-1), (int)position.X, (int)position.Y, 0, 0, 0x0011);
                UpdateLayout();
            }
        }
        WindowContour.Refresh(this); Activate();
        if (animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            surface.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(130)));
    }
    internal static Point Place(Point anchor, Size size, Rect work)
    {
        double x = Math.Clamp(anchor.X, work.Left, Math.Max(work.Left, work.Right - size.Width));
        double y = anchor.Y + 6;
        if (y + size.Height > work.Bottom) y = anchor.Y - size.Height - 6;
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - size.Height));
        return new Point(x, y);
    }
    [StructLayout(LayoutKind.Sequential)] private struct PixelPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct PixelRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public PixelRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out PixelRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PixelPoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
