# Lazy Read

A Windows tray app that reads highlighted text with **Kokoro v1.0 running locally**. Highlight text and press **Ctrl+Alt+F8**, or assign that shortcut to a mouse macro. No Chrome extension or browser speech API is involved. Right-click always keeps the application's own menu.

A small status badge follows the cursor while speech is generated and disappears during playback. It never takes focus or intercepts clicks. Its **≈ seconds** countdown is an estimate learned from actual generation times; if the estimate expires it says **Finishing…** until audio is ready. Model loading uses an elapsed timer because download/startup duration is unknown. Press the reading shortcut again during loading, generation, or playback to cancel and unload the model. The badge shows **Cancelling…**, then confirms **Cancelled · model unloaded** after the worker exits. Playback also has pause, resume, stop, and speed controls in settings and the tray.

## Install and run

Windows 10/11 x64 and Python **3.10** with its `py` launcher (`winget install Python.Python.3.10`) are required. Speech runs on the CPU through ONNX Runtime, so no GPU, CUDA or PyTorch is needed. Keep about 600 MB of disk space available for the Python packages (about 210 MB) and the Kokoro model files (about 350 MB).

### From a release (no .NET SDK needed)

Download `LazyRead-win-x64.zip` from the repository's **Releases** page, extract it to a folder you can write to (for example `Documents\LazyRead`), then run in that folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Setup.ps1
```

The zip contains the prebuilt self-contained executable; Setup installs the isolated Python environment in `.venv/`. If Windows SmartScreen warns about the unsigned app, choose **More info → Run anyway**. Releases are built by GitHub Actions when a `v*` tag is pushed; build outputs are never committed to the repository.

### From source

Source setup also requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run the same `Setup.ps1` command in the repository folder. It publishes a self-contained desktop executable in `app/` and installs the Python environment.

### Run

Keep `app/`, `engine/`, and `.venv/` together in the same folder and double-click **Start Lazy Read.cmd**. The app starts with the model unloaded. Each reading loads Kokoro into a separate process, which takes about a second. The first reading downloads the model files from the kokoro-onnx GitHub release and checks them against pinned SHA-256 hashes. Later readings reuse the downloaded files. After the last audio finishes, the worker exits automatically, releasing its memory. Cancellation also exits the worker, including during loading.

1. Highlight text in the application you are using.
2. Press **Ctrl+Alt+F8**, or use your mouse macro that sends this key combination and releases it.
3. Watch the cursor badge as speech is prepared; audio plays automatically and the badge disappears.
4. Press the same hotkey again to cancel and unload, or use **Pause**, **Resume**, or **Stop** in settings or the tray. After normal playback, the model unloads automatically.

Use **Voice** in settings to choose an English Kokoro voice. The prefix gives the accent and voice: `af`/`am` American female/male and `bf`/`bm` British female/male. **af_heart** is the default. Use **Change shortcut** to record another combination. Ctrl/Alt with a letter or number and function keys (including F13–F24 for macros) are supported. Escape cancels recording. Conflicts with another app are explained without replacing your working shortcut. You can disable the global shortcut with its checkbox. Closing settings keeps the app in the tray; choose **Quit** to unload the model. **Sample document** opens a safe test document in a separate process.

## Selection compatibility

Lazy Read first uses Windows UI Automation's selection API in the foreground application. For Chrome, Edge, Firefox, T3 Code, VS Code, Notepad, Word, and Acrobat, an explicit hotkey request can also use a guarded Ctrl+C fallback when selection APIs are unavailable. The fallback preserves available clipboard formats, checks for a fresh copy, and avoids overwriting a later clipboard update. A known empty selection is rejected. It does not send copy shortcuts to password or terminal controls.

Support depends on each application's accessibility implementation. Image text, canvas-only editors, protected content, and some custom controls may not expose a selection. A normal, unelevated app cannot reliably read elevated applications. Editors without accessible selection information may copy the current line when nothing is selected, so highlight the intended passage before invoking your macro.

Code is sent as the literal selected text. Kokoro may pronounce punctuation and identifiers inconsistently; Lazy Read does not rewrite your code or turn it into a summary. Readings are limited to 12,000 characters and divided into small passages for earlier playback and cancellation. Long passages and slower CPUs can take longer than a few seconds.

## Local data

Selected text travels over a private parent/child process pipe to the local Python worker. There is no HTTP service or cloud inference. Package and model downloads require network access initially. Cached inference also works with the network disabled.

Settings and engine diagnostics live in `%LOCALAPPDATA%\LazyRead`. Selected text is not logged or saved. Temporary WAV files are deleted after playback or when that app instance exits. Kokoro's model files live in `%LOCALAPPDATA%\LazyRead\models`. The model and dependencies retain their own licenses; the app code is MIT licensed. See [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) and [kokoro-onnx](https://github.com/thewh1teagle/kokoro-onnx) for model details.

## Development and verification

```powershell
dotnet build src/LazyRead/LazyRead.csproj -c Release
dotnet run --project tests/LazyRead.Checks -c Release
.\.venv\Scripts\python.exe -m unittest discover -s engine/tests
.\.venv\Scripts\python.exe engine/verify_local.py
.\.venv\Scripts\python.exe engine/verify_local.py --offline
```

`tools/load_benchmark.py` times how long Kokoro takes to load into memory in a fresh worker, as happens for every reading. Example: `.\.venv\Scripts\python.exe tools/load_benchmark.py 5`.

The last two `verify_local.py` commands are opt-in integration checks using the real model and synthetic text, including cancellation, replacement, Unicode, and cached offline inference. Evidence is saved under ignored `artifacts/`. Launch `LazyRead.exe --sample` in a separate process for a sample document with prose and code for safe native selection testing.

If loading fails, inspect `%LOCALAPPDATA%\LazyRead\engine.log`, click **Reset engine**, then try reading again. If the model files were damaged, delete `%LOCALAPPDATA%\LazyRead\models` and read again to download them.
