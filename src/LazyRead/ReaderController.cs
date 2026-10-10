using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LazyRead;

public sealed class ReaderController : IDisposable
{
    public ReaderSettings Settings { get; } = ReaderSettings.Load();
    public bool Exiting { get; private set; }
    public string? ShortcutProblem { get; private set; }
    private readonly GlobalHotkey _shortcut = new();
    private readonly SelectionReader _selection = new();
    private readonly CursorIndicator _indicator = new();
    private readonly GenerationEstimate _estimate = new();
    private SpeechEngine? _engine;
    private readonly MediaPlayer _audio = new();
    private readonly Queue<(string Path, double Seconds)> _pending = new();
    private readonly DispatcherTimer _bufferTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int[] _plan = Array.Empty<int>();
    private int _nextPassage;
    private readonly Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private MainWindow? _main;
    private string? _request, _playing;
    private bool _done, _paused, _capturing, _ready, _generating, _unloading;
    private int _generatingCharacters;
    private readonly Stopwatch _generationClock = new();
    private double _generationPrediction;
    private string _state = "Ready to read", _device, _source = "", _detail = "Highlight text and press Ctrl+Alt+F8.";
    public ReaderController()
    {
        _device = DeviceLabel("unloaded");
        _shortcut.Pressed += () => ReadSelection();
        _bufferTimer.Tick += (_, _) => TryStart();
        _audio.MediaEnded += (_, _) => { RemoveClip(); PlayNext(); UpdateIndicator(); };
        _audio.MediaFailed += async (_, args) => await UnloadVoice(false, "Playback failed", args.ErrorException.Message);
        _audio.MediaOpened += (_, _) => { if (_playing == null || _request == null) return; _audio.SpeedRatio = Settings.Speed; if (!_paused) _audio.Play(); SetState(_paused ? "Paused" : "Reading aloud", _detail); UpdateIndicator(); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Lazy Read", null, (_, _) => OpenMain());
        menu.Items.Add("Pause / resume", null, (_, _) => TogglePause());
        menu.Items.Add("Stop reading", null, (_, _) => Stop());
        menu.Items.Add("Enable / disable reading shortcut", null, (_, _) => SetEnabled(!Settings.Enabled));
        menu.Items.Add("Quit", null, (_, _) => Quit());
        using (var icon = typeof(ReaderController).Assembly.GetManifestResourceStream("LazyRead.ico")!) _trayIcon = new System.Drawing.Icon(icon, Forms.SystemInformation.SmallIconSize);
        _tray = new Forms.NotifyIcon { Icon = _trayIcon, Text = "Lazy Read · local voice", Visible = true, ContextMenuStrip = menu };
        _tray.DoubleClick += (_, _) => OpenMain();
        if (Settings.Enabled && !_shortcut.TryBind(Settings.Shortcut, out var error))
        { Settings.Enabled = false; ShortcutProblem = error; SetState("Choose another shortcut", error); }
    }
    public void OpenMain()
    {
        _main ??= new MainWindow(this);
        // Show and Activate leave a minimized window minimized, so a taskbar click must restore it.
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Show(); _main.Activate(); Refresh();
    }
    public void SetEnabled(bool enabled)
    {
        if (enabled && !_shortcut.TryBind(Settings.Shortcut, out var error))
        { enabled = false; ShortcutProblem = error; SetState("Shortcut unavailable", error); }
        else if (enabled) ShortcutProblem = null;
        if (!enabled) _shortcut.Suspend();
        Settings.Enabled = enabled; Settings.Save();
        if (_main != null) _main.EnabledBox.IsChecked = enabled;
    }
    public void BeginShortcutCapture() => _shortcut.Suspend();
    public bool SetShortcut(HotkeySpec spec, out string error)
    {
        error = "";
        if (!HotkeySpec.Valid(spec.Key, spec.Modifiers)) { error = "Use Ctrl/Alt with a letter or number, or a function key."; return false; }
        if (Settings.Enabled && !_shortcut.TryBind(spec, out error)) { _shortcut.TryBind(Settings.Shortcut, out _); return false; }
        ShortcutProblem = null; Settings.ShortcutKey = spec.Key; Settings.ShortcutModifiers = spec.Modifiers; Settings.Save();
        if (_request == null && _ready && _state == "Ready to read") SetState("Ready to read", "Highlight text and press " + spec.Label + "."); else Refresh();
        return true;
    }
    public void CancelShortcutCapture() { if (Settings.Enabled) _shortcut.TryBind(Settings.Shortcut, out _); }
    public void OpenSample()
    {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "LazyRead.exe")) { UseShellExecute = false, CreateNoWindow = true };
        if (Environment.ProcessPath?.EndsWith("LazyRead.exe", StringComparison.OrdinalIgnoreCase) == true) info.FileName = Environment.ProcessPath;
        info.ArgumentList.Add("--sample"); Process.Start(info);
    }
    private void StartEngine()
    {
        StopPlayback(); _ready = false; _engine?.Dispose();
        var engine = new SpeechEngine(); _engine = engine;
        engine.Message += message => Application.Current.Dispatcher.BeginInvoke(() => { if (ReferenceEquals(engine, _engine)) HandleMessage(message); });
        SetState($"Loading {VoiceChoice.Engine}…", "The first launch downloads the model. Later launches use the local cache.");
        try { engine.Start(); } catch (Exception e) { SetState("Engine failed to start", e.Message); }
    }
    private async void ReadSelection()
    {
        if (_unloading) return;
        if (_request != null) { await UnloadVoice(true); return; }
        if (_capturing) return;
        _capturing = true;
        var source = Native.GetForegroundWindow(); Native.GetCursorPos(out var point);
        try
        {
            var released = false;
            for (var i = 0; i < 60; i++)
            {
                if (!KeyHeld((int)Settings.Shortcut.Key) && !KeyHeld(0x11) && !KeyHeld(0x12) && !KeyHeld(0x10)) { released = true; break; }
                await Task.Delay(20);
            }
            if (!released || source != Native.GetForegroundWindow()) return;
            var capture = await _selection.Capture(source, point);
            if (capture == null)
            { SetState("No text selected", "Highlight a passage, then press " + Settings.Shortcut.Label + "."); if (_request == null) _indicator.SetStatus("No text selected", transient: true); return; }
            Speak(capture.Text, capture.Source);
        }
        catch (Exception e) { SetState("Selection could not be read", e.Message); if (_request == null) _indicator.SetStatus("Selection unavailable", transient: true); }
        finally { _capturing = false; }
    }
    private static bool KeyHeld(int key) => (Native.GetAsyncKeyState(key) & 0x8000) != 0;
    public async void Speak(string text, string source)
    {
        if (_unloading) return;
        try { text = ReadingRules.Validate(text); } catch (Exception e) { SetState("Choose some text", e.Message); return; }
        StopPlayback();
        if (_engine == null) StartEngine();
        _request = Guid.NewGuid().ToString("N"); _source = source;
        SetState(_ready ? "Generating voice…" : "Waiting for the local voice…", $"{source} · {Settings.Voice} · {text.Length:N0} characters");
        _indicator.SetStatus(_ready ? "Preparing speech" : "Loading voice");
        try
        {
            if (_engine == null) throw new InvalidOperationException("Open settings and start the local engine first.");
            await _engine.Send(new { action = "speak", id = _request, text, voice = Settings.Voice });
        }
        catch (Exception e) { await UnloadVoice(false, "Reading failed", e.Message); }
    }
    private void HandleMessage(JsonElement message)
    {
        var type = message.GetProperty("type").GetString();
        var id = message.TryGetProperty("id", out var value) ? value.GetString() : null;
        if (!string.IsNullOrEmpty(id) && id != _request)
        { if (type == "audio" && message.TryGetProperty("path", out var stale)) DeleteClip(stale.GetString()); return; }
        switch (type)
        {
            case "loading": SetState($"Loading {VoiceChoice.Engine}…", message.GetProperty("message").GetString() ?? ""); break;
            case "ready":
                _ready = true; _device = message.GetProperty("device").GetString() + " · " + VoiceChoice.Engine;
                if (_request == null) SetState("Ready to read", "Highlight text and press " + Settings.Shortcut.Label + "."); else Refresh();
                break;
            case "plan": _plan = message.GetProperty("characters").EnumerateArray().Select(part => part.GetInt32()).ToArray(); _nextPassage = 0; break;
            case "generating":
                _generating = true; _nextPassage = message.GetProperty("index").GetInt32(); _generatingCharacters = message.GetProperty("characters").GetInt32();
                _detail = $"{_source} · {Settings.Voice} · passage {message.GetProperty("index").GetInt32() + 1} of {message.GetProperty("total").GetInt32()}";
                if (_playing == null && _pending.Count == 0) SetState("Generating voice…", _detail); else Refresh();
                _generationPrediction = _estimate.Predict(_generatingCharacters); _generationClock.Restart(); UpdateIndicator();
                break;
            case "audio":
                _generating = false; _nextPassage = message.GetProperty("index").GetInt32() + 1;
                var seconds = message.GetProperty("seconds").GetDouble();
                _estimate.Observe(_generatingCharacters, message.GetProperty("generation_seconds").GetDouble()); _estimate.ObserveAudio(_generatingCharacters, seconds);
                var path = message.GetProperty("path").GetString()!;
                if (!ValidClip(path)) { FailReading("The engine returned an invalid audio file."); break; }
                _pending.Enqueue((path, seconds)); if (_playing == null) TryStart(); UpdateIndicator(); break;
            case "done": _generating = false; _done = true; if (_playing == null && _pending.Count == 0) Complete(); else if (_playing == null) TryStart(); else UpdateIndicator(); break;
            case "error": FailReading(message.GetProperty("message").GetString() ?? "Unknown engine error."); break;
        }
    }
    private bool ValidClip(string path) => _engine != null && Path.GetFullPath(path).StartsWith(Path.GetFullPath(_engine.AudioDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Path.GetExtension(path) == ".wav" && File.Exists(path);
    private void PlayNext()
    {
        if (_pending.TryDequeue(out var clip)) { _bufferTimer.Stop(); _playing = clip.Path; _audio.Open(new Uri(clip.Path)); }
        else if (_done) Complete();
        else { SetState("Buffering for smooth playback…", _detail); _bufferTimer.Start(); }
    }
    // Playback starts, or resumes after running dry, only once the rest of the reading is predicted to keep up.
    private void TryStart()
    {
        if (_playing != null || _request == null) { _bufferTimer.Stop(); return; }
        if (_pending.Count == 0) { if (_done) Complete(); return; }
        if (_done || BufferDelay() <= 0) { PlayNext(); return; }
        _bufferTimer.Start(); SetState("Buffering for smooth playback…", _detail); UpdateIndicator();
    }
    private double BufferDelay()
    {
        var remaining = new List<(double Generate, double Audio)>();
        for (var i = _nextPassage; i < _plan.Length && !_done; i++)
        {
            var generate = _generating && i == _nextPassage ? Math.Max(0.5, _generationPrediction - _generationClock.Elapsed.TotalSeconds) : _estimate.Predict(_plan[i]);
            remaining.Add((generate, _estimate.PredictAudio(_plan[i])));
        }
        return PlaybackBuffer.RequiredDelay(_pending.Sum(clip => clip.Seconds), remaining, Settings.Speed);
    }
    private void UpdateIndicator()
    {
        if (_playing != null || _request == null) _indicator.Clear();
        else if (_plan.Length > 0 && (_generating || _pending.Count > 0)) _indicator.SetStatus(_pending.Count > 0 ? "Buffering voice" : "Generating voice", BufferDelay());
        else if (_generating) _indicator.SetStatus("Generating voice", Math.Max(0, _generationPrediction - _generationClock.Elapsed.TotalSeconds));
        else _indicator.SetStatus("Preparing speech", restartTimer: false);
    }
    public void TogglePause()
    {
        if (_playing == null) return;
        _paused = !_paused; if (_paused) _audio.Pause(); else _audio.Play();
        SetState(_paused ? "Paused" : "Reading aloud", _detail); UpdateIndicator();
    }
    public void UpdateSpeed() => _audio.SpeedRatio = Settings.Speed;
    private static string DeviceLabel(string state) => VoiceChoice.Engine + " · model " + state;
    public async void Stop() => await UnloadVoice(true);
    private async void FailReading(string message) => await UnloadVoice(false, "Reading failed", message);
    private async Task UnloadVoice(bool cancelled, string? finalStatus = null, string? finalDetail = null)
    {
        if (_unloading) return;
        _unloading = true;
        var engine = _engine; _engine = null; _ready = false;
        StopPlayback();
        _device = DeviceLabel("unloading");
        SetState(cancelled ? "Cancelling…" : "Unloading voice…", "Releasing the local model from memory.");
        if (cancelled) _indicator.SetStatus("Cancelling…"); else _indicator.Clear();
        try
        {
            if (engine != null) await engine.Unload();
            _device = DeviceLabel("unloaded");
            SetState(finalStatus ?? (cancelled ? "Cancelled · model unloaded" : "Finished reading · model unloaded"),
                finalDetail ?? (cancelled ? "Highlight text and press " + Settings.Shortcut.Label + " to read again." : $"{_source} · {Settings.Voice}"));
            if (cancelled) _indicator.SetStatus("Cancelled · model unloaded", transient: true);
        }
        catch (Exception e)
        {
            _engine = engine;
            SetState("Model could not be unloaded", e.Message);
            _indicator.SetStatus("Unload failed", transient: true);
        }
        finally { _unloading = false; Refresh(); }
    }
    private async void StopPlayback()
    {
        var old = _request; var engine = _engine;
        _request = null; _done = false; _paused = false; _generating = false; _plan = Array.Empty<int>(); _nextPassage = 0; _bufferTimer.Stop();
        _indicator.Clear(); _audio.Stop(); _audio.Close(); RemoveClip();
        while (_pending.TryDequeue(out var clip)) DeleteClip(clip.Path);
        if (old != null) SetState("Stopped", "Ready for your next selection.");
        if (old != null && engine != null) try { await engine.Send(new { action = "cancel", id = old }); } catch { }
    }
    private async void Complete() => await UnloadVoice(false);
    private void RemoveClip() { var path = _playing; _playing = null; DeleteClip(path); }
    private void DeleteClip(string? path) { if (path != null && ValidClip(path)) try { File.Delete(path); } catch { } }
    private void SetState(string state, string detail) { _state = state; _detail = detail; Refresh(); }
    private void Refresh()
    {
        if (_main == null) return;
        _main.StatusText.Text = _state; _main.DeviceText.Text = _device;
        _main.ReadingText.Text = _detail; _main.ReadingText.ToolTip = _detail;
        _main.PauseButton.IsEnabled = _playing != null; _main.PauseButton.Content = _paused ? "Resume" : "Pause";
        _main.RetryButton.IsEnabled = !_unloading;
        _main.ShortcutLabel.Text = Settings.Shortcut.Label;
        if (ShortcutProblem != null) _main.ShortcutHint.Text = ShortcutProblem;
    }
    public void Quit() { Exiting = true; Application.Current.Shutdown(); }
    public void Dispose() { _shortcut.Dispose(); _indicator.Release(); _audio.Close(); _engine?.Dispose(); _tray.Dispose(); _trayIcon.Dispose(); }
}
