# Lazy Read

A Windows tray app that reads highlighted text with **Qwen3-TTS running locally**. Highlight text and press **Ctrl+Alt+F8**, or assign that shortcut to a mouse macro. No Chrome extension or browser speech API is involved. Right-click always keeps the application's own menu.

A small status badge follows the cursor while speech is generated and disappears during playback. It never takes focus or intercepts clicks. Its **≈ seconds** countdown is an estimate learned from actual generation times; if the estimate expires it says **Finishing…** until audio is ready. Model loading uses an elapsed timer because download/startup duration is unknown. Press the reading shortcut again during loading, generation, or playback to cancel and unload the model. The badge shows **Cancelling…**, then confirms **Cancelled · model unloaded** after the worker exits. Playback also has pause, resume, stop, and speed controls in settings and the tray.

## Install and run

Windows 10/11 x64 and Python **3.10** with its `py` launcher (`winget install Python.Python.3.10`) are required. An NVIDIA GPU with a CUDA-compatible driver is recommended. The initial configuration uses CUDA 12.8 PyTorch and the **Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice** model. CPU inference is available but substantially slower; Setup selects it automatically when no NVIDIA driver is found. Keep several GB of disk space available for packages and model files.

### From a release (no .NET SDK needed)

Download `LazyRead-win-x64.zip` from the repository's **Releases** page, extract it to a folder you can write to (for example `Documents\LazyRead`), then run in that folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Setup.ps1
```

The zip contains the prebuilt self-contained executable; Setup installs the isolated Python environment in `.venv/`. If Windows SmartScreen warns about the unsigned app, choose **More info → Run anyway**. Releases are built by GitHub Actions when a `v*` tag is pushed; build outputs are never committed to the repository.

### From source

Source setup also requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run the same `Setup.ps1` command in the repository folder. It publishes a self-contained desktop executable in `app/` and installs the Python environment. For CPU-only installation on an NVIDIA system, add `-Cpu`.

### Run

Keep `app/`, `engine/`, and `.venv/` together in the same folder and double-click **Start Lazy Read.cmd**. The app starts with the model unloaded. Each reading loads Qwen3 into a separate process; the first reading downloads the model from Hugging Face and can take several minutes. Later readings reuse the downloaded files but reload the model into memory. After the last audio finishes, the worker exits automatically, releasing CPU/GPU memory. Cancellation also exits the worker, including during loading.

1. Highlight text in the application you are using.
2. Press **Ctrl+Alt+F8**, or use your mouse macro that sends this key combination and releases it.
3. Watch the cursor badge as speech is prepared; audio plays automatically and the badge disappears.
4. Press the same hotkey again to cancel and unload, or use **Pause**, **Resume**, or **Stop** in settings or the tray. After normal playback, the model unloads automatically.

Use **Model** in settings to choose **Qwen3-TTS 1.7B** (best quality, the default) or the smaller **Qwen3-TTS 0.6B** (faster, lighter). The choice is saved and applies from the next reading; a model's files download on its first use. Use **Change shortcut** to record another combination. Ctrl/Alt with a letter or number and function keys (including F13–F24 for macros) are supported. Escape cancels recording. Conflicts with another app are explained without replacing your working shortcut. You can disable the global shortcut with its checkbox. Closing settings keeps the app in the tray; choose **Quit** to unload the model. **Sample document** opens a safe test document in a separate process.

## Selection compatibility

Lazy Read first uses Windows UI Automation's selection API in the foreground application. For Chrome, Edge, Firefox, T3 Code, VS Code, Notepad, Word, and Acrobat, an explicit hotkey request can also use a guarded Ctrl+C fallback when selection APIs are unavailable. The fallback preserves available clipboard formats, checks for a fresh copy, and avoids overwriting a later clipboard update. A known empty selection is rejected. It does not send copy shortcuts to password or terminal controls.

Support depends on each application's accessibility implementation. Image text, canvas-only editors, protected content, and some custom controls may not expose a selection. A normal, unelevated app cannot reliably read elevated applications. Editors without accessible selection information may copy the current line when nothing is selected, so highlight the intended passage before invoking your macro.

Code is sent as the literal selected text. Qwen may pronounce punctuation and identifiers inconsistently; Lazy Read does not rewrite your code or turn it into a summary. Readings are limited to 12,000 characters and divided into small passages for earlier playback and cancellation. Long passages and CPU inference can take longer than a few seconds.

## Local data

Selected text travels over a private parent/child process pipe to the local Python worker. There is no HTTP service or cloud inference. Package and model downloads require network access initially. Cached inference also works with the network disabled.

Settings and engine diagnostics live in `%LOCALAPPDATA%\LazyRead`. Selected text is not logged or saved. Temporary WAV files are deleted after playback or when that app instance exits. Model weights use Hugging Face's normal cache. The model and dependencies retain their own licenses; the app code is MIT licensed. See the [official Qwen3-TTS repository](https://github.com/QwenLM/Qwen3-TTS) for model details.

## Development and verification

```powershell
dotnet build src/LazyRead/LazyRead.csproj -c Release
dotnet run --project tests/LazyRead.Checks -c Release
.\.venv\Scripts\python.exe -m unittest discover -s engine/tests
.\.venv\Scripts\python.exe engine/verify_local.py
.\.venv\Scripts\python.exe engine/verify_local.py --offline
```

The last two commands are opt-in integration checks using the real model and synthetic text, including cancellation, replacement, Unicode, and cached offline inference. Evidence is saved under ignored `artifacts/`. Launch `LazyRead.exe --sample` in a separate process for a sample document with prose and code for safe native selection testing.

If loading fails, inspect `%LOCALAPPDATA%\LazyRead\engine.log`, click **Reset engine**, then try reading again. Use the CUDA install on NVIDIA systems and `Setup.ps1 -Cpu` on CPU-only systems. FlashAttention and SoX executables are not required by this app's synthesis path.
