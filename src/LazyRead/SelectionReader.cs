using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace LazyRead;

public sealed class SelectionReader
{
    private record AccessibleResult(string Text, string Surface, bool Password, bool KnownEmpty = false);
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    public async Task<CapturedText?> Capture(nint source, Native.Point point)
    {
        if (Native.GetForegroundWindow() != source || !_probeLock.Wait(0)) return null;
        var process = Native.ProcessName(source);
        var probe = Task.Run(() =>
        {
            try { return ReadAccessible(source, point); }
            finally { _probeLock.Release(); }
        });
        // A hung accessibility provider must not freeze either the app or the mouse hook.
        if (await Task.WhenAny(probe, Task.Delay(650)) != probe) return null;
        AccessibleResult result;
        try { result = await probe; } catch { return null; }
        if (result.Password || result.KnownEmpty || Native.GetForegroundWindow() != source) return null;
        var text = result.Text;
        if (string.IsNullOrWhiteSpace(text) && ReadingRules.AllowCopyFallback(process, result.Surface, false, true))
            text = await CopySelection(source);
        if (string.IsNullOrWhiteSpace(text) || Native.GetForegroundWindow() != source) return null;
        return new CapturedText(ReadingRules.Validate(text), FriendlyName(process), source, point);
    }
    private static string FriendlyName(string process) => process.ToLowerInvariant() switch
    {
        "chrome" => "Chrome", "msedge" => "Microsoft Edge", "code" => "Visual Studio Code",
        "t3 code" or "t3 code (alpha)" or "t3-code" or "t3code" => "T3 Code", "notepad" => "Notepad", "lazyread" => "Sample document", _ => process
    };
    private static AccessibleResult ReadAccessible(nint source, Native.Point point)
    {
        var focused = AutomationElement.FocusedElement;
        Native.GetWindowThreadProcessId(source, out var sourcePid);
        if (focused != null && focused.Current.ProcessId != sourcePid) focused = null;
        if (focused?.Current.IsPassword == true) return new("", "password", true);
        var surface = focused?.Current.ClassName + " " + focused?.Current.Name;
        var knownEmpty = false;
        if (focused != null)
        {
            if (TrySelection(focused, out var selected, out var exposed)) return new(selected, surface ?? "", false);
            knownEmpty = exposed;
        }
        var element = Native.GetAncestor(Native.WindowFromPoint(point), 2) == source ? AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y)) : null;
        for (var i = 0; element != null && i < 12; i++, element = TreeWalker.ControlViewWalker.GetParent(element))
        {
            if (element.Current.ProcessId != sourcePid) break;
            if (element.Current.IsPassword) return new("", "password", true);
            if (TrySelection(element, out var text, out var exposed)) return new(text, surface ?? "", false);
            knownEmpty |= exposed;
            if (element.Current.NativeWindowHandle == source) break;
        }
        return new("", surface ?? "", false, knownEmpty);
    }
    private static bool TrySelection(AutomationElement element, out string text, out bool exposed)
    {
        text = ""; exposed = false;
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var obj)) return false;
        var pattern = (TextPattern)obj;
        if (pattern.SupportedTextSelection == SupportedTextSelection.None) return false;
        exposed = true;
        foreach (var selection in pattern.GetSelection())
        {
            text += selection.GetText(ReadingRules.MaxCharacters + 1);
            if (text.Length > ReadingRules.MaxCharacters) break;
        }
        return !string.IsNullOrWhiteSpace(text);
    }
    private static async Task<string> CopySelection(nint source)
    {
        if (Native.GetForegroundWindow() != source) return "";
        // Do not clear the clipboard: a sequence change distinguishes fresh copy from stale data.
        var before = Native.GetClipboardSequenceNumber();
        DataObject snapshot = new();
        try
        {
            var original = Clipboard.GetDataObject();
            if (original != null)
                foreach (var format in original.GetFormats(false))
                {
                    var data = original.GetData(format, false);
                    if (data is MemoryStream stream) data = new MemoryStream(stream.ToArray());
                    if (data != null) snapshot.SetData(format, data, false);
                }
        }
        catch { return ""; } // Abort if the original clipboard cannot safely be preserved.
        if (Native.GetClipboardSequenceNumber() != before) return "";
        if ((Native.GetAsyncKeyState(0x11) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x12) & 0x8000) != 0) return "";
        if (!Native.PressCopy()) return "";
        uint capturedSequence = before;
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(30);
            capturedSequence = Native.GetClipboardSequenceNumber();
            if (capturedSequence != before) break;
        }
        if (capturedSequence == before) return "";
        Native.GetWindowThreadProcessId(source, out var sourcePid);
        Native.GetWindowThreadProcessId(Native.GetClipboardOwner(), out var clipboardPid);
        if (sourcePid != clipboardPid) return ""; // Another app updated the clipboard; leave it intact.
        string text = "";
        try
        {
            if (Clipboard.ContainsText()) text = Clipboard.GetText();
            if (Native.GetClipboardSequenceNumber() != capturedSequence) text = "";
        }
        finally
        {
            if (ReadingRules.MayRestoreClipboard(capturedSequence, Native.GetClipboardSequenceNumber()))
                try { Clipboard.SetDataObject(snapshot, true); } catch { /* Clipboard temporarily busy. */ }
        }
        return text;
    }
}
