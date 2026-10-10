using LazyRead;

var checks = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(string text, string name) { try { ReadingRules.Validate(text); throw new Exception("Accepted " + name); } catch (InvalidOperationException) { checks++; Console.WriteLine("PASS " + name); } }
Assert(ReadingRules.Validate("\0 Hello 世界 ") == "Hello 世界", "capture preserves Unicode");
Reject(" \0 ", "empty selection is rejected");
Reject(new string('x', 12001), "oversized text is rejected instead of truncated");
Assert(ReadingRules.MayRestoreClipboard(11, 11), "owned clipboard may be restored");
Assert(!ReadingRules.MayRestoreClipboard(11, 12), "newer clipboard data is preserved");
Assert(!ReadingRules.AllowCopyFallback("pwsh", "Document", false, true), "PowerShell never receives copy injection");
Assert(!ReadingRules.AllowCopyFallback("WindowsTerminal", "Document", false, true), "terminal process is excluded");
Assert(!ReadingRules.AllowCopyFallback("Code", "Integrated Terminal", false, true), "editor terminal surface is excluded");
Assert(!ReadingRules.AllowCopyFallback("chrome", "Edit", true, true), "password controls are excluded");
Assert(!ReadingRules.AllowCopyFallback("chrome", "Document", false, false), "no copy fallback without an explicit reading request");
Assert(ReadingRules.AllowCopyFallback("chrome", "Document", false, true), "browser hotkey request permits guarded copy fallback");
Assert(ReadingRules.AllowCopyFallback("T3 Code (Alpha)", "Document", false, true), "T3 Code's installed process name permits guarded copy fallback");
Assert(System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>() == 40, "64-bit SendInput ABI size");
Assert(HotkeySpec.Default.Label == "Ctrl+Alt+F8", "default mouse macro shortcut");
Assert(!HotkeySpec.Valid(0x52, 0), "typing a letter alone cannot trigger global reading");
Assert(!HotkeySpec.Valid(0x52, 8), "Windows-key combinations are excluded");
Assert(HotkeySpec.Valid(0x7C, 0) && new HotkeySpec(0x7C, 0).Label == "F13", "macro-only function keys are supported");
Assert(GenerationEstimate.Remaining(10, 3) == "≈ 7s", "countdown decreases with elapsed time");
Assert(GenerationEstimate.Remaining(10, 12) == "Finishing…", "estimate expiry does not claim completion");
var estimate = new GenerationEstimate(); var initial = estimate.Predict(100); estimate.Observe(100, 50);
Assert(estimate.Predict(100) > initial, "timer learns from actual local generation time");
var learned = estimate.Predict(100); estimate.Observe(100, double.NaN);
Assert(estimate.Predict(100) == learned, "invalid timing sample does not corrupt the countdown");
Assert(new ReaderSettings().Voice == "af_heart" && VoiceChoice.Engine == "Kokoro v1.0", "new settings default to Kokoro's af_heart voice");
Assert(Array.TrueForAll(VoiceChoice.Options, v => v.Length > 3 && "ab".Contains(v[0]) && "fm".Contains(v[1]) && v[2] == '_'), "only English Kokoro voices are offered");
Assert(new ReaderSettings { Voice = "bm_george" }.Voice == "bm_george", "a chosen Kokoro voice is kept");
var legacy = System.Text.Json.JsonSerializer.Deserialize<ReaderSettings>("{\"Voice\":\"Sohee\",\"Language\":\"Auto\",\"Model\":\"Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice\",\"ModelVoices\":{},\"Speed\":1.25}")!;
Assert(legacy.Voice == "af_heart" && legacy.Speed == 1.25, "settings saved for the removed models load with Kokoro's default voice");
var fresh = new GenerationEstimate(); fresh.Observe(100, 41.5); fresh.ObserveAudio(100, 9);
Assert(Math.Abs(fresh.Predict(100) - 41.5) < 0.01 && Math.Abs(fresh.PredictAudio(100) - 9) < 0.01, "first measured passage replaces default timing for slower devices");
var converged = new GenerationEstimate(); for (var i = 0; i < 12; i++) { converged.Observe(100, 11.5); converged.ObserveAudio(100, 6.2); }
Assert(Math.Abs(converged.Predict(100) - 11.5) < 0.1 && Math.Abs(converged.PredictAudio(100) - 6.2) < 0.05, "estimates converge on observed generation and speech length");
Assert(PlaybackBuffer.RequiredDelay(12, Array.Empty<(double, double)>(), 1) == 0, "fully generated reading plays immediately");
Assert(PlaybackBuffer.RequiredDelay(10, new[] { (2.0, 10.0), (2.0, 10.0) }, 1) == 0, "generation faster than speech needs no extra wait");
var slow = new[] { (19.0, 10.0), (19.0, 10.0), (19.0, 10.0), (19.0, 10.0) };
var wait = PlaybackBuffer.RequiredDelay(10, slow, 1);
Assert(Enumerable.Range(1, slow.Length).All(j => 19.0 * j <= wait + 10.0 * j) && wait < 60, "slow generation waits just enough for gapless playback");
Assert(PlaybackBuffer.RequiredDelay(10, slow, 1.5) > wait, "faster playback speed buffers longer");
var screen = new System.Drawing.Rectangle(0, 0, 1920, 1040);
Assert(CursorIndicator.Place(800, 500, 260, 52, screen) == (532, 522), "badge sits below and left of the cursor");
Assert(CursorIndicator.Place(100, 500, 260, 52, screen) == (122, 522), "badge flips right at the left screen edge");
Assert(CursorIndicator.Place(800, 1030, 260, 52, screen) == (532, 960), "badge flips above at the bottom screen edge");
Assert(CursorIndicator.Place(3000, 500, 260, 52, new System.Drawing.Rectangle(2560, 0, 2560, 1440)) == (2732, 522), "badge stays on a secondary monitor");
Console.WriteLine($"{checks} checks passed.");
