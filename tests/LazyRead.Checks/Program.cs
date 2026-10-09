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
Console.WriteLine($"{checks} checks passed.");
