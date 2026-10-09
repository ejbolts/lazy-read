using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LazyRead;

public readonly record struct HotkeySpec(uint Key, uint Modifiers)
{
    public static HotkeySpec Default => new(0x77, 3);
    public static bool Valid(uint key, uint modifiers) => (modifiers & ~7u) == 0 &&
        ((key >= 0x70 && key <= 0x87) || ((modifiers & 3) != 0 && ((key >= 0x41 && key <= 0x5A) || (key >= 0x30 && key <= 0x39))));
    public string Label => ((Modifiers & 2) != 0 ? "Ctrl+" : "") + ((Modifiers & 1) != 0 ? "Alt+" : "") +
        ((Modifiers & 4) != 0 ? "Shift+" : "") + (Key >= 0x70 && Key <= 0x87 ? "F" + (Key - 0x6F) : ((char)Key).ToString());
    public static bool TryCreate(System.Windows.Input.Key key, ModifierKeys modifiers, out HotkeySpec spec)
    {
        spec = new((uint)KeyInterop.VirtualKeyFromKey(key), (uint)modifiers);
        return Valid(spec.Key, spec.Modifiers);
    }
}

public sealed class GlobalHotkey : IDisposable
{
    private readonly HwndSource _source = new(new HwndSourceParameters("Lazy Read shortcut") { ParentWindow = -3, WindowStyle = 0 });
    private HotkeySpec? _registered;
    public event Action? Pressed;
    public GlobalHotkey() => _source.AddHook(Handle);
    private nint Handle(nint hwnd, int msg, nint wparam, nint lparam, ref bool handled)
    {
        if (msg == 0x312 && wparam == 1) { handled = true; Pressed?.Invoke(); }
        return 0;
    }
    public bool TryBind(HotkeySpec spec, out string error)
    {
        error = "";
        if (!HotkeySpec.Valid(spec.Key, spec.Modifiers)) { error = "Use Ctrl/Alt with a letter or number, or a function key."; return false; }
        if (_registered == spec) return true;
        var previous = _registered;
        Suspend();
        if (Native.RegisterHotKey(_source.Handle, 1, spec.Modifiers | 0x4000, spec.Key)) { _registered = spec; return true; }
        var code = Marshal.GetLastWin32Error();
        if (previous != null && Native.RegisterHotKey(_source.Handle, 1, previous.Value.Modifiers | 0x4000, previous.Value.Key)) _registered = previous;
        error = code == 1409 ? $"{spec.Label} is already used by another application. Choose another shortcut." : $"Could not register {spec.Label} (Windows error {code}).";
        return false;
    }
    public void Suspend() { if (_registered != null) Native.UnregisterHotKey(_source.Handle, 1); _registered = null; }
    public void Dispose() { Suspend(); _source.Dispose(); }
}
