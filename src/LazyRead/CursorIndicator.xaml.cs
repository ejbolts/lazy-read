using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LazyRead;

public partial class CursorIndicator : Window
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch _elapsed = new();
    private nint _handle;
    private double? _estimate;
    private DateTime? _hideAt;
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
        _tick.Tick += (_, _) => UpdatePosition();
    }
    public void SetStatus(string label, double? estimate = null, bool transient = false, bool restartTimer = true)
    {
        StatusLabel.Text = label; _estimate = estimate;
        if (restartTimer) _elapsed.Restart();
        _hideAt = transient ? DateTime.UtcNow.AddSeconds(2) : null;
        if (!IsVisible) { Show(); UpdateLayout(); }
        _tick.Start(); UpdatePosition();
    }
    private void UpdatePosition()
    {
        if (_hideAt <= DateTime.UtcNow) { Clear(); return; }
        TimerLabel.Text = _estimate != null ? GenerationEstimate.Remaining(_estimate.Value, _elapsed.Elapsed.TotalSeconds)
            : _hideAt != null ? "" : _elapsed.Elapsed.ToString(@"mm\:ss");
        if (_handle == 0 || !Native.GetCursorPos(out var point)) return;
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)).WorkingArea;
        GetWindowRect(_handle, out var rect);
        var width = rect.Right - rect.Left; var height = rect.Bottom - rect.Top;
        var x = Math.Clamp(point.X + 22, area.Left, Math.Max(area.Left, area.Right - width));
        var desiredY = point.Y + 26 + height > area.Bottom ? point.Y - height - 18 : point.Y + 26;
        var y = Math.Clamp(desiredY, area.Top, Math.Max(area.Top, area.Bottom - height));
        SetWindowPos(_handle, -1, x, y, 0, 0, 0x11);
    }
    public void Clear() { _tick.Stop(); _elapsed.Stop(); Hide(); }
    public void Release() { _tick.Stop(); Close(); }
}
