using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LazyRead;

public partial class CursorIndicator : Window
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _elapsed = new();
    private nint _handle;
    private double? _estimate;
    private DateTime? _hideAt;
    private (int X, int Y) _cursor, _pos;
    private (int Width, int Height)? _size;
    private bool _placed;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    public CursorIndicator()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            Native.SetWindowLongPtr(_handle, -20, Native.GetWindowLongPtr(_handle, -20) | 0x080000A0);
            HwndSource.FromHwnd(_handle)?.AddHook((nint hwnd, int msg, nint wparam, nint lparam, ref bool handled) =>
            {
                if (msg == 0x21) { handled = true; return 3; }
                if (msg == 0x84) { handled = true; return -1; }
                return 0;
            });
        };
        _tick.Tick += (_, _) => UpdateLabels();
    }
    public void SetStatus(string label, double? estimate = null, bool transient = false, bool restartTimer = true)
    {
        StatusLabel.Text = label; _estimate = estimate;
        if (restartTimer) _elapsed.Restart();
        _hideAt = transient ? DateTime.UtcNow.AddSeconds(2) : null;
        if (!IsVisible) { Show(); UpdateLayout(); }
        _placed = false; _size = null;
        _tick.Start(); UpdateLabels();
        if (!IsVisible) return;
        CompositionTarget.Rendering -= Follow; CompositionTarget.Rendering += Follow;
        Follow(this, EventArgs.Empty);
    }
    private void UpdateLabels()
    {
        if (_hideAt <= DateTime.UtcNow) { Clear(); return; }
        TimerLabel.Text = _estimate != null ? GenerationEstimate.Remaining(_estimate.Value, _elapsed.Elapsed.TotalSeconds)
            : _hideAt != null ? "" : _elapsed.Elapsed.ToString(@"mm\:ss");
    }
    private void Follow(object? sender, EventArgs e)
    {
        if (_handle == 0 || !Native.GetCursorPos(out var point)) return;
        if (_placed && _cursor == (point.X, point.Y)) return;
        _cursor = (point.X, point.Y);
        if (_size is null) { GetWindowRect(_handle, out var rect); _size = (rect.Right - rect.Left, rect.Bottom - rect.Top); }
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)).WorkingArea;
        var (x, y) = Place(point.X, point.Y, _size.Value.Width, _size.Value.Height, area);
        if (_placed && _pos == (x, y)) return;
        var first = !_placed; _placed = true; _pos = (x, y);
        SetWindowPos(_handle, -1, x, y, 0, 0, first ? 0x11u : 0x15u);
    }
    public static (int X, int Y) Place(int cursorX, int cursorY, int width, int height, System.Drawing.Rectangle area)
    {
        var x = cursorX - width - 8;
        if (x < area.Left) x = cursorX + 22;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        var y = cursorY + 22;
        if (y + height > area.Bottom) y = cursorY - height - 18;
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        return (x, y);
    }
    public bool Transient => IsVisible && _hideAt > DateTime.UtcNow;
    public void Clear() { _tick.Stop(); _elapsed.Stop(); CompositionTarget.Rendering -= Follow; Hide(); }
    public void Release() { _tick.Stop(); CompositionTarget.Rendering -= Follow; Close(); }
}
