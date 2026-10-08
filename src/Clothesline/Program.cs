namespace Clothesline;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--snapshot"))
        {
            var snapApp = new App();
            snapApp.InitializeComponent();
            return Snapshot.Run(args);
        }
        using var mutex = new Mutex(true, @"Local\Clothesline.SingleInstance", out bool first);
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Clothesline.Toggle");
        if (!first)
        {
            // Already running: bring the line down and leave.
            signal.Set();
            return 0;
        }
        App.ToggleSignal = signal;
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
