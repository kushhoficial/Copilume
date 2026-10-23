using System;
using System.Runtime.InteropServices;

namespace Copilume;

internal static class Native
{
    internal const int ClipboardUpdate = 0x031D, HotKey = 0x0312;
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool CloseClipboard();
    [DllImport("user32.dll")] internal static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] internal static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Value; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    internal static bool ModifiersReleased()
    {
        foreach (int key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
            if ((GetAsyncKeyState(key) & 0x8000) != 0) return false;
        return true;
    }
    internal static bool Paste()
    {
        Input Key(ushort key, uint flags) => new() { Type = 1, Value = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = flags } } };
        Input[] events = { Key(0x11, 0), Key(0x56, 0), Key(0x56, 2), Key(0x11, 2) };
        if (SendInput(4, events, Marshal.SizeOf<Input>()) == 4) return true;
        // Do not leave a synthetic modifier down after a partial insertion.
        Input[] release = { Key(0x56, 2), Key(0x11, 2) };
        SendInput(2, release, Marshal.SizeOf<Input>());
        return false;
    }
    // Must be called only between OpenClipboard / CloseClipboard. Never resolve file paths or deserialize OLE objects.
    internal static byte[]? Read(uint format, int maximum)
    {
        if (!IsClipboardFormatAvailable(format)) return null;
        var handle = GetClipboardData(format);
        if (handle == IntPtr.Zero) return null;
        ulong size = GlobalSize(handle).ToUInt64();
        if (size == 0 || size > (ulong)maximum) return null;
        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero) return null;
        try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); return bytes; }
        finally { GlobalUnlock(handle); }
    }
}
