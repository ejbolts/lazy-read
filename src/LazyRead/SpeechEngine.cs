using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LazyRead;

public sealed class SpeechEngine : IDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;
    public string AudioDirectory { get; } = Path.Combine(ReaderSettings.DirectoryPath, "audio", Guid.NewGuid().ToString("N"));
    public string Model { get; }
    public event Action<JsonElement>? Message;
    public SpeechEngine(string model) => Model = model;
    public static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "engine", "worker.py"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Cannot find the speech engine. Launch with Start Lazy Read.cmd in the project folder.");
    }
    public void Start()
    {
        if (_process is { HasExited: false }) return;
        var root = FindRoot();
        var python = Path.Combine(root, ".venv", "Scripts", "python.exe");
        if (!File.Exists(python)) throw new InvalidOperationException("Run Setup.ps1 once to install the local speech engine.");
        Directory.CreateDirectory(AudioDirectory);
        var info = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = root };
        info.ArgumentList.Add("-u"); info.ArgumentList.Add(Path.Combine(root, "engine", "worker.py"));
        info.ArgumentList.Add("--audio-dir"); info.ArgumentList.Add(AudioDirectory);
        info.ArgumentList.Add("--model"); info.ArgumentList.Add(Model);
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["HF_HUB_DISABLE_TELEMETRY"] = "1";
        _process = new Process { StartInfo = info, EnableRaisingEvents = true };
        _process.Exited += (_, _) => { if (!_disposed) EmitError("Speech engine stopped. Open Lazy Read and click Reset engine."); };
        _process.Start();
        _ = ReadMessages(_process);
        _ = ReadErrors(_process);
    }
    private async Task ReadMessages(Process process)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                try { using var document = JsonDocument.Parse(line); Message?.Invoke(document.RootElement.Clone()); }
                catch (JsonException) { /* Library banners belong on stderr, never become protocol events. */ }
            }
        }
        catch (Exception) { if (!_disposed) EmitError("Lost connection to the local engine. Click Reset engine."); }
    }
    private static async Task ReadErrors(Process process)
    {
        var path = Path.Combine(ReaderSettings.DirectoryPath, "engine.log");
        Directory.CreateDirectory(ReaderSettings.DirectoryPath);
        try
        {
            using var log = new StreamWriter(path, false, Encoding.UTF8);
            var lines = 0;
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                if (++lines < 2000) { await log.WriteLineAsync(line); await log.FlushAsync(); }
            }
        }
        catch { }
    }
    private void EmitError(string message)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { type = "error", message }));
        Message?.Invoke(json.RootElement.Clone());
    }
    public async Task Send(object message)
    {
        await _sendLock.WaitAsync();
        try
        {
            if (_process == null || _process.HasExited) throw new InvalidOperationException("The speech engine is not running. Click Reset engine.");
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));
            await _process.StandardInput.FlushAsync();
        }
        finally { _sendLock.Release(); }
    }
    public async Task Unload()
    {
        _disposed = true;
        if (_process is { HasExited: false })
        {
            _process.Kill(true);
            await _process.WaitForExitAsync();
        }
        Dispose();
    }
    public void Dispose()
    {
        _disposed = true;
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { }
        _process?.Dispose();
        // Only this instance's UUID directory is removed; other instances' data is untouched.
        try { if (Directory.Exists(AudioDirectory)) Directory.Delete(AudioDirectory, true); } catch { }
    }
}
