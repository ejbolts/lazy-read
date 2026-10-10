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
        if (!created)
        {
            // A second launch, such as the taskbar icon, asks the running instance to show its window.
            try { EventWaitHandle.OpenExisting("Local\\LazyRead.Show").Set(); }
            catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("Lazy Read is already running. Open it from the system tray.", "Lazy Read"); }
            Shutdown(); return;
        }
        _reader = new ReaderController();
        var reader = _reader; var dispatcher = Dispatcher;
        // Each signal from a second launch opens the window on the UI thread; the thread is background so it never delays exit.
        var show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LazyRead.Show");
        new Thread(() => { while (show.WaitOne()) dispatcher.BeginInvoke(reader.OpenMain); }) { IsBackground = true }.Start();
        // Sign-in startup stays in the tray; the window opens from the tray icon.
        if (!e.Args.Contains("--background")) reader.OpenMain();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _reader?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
