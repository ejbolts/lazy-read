using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazyRead;

public record CapturedText(string Text, string Source, nint Window, Native.Point Position);

public static class ReadingRules
{
    public const int MaxCharacters = 12000;
    public static string Validate(string text)
    {
        var result = text.Replace("\0", "").Trim();
        if (result.Length == 0) throw new InvalidOperationException("No text selected. Highlight some text and try again.");
        if (result.Length > MaxCharacters) throw new InvalidOperationException("Select a shorter passage (up to 12,000 characters).");
        return result;
    }
    public static bool MayRestoreClipboard(uint capturedSequence, uint currentSequence) => capturedSequence == currentSequence;
    public static bool AllowCopyFallback(string process, string surface, bool password, bool explicitRequest)
    {
        if (password || !explicitRequest || surface.Contains("terminal", StringComparison.OrdinalIgnoreCase) || surface.Contains("console", StringComparison.OrdinalIgnoreCase)) return false;
        return new[] { "chrome", "msedge", "firefox", "t3-code", "t3 code", "t3 code (alpha)", "t3code", "code", "notepad", "winword", "acrobat", "acrord32" }.Contains(process.ToLowerInvariant());
    }
}

public sealed class ReaderSettings
{
    public string Voice { get; set; } = "Ryan";
    public string Language { get; set; } = "English";
    public double Speed { get; set; } = 1.0;
    public bool Enabled { get; set; } = true;
    public uint ShortcutKey { get; set; } = 0x77;
    public uint ShortcutModifiers { get; set; } = 3;
    [JsonIgnore] public HotkeySpec Shortcut => HotkeySpec.Valid(ShortcutKey, ShortcutModifiers) ? new(ShortcutKey, ShortcutModifiers) : HotkeySpec.Default;
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LazyRead");
    public static ReaderSettings Load()
    {
        try { return JsonSerializer.Deserialize<ReaderSettings>(File.ReadAllText(Path.Combine(DirectoryPath, "settings.json"))) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "settings.json"), JsonSerializer.Serialize(this));
    }
}
