using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace LazyRead;

public partial class App : Application
{
    private Mutex? _instance;
    private ReaderController? _reader;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "Lazy Read"); args.Handled = true; };
        if (e.Args.Contains("--sample"))
        {
            var sample = new SampleWindow();
            sample.Closed += (_, _) => Shutdown();
            sample.Show(); return;
        }
        _instance = new Mutex(true, "Local\\LazyRead.Desktop", out var created);
        if (!created) { MessageBox.Show("Lazy Read is already running. Open it from the system tray.", "Lazy Read"); Shutdown(); return; }
        _reader = new ReaderController();
        _reader.OpenMain();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _reader?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
