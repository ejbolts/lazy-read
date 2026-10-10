using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LazyRead;

public partial class MainWindow : Window
{
    private readonly ReaderController _reader;
    private bool _initialized;
    private bool _recordingShortcut;
    public MainWindow(ReaderController reader)
    {
        InitializeComponent(); _reader = reader;
        SpeedBox.ItemsSource = new[] { "0.8×", "1.0×", "1.25×", "1.5×" };
        VoiceBox.ItemsSource = VoiceChoice.Options;
        VoiceBox.SelectedItem = reader.Settings.Voice;
        SpeedBox.SelectedIndex = Array.IndexOf(new[] { 0.8, 1.0, 1.25, 1.5 }, reader.Settings.Speed);
        if (SpeedBox.SelectedIndex < 0) SpeedBox.SelectedIndex = 1;
        EnabledBox.IsChecked = reader.Settings.Enabled;
        _initialized = true;
        Closing += OnClosing;
    }
    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (_recordingShortcut) EndShortcutCapture();
        if (!_reader.Exiting) { args.Cancel = true; Hide(); }
    }
    private void ChangeShortcutClick(object sender, RoutedEventArgs args)
    {
        if (_recordingShortcut) { EndShortcutCapture(); return; }
        _reader.BeginShortcutCapture(); _recordingShortcut = true;
        ShortcutButton.Content = "Cancel"; ShortcutButton.Focus();
        ShortcutHint.Text = "Press a shortcut, for example Ctrl+Alt+F8 or F8. Escape cancels.";
    }
    private void EndShortcutCapture()
    {
        _recordingShortcut = false; _reader.CancelShortcutCapture();
        ShortcutButton.Content = "Change shortcut";
        ShortcutHint.Text = "Assign this shortcut to your mouse macro. Right-click keeps its usual menu.";
    }
    private void ShortcutKeyDown(object sender, KeyEventArgs args)
    {
        if (!_recordingShortcut) return;
        args.Handled = true;
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (key == Key.Escape) { EndShortcutCapture(); return; }
        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift) return;
        if (!HotkeySpec.TryCreate(key, Keyboard.Modifiers, out var spec))
        { ShortcutHint.Text = "Use Ctrl/Alt with a letter or number, or a function key."; return; }
        var success = _reader.SetShortcut(spec, out var error);
        EndShortcutCapture();
        if (!success) ShortcutHint.Text = error;
    }
    private void EnableChanged(object sender, RoutedEventArgs args) { if (_initialized) _reader.SetEnabled(EnabledBox.IsChecked == true); }
    private void OptionsChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_initialized) return;
        _reader.Settings.Voice = (string)VoiceBox.SelectedItem;
        _reader.Settings.Speed = new[] { 0.8, 1.0, 1.25, 1.5 }[SpeedBox.SelectedIndex];
        _reader.Settings.Save(); _reader.UpdateSpeed();
    }
    private void SpeakClick(object sender, RoutedEventArgs args) => _reader.Speak(SampleText.SelectionLength > 0 ? SampleText.SelectedText : SampleText.Text, "Test reading");
    private void StopClick(object sender, RoutedEventArgs args) => _reader.Stop();
    private void PauseClick(object sender, RoutedEventArgs args) => _reader.TogglePause();
    private void RetryClick(object sender, RoutedEventArgs args) => _reader.Stop();
    private void QuitClick(object sender, RoutedEventArgs args) => _reader.Quit();
    private void SampleClick(object sender, RoutedEventArgs args) => _reader.OpenSample();
}
