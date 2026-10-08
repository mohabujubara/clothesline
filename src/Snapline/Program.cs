namespace Snapline;

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
        if (args.Length == 2 && args[0] == "--markup-test")
        {
            var testApp = new App();
            testApp.InitializeComponent();
            return testApp.Run();
        }
        if (args.Length == 3 && args[0] == "--note-test")
        {
            // Draws a sticky note into a PNG: for checking the paper without a desktop.
            Core.Notes.Render(new Core.NoteData { Text = args[2], Color = "yellow" }, System.IO.Path.GetFullPath(args[1]));
            return 0;
        }
        if (args.Length >= 2 && args[0] is "--demo" or "--hero" or "--hero-dark")
        {
            var artApp = new App();
            artApp.InitializeComponent();
            // --title "Name" puts another name on the banner and the title card.
            int ti = Array.IndexOf(args, "--title");
            if (ti >= 0 && ti + 1 < args.Length) Demo.Title = args[ti + 1];
            try
            {
                return args[0] == "--demo" ? Demo.RunDemo(System.IO.Path.GetFullPath(args[1]))
                     : Demo.RunHero(System.IO.Path.GetFullPath(args[1]), dark: args[0] == "--hero-dark");
            }
            catch (Exception e) { Core.Log.Error($"Art failed: {e}"); return 1; }
        }
        if (args.Contains("--snapshot"))
        {
            var snapApp = new App();
            snapApp.InitializeComponent();
            try { return Snapshot.Run(args); }
            catch (Exception e) { Core.Log.Error($"Snapshot failed: {e}"); return 1; }
        }
        using var mutex = new Mutex(true, @"Local\Snapline.SingleInstance", out bool first);
        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Snapline.Toggle");
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
