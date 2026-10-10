using System;
using System.Collections.Generic;
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

public sealed record ModelOption(string Id, string Label, string Name, string[] Voices, string[] Languages);

public static class ModelChoice
{
    public const string DefaultId = "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice";
    public const string KokoroId = "kokoro-onnx/kokoro-v1.0";
    public const string PocketId = "kyutai/pocket-tts";
    private static readonly string[] QwenVoices = { "Ryan", "Aiden", "Vivian", "Serena", "Uncle_Fu", "Dylan", "Eric", "Ono_Anna", "Sohee" };
    private static readonly string[] QwenLanguages = { "Auto", "English", "Chinese", "Japanese", "Korean", "German", "French", "Russian", "Portuguese", "Spanish", "Italian" };
    private static readonly string[] English = { "English" };
    public static readonly ModelOption[] Options =
    {
        new(DefaultId, "Qwen3-TTS 1.7B (best quality)", "Qwen3-TTS 1.7B", QwenVoices, QwenLanguages),
        new("Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice", "Qwen3-TTS 0.6B (faster, lighter)", "Qwen3-TTS 0.6B", QwenVoices, QwenLanguages),
        // Kokoro voice prefixes: af/am American female/male, bf/bm British female/male.
        new(KokoroId, "Kokoro v1.0 (test, CPU)", "Kokoro v1.0", new[] { "af_heart", "af_bella", "af_nicole", "af_sarah", "af_sky", "am_michael", "am_fenrir", "am_puck", "am_adam", "bf_emma", "bf_isabella", "bm_george", "bm_fable", "bm_lewis" }, English),
        new(PocketId, "Pocket TTS (test, CPU)", "Pocket TTS", new[] { "alba", "marius", "javert", "jean", "cosette", "anna", "vera", "fantine", "charles", "paul", "eponine", "azelma", "george", "mary", "jane", "michael", "eve" }, English),
    };
    public static ModelOption Find(string? id) => Array.Find(Options, option => option.Id == id) ?? Options[0];
    // Voices and languages belong to a model; switching models falls back to its first voice and to English.
    public static string PickVoice(ModelOption model, string? voice) => Array.IndexOf(model.Voices, voice) >= 0 ? voice! : model.Voices[0];
    public static string PickLanguage(ModelOption model, string? language) => Array.IndexOf(model.Languages, language) >= 0 ? language! : "English";
}

public sealed class ReaderSettings
{
    private string _model = ModelChoice.DefaultId;
    public string Voice { get; set; } = "Ryan";
    public string Language { get; set; } = "English";
    public string Model { get => ModelChoice.Find(_model).Id; set => _model = value; }
    // Each model's last voice, so comparing models does not lose the voice chosen for another.
    public Dictionary<string, string> ModelVoices { get; set; } = new();
    public string VoiceFor(ModelOption model) => ModelChoice.PickVoice(model, ModelVoices.GetValueOrDefault(model.Id, Voice));
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
