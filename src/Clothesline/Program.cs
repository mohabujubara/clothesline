namespace Clothesline;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--ocr")
        {
            // Reads the text in an image into a file: for checking the OCR without a desktop.
            var text = Core.Ocr.ReadAsync(System.IO.Path.GetFullPath(args[1])).GetAwaiter().GetResult();
            System.IO.File.WriteAllText(args[2], text ?? "<ocr unavailable>");
            return text is null ? 1 : 0;
        }
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
