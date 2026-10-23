using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Copilume;

// Clip the HWND as well as WPF content. DWM's rounding preference is only a hint,
// and its small system radius does not match our 24-DIP visual border.
internal static class WindowContour
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    private static readonly ConditionalWeakTable<Window, Contour> contours = new();
    internal static void Attach(Window window, double radius) => contours.GetValue(window, w => new Contour(w, radius)).Update();
    internal static void Refresh(Window window) { if (contours.TryGetValue(window, out var contour)) contour.Update(); }
    private sealed class Contour
    {
        private readonly Window window;
        private readonly double radius;
        private HwndSource? source;
        private bool pending, closed;
        internal Contour(Window window, double radius)
        {
            this.window = window; this.radius = radius;
            window.SourceInitialized += (_, _) => Hook();
            window.SizeChanged += (_, _) => Queue();
            window.StateChanged += (_, _) => Queue();
            window.Loaded += (_, _) => { Hook(); Update(); };
            window.Closed += (_, _) => { closed = true; source?.RemoveHook(Message); source = null; };
            Hook();
        }
        private void Hook()
        {
            if (source != null) return;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            source = HwndSource.FromHwnd(handle);
            if (source == null) return;
            source.AddHook(Message);
            // Recalculate the client area after installing WM_NCCALCSIZE handling.
            // Otherwise Windows reserves an invisible resize frame behind the rounded content.
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x0037);
        }
        private IntPtr Message(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0083) { handled = true; return IntPtr.Zero; } // client fills the HWND
            if (message is 0x0085 or 0x0086) { handled = true; return new IntPtr(1); } // no legacy non-client paint
            if (message is 0x0005 or 0x02E0 or 0x007D) Queue(); // size, DPI, styles
            if (message == 0x0084 && window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip && window.WindowState == WindowState.Normal && GetWindowRect(hwnd, out var bounds))
            {
                long coordinates = lParam.ToInt64();
                int x = (short)(coordinates & 0xFFFF), y = (short)((coordinates >> 16) & 0xFFFF);
                int edge = Math.Max(4, (int)Math.Round(6 * GetDpiForWindow(hwnd) / 96.0));
                bool left = x < bounds.Left + edge, right = x >= bounds.Right - edge;
                bool top = y < bounds.Top + edge, bottom = y >= bounds.Bottom - edge;
                int corner = Math.Max(edge, (int)Math.Round(16 * GetDpiForWindow(hwnd) / 96.0));
                int hit = x < bounds.Left + corner && y < bounds.Top + corner ? 13 :
                    x >= bounds.Right - corner && y < bounds.Top + corner ? 14 :
                    x < bounds.Left + corner && y >= bounds.Bottom - corner ? 16 :
                    x >= bounds.Right - corner && y >= bounds.Bottom - corner ? 17 :
                    top ? 12 : bottom ? 15 : left ? 10 : right ? 11 : 0;
                if (hit != 0) { handled = true; return new IntPtr(hit); }
            }
            return IntPtr.Zero;
        }
        private void Queue()
        {
            if (pending || closed) return;
            pending = true;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => { pending = false; if (!closed) Update(); }));
        }
        internal void Update()
        {
            if (closed) return;
            Hook();
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
            int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return;
            double dpi = GetDpiForWindow(handle) / 96.0;
            int diameter = Math.Max(2, (int)Math.Round(radius * 2 * dpi));
            var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
            // On success Windows owns the HRGN; on failure it remains ours.
            if (region != IntPtr.Zero && SetWindowRgn(handle, region, true) == 0) DeleteObject(region);
        }
    }
}
