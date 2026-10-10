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

public static class VoiceChoice
{
    public const string Engine = "Kokoro v1.0";
    // Kokoro voice prefixes: af/am American female/male, bf/bm British female/male.
    public static readonly string[] Options = { "af_heart", "af_bella", "af_nicole", "af_sarah", "af_sky", "am_michael", "am_fenrir", "am_puck", "am_adam", "bf_emma", "bf_isabella", "bm_george", "bm_fable", "bm_lewis" };
    public static string Pick(string? voice) => Array.IndexOf(Options, voice) >= 0 ? voice! : Options[0];
}

public sealed class ReaderSettings
{
    private string _voice = VoiceChoice.Options[0];
    // Voices saved for earlier models fall back to Kokoro's default voice.
    public string Voice { get => VoiceChoice.Pick(_voice); set => _voice = value; }
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
