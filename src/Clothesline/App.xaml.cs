using System.Windows;
using Clothesline.Core;

namespace Clothesline;

public partial class App : Application
{
    private AppController? _controller;
    public static EventWaitHandle? ToggleSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error($"Unhandled: {ex.Exception}");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error($"Fatal: {ex.ExceptionObject}");

        if (e.Args.Length == 2 && e.Args[0] == "--markup-test")
        {
            bool ok = UI.MarkupWindow.SelfTest(System.IO.Path.GetFullPath(e.Args[1]), new Line(persist: false));
            Shutdown(ok ? 0 : 1);
            return;
        }
        _controller = new AppController();

        // A second launch asks the running one to show the line.
        if (ToggleSignal is not null)
        {
            var signal = ToggleSignal;
            var thread = new Thread(() =>
            {
                while (signal.WaitOne())
                    Dispatcher.BeginInvoke(() => _controller?.Toggle());
            }) { IsBackground = true, Name = "Clothesline toggle" };
            thread.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
