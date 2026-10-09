using System.Windows;
using Snapline.Core;
using Snapline.UI;

namespace Snapline;

public partial class App : Application
{
    private AppController? _controller;
    public static EventWaitHandle? ToggleSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? lastError = null;
        var lastErrorAt = DateTime.MinValue;
        DispatcherUnhandledException += (_, ex) =>
        {
            // The same failure every tick would fill the log; one line a second is enough.
            var text = ex.Exception.ToString();
            if (text != lastError || (DateTime.Now - lastErrorAt).TotalSeconds > 1)
            {
                Log.Error($"Unhandled: {text}");
                lastError = text;
                lastErrorAt = DateTime.Now;
            }
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error($"Fatal: {ex.ExceptionObject}");

        if (e.Args.Length == 2 && e.Args[0] == "--markup-test")
        {
            bool ok = UI.MarkupWindow.SelfTest(System.IO.Path.GetFullPath(e.Args[1]), new Line(persist: false));
            Shutdown(ok ? 0 : 1);
            return;
        }
        try
        {
            _controller = new AppController();
        }
        catch (Exception ex)
        {
            // Without a tray icon there would be nothing to quit: say what happened and leave.
            Log.Error($"Could not start: {ex}");
            MessageBox.Show($"{Strings.AppName} could not start.\n\n{ex.Message}\n\n{Log.Location}", Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // A second launch asks the running one to show the line.
        if (ToggleSignal is not null)
        {
            var signal = ToggleSignal;
            var thread = new Thread(() =>
            {
                while (signal.WaitOne())
                    Dispatcher.BeginInvoke(() => _controller?.Toggle());
            }) { IsBackground = true, Name = "Snapline toggle" };
            thread.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
