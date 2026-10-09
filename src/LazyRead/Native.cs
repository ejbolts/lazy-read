using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LazyRead;

public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern nint GetAncestor(nint window, uint flag);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern nint GetClipboardOwner();
    [DllImport("user32.dll")] public static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    public static string ProcessName(nint window)
    {
        GetWindowThreadProcessId(window, out var pid);
        try { return Process.GetProcessById((int)pid).ProcessName; } catch { return "Application"; }
    }
    public static bool OwnWindow(nint window)
    {
        GetWindowThreadProcessId(window, out var pid);
        return pid == Environment.ProcessId;
    }
    public static bool PressCopy()
    {
        var input = new[] { Key(0x11, false), Key(0x43, false), Key(0x43, true), Key(0x11, true) };
        return SendInput(4, input, Marshal.SizeOf<Input>()) == 4;
    }
    private static Input Key(ushort key, bool up) => new() { Type = 1, Union = new InputUnion { Keyboard = new KeyboardInput { Vk = key, Flags = up ? 2u : 0u } } };
}
