# Lazy Read

Standalone Windows WPF application with an isolated Python Kokoro (kokoro-onnx) worker.

- Build: `dotnet build src/LazyRead/LazyRead.csproj -c Release`
- Checks: `dotnet run --project tests/LazyRead.Checks -c Release` and `.venv/Scripts/python.exe -m unittest discover -s engine/tests`
- Use sample text only for UI verification. Never log or commit selected text, settings, model weights, audio, or verification artifacts.
- Preserve clipboard formats and newer clipboard changes during fallback capture. Never issue copy shortcuts to terminal/password controls.
- UI evidence goes in ignored `artifacts/visual-review/initial-app/`. Native Windows interaction requires native verification; a browser mockup is insufficient.
- Keep inference local. Network access is limited to first-time package/model downloads.
- Push the task branch after the shared verification gates pass.
