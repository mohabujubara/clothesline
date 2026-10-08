using System.Globalization;
using System.Windows;
using Clothesline.Core;

namespace Clothesline.UI;

/// <summary>
/// Every word the app shows, in English and Arabic. The language follows
/// Windows unless chosen in Settings.
/// </summary>
public static class Strings
{
    public static event Action? LanguageChanged;

    /// <summary>"en" or "ar".</summary>
    public static string Language { get; private set; } = Resolve();

    public static bool IsRtl => Language == "ar";
    public static FlowDirection Flow => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static void Refresh()
    {
        var was = Language;
        Language = Resolve();
        if (was != Language) LanguageChanged?.Invoke();
    }

    private static string Resolve()
    {
        var chosen = Core.Settings.Current.Language;
        if (chosen is "en" or "ar") return chosen;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";
    }

    private static string L(string en) => Language == "ar" && Ar.TryGetValue(en, out var ar) ? ar : en;

    public const string AppName = "Clothesline";
    public static string Tagline => L("Screenshots, hung out to dry.");

    public static string ShowLine => L("Show line");
    public static string HideLine => L("Hide line");
    public static string TakeEverythingDown => L("Take everything down");
    public static string NewCapture => L("New capture");
    public static string NewCaptureTip => L("Click to take a new screenshot (Win+Shift+S)");
    public static string CatchClipboard => L("Catch clipboard captures");
    public static string CatchClipboardTip => L("Win+Shift+S and PrtScn captures hang even when no file is saved");
    public static string StayDownWhileUnused => L("Stay down until each capture is used");
    public static string StayDownWhileUnusedTip => L("The line waits while a capture has not been copied, dragged or opened");
    public static string TakeDownAfterDrag => L("Take down after dragging into an app");
    public static string TakeDownAfterDragTip => L("A photo dropped into a chat or an app leaves the line, like one saved to a folder");
    public static string RevealAtTopEdge => L("Bring the line down when the pointer rests at the top edge");
    public static string RevealAtTopEdgeTip => L("Hold the pointer still against the top of the screen for half a second. Off, the shortcut and the tray icon show the line.");
    public static string OpenScreenshotsFolder => L("Open Screenshots folder");
    public static string OpenInboxFolder => L("Open caught captures folder");
    public static string Sounds => L("Sounds");
    public static string StartWithWindows => L("Start with Windows");
    public static string Settings => L("Settings…");
    public static string About => L("About Clothesline");
    public static string Quit => L("Quit Clothesline");

    public static string Copy => L("Copy");
    public static string CopyText => L("Copy text");
    public static string Open => L("Open");
    public static string Edit => L("Edit");
    public static string ShowInExplorer => L("Show in Explorer");
    public static string KeepOnLine => L("Keep on the line");
    public static string SaveToDesktop => L("Save to Desktop");
    public static string SaveToScreenshots => L("Save to Pictures\\Screenshots");
    public static string Discard => L("Discard");
    public static string TakeDown => L("Take down");
    public static string MoveToRecycleBin => L("Move to Recycle Bin");

    public static string Hint => L("Take a screenshot and it will hang here");
    public static string Copied => L("Copied");
    public static string TextCopied => L("Text copied");
    public static string JustNow => L("just now");
    public static string MinutesAgo => L("{0} min ago");
    public static string HoursAgo => L("{0} h ago");

    public static string SettingsTitle => L("Settings");
    public static string SectionLine => L("The line");
    public static string SectionLook => L("Look");
    public static string SectionCaptures => L("Captures");
    public static string SectionGeneral => L("General");
    public static string Shortcut => L("Show or hide the line");
    public static string ShortcutTip => L("Click here and press the keys you want, like Ctrl+Alt+T");
    public static string LineDistance => L("Distance from the top of the screen");
    public static string LineDistanceTip => L("You can also drag the line itself up or down");
    public static string RopeColor => L("Line colour");
    public static string PegStyle => L("Clothespins");
    public static string Bows => L("Bows at the ends of the line");
    public static string Appearance => L("Appearance");
    public static string LanguageLabel => L("Language");
    public static string Auto => L("Follow Windows");
    public static string Light => L("Light");
    public static string Dark => L("Dark");
    public static string CaughtCapturesFolder => L("Caught captures are saved to");
    public static string Change => L("Change…");
    public static string WatchFoldersLabel => L("Folders watched for new screenshots");
    public static string AddFolder => L("Add folder…");
    public static string RemoveFolder => L("Remove");
    public static string SettingsFooter => L("Changes apply right away. Everything lives in {0}.");

    public static string WelcomeTitle => L("Clothesline is on the line");
    public static string WelcomeBody => L("Take a screenshot and it hangs at the top of the screen. Rest the pointer against the top edge to bring the line down, or press {0}.");

    public static string AboutBody => L(
        "Click a photo to copy it. Press and hold to edit it. Double click to open it.\n" +
        "Drag it along the line to reorder, into an app to send a copy, into a folder to keep it, or to the Recycle Bin to let it go.\n" +
        "Rest the pointer against the top edge of the screen, or press {0}, to bring the line down.");

    // Colour and peg names
    public static string ColorName(string key) => L(key switch
    {
        "bronze" => "Bronze", "gray" => "Gray", "black" => "Black", "white" => "White", "red" => "Red",
        "blue" => "Blue", "green" => "Green", "gold" => "Gold", "pink" => "Pink", "purple" => "Purple", _ => key,
    });

    public static string PegName(string key) => L(key switch
    {
        "wood" => "Wooden", "metal" => "Aluminium", "mixed" => "Coloured plastic, mixed", "red" => "Red plastic",
        "blue" => "Blue plastic", "green" => "Green plastic", "yellow" => "Yellow plastic", _ => key,
    });

    private static readonly Dictionary<string, string> Ar = new()
    {
        ["Screenshots, hung out to dry."] = "لقطات الشاشة، معلّقة على الحبل.",
        ["Show line"] = "إظهار الحبل",
        ["Hide line"] = "إخفاء الحبل",
        ["Take everything down"] = "إنزال كل اللقطات",
        ["New capture"] = "لقطة جديدة",
        ["Click to take a new screenshot (Win+Shift+S)"] = "انقر لالتقاط لقطة شاشة جديدة (Win+Shift+S)",
        ["Catch clipboard captures"] = "التقاط صور الحافظة",
        ["Win+Shift+S and PrtScn captures hang even when no file is saved"] = "لقطات Win+Shift+S وPrtScn تُعلَّق حتى لو لم يُحفظ ملف",
        ["Stay down until each capture is used"] = "يبقى الحبل ظاهرًا حتى تُستخدم كل لقطة",
        ["The line waits while a capture has not been copied, dragged or opened"] = "ينتظر الحبل ما دامت هناك لقطة لم تُنسخ أو تُسحب أو تُفتح",
        ["Take down after dragging into an app"] = "إنزال اللقطة بعد سحبها إلى تطبيق",
        ["A photo dropped into a chat or an app leaves the line, like one saved to a folder"] = "الصورة التي تُفلت في محادثة أو تطبيق تغادر الحبل، كالتي تُحفظ في مجلد",
        ["Bring the line down when the pointer rests at the top edge"] = "إنزال الحبل عندما يستقر المؤشر عند الحافة العلوية",
        ["Hold the pointer still against the top of the screen for half a second. Off, the shortcut and the tray icon show the line."] = "ثبّت المؤشر عند أعلى الشاشة نصف ثانية. عند الإيقاف، يُظهر الحبلَ الاختصارُ وأيقونةُ شريط المهام.",
        ["Open Screenshots folder"] = "فتح مجلد لقطات الشاشة",
        ["Open caught captures folder"] = "فتح مجلد اللقطات الملتقطة",
        ["Sounds"] = "الأصوات",
        ["Start with Windows"] = "التشغيل مع Windows",
        ["Settings…"] = "الإعدادات…",
        ["About Clothesline"] = "حول Clothesline",
        ["Quit Clothesline"] = "إنهاء Clothesline",
        ["Copy"] = "نسخ",
        ["Copy text"] = "نسخ النص",
        ["Open"] = "فتح",
        ["Edit"] = "تحرير",
        ["Show in Explorer"] = "إظهار في مستكشف الملفات",
        ["Keep on the line"] = "إبقاؤها على الحبل",
        ["Save to Desktop"] = "حفظ على سطح المكتب",
        ["Save to Pictures\\Screenshots"] = "حفظ في الصور\\لقطات الشاشة",
        ["Discard"] = "تجاهل",
        ["Take down"] = "إنزال",
        ["Move to Recycle Bin"] = "نقل إلى سلة المحذوفات",
        ["Take a screenshot and it will hang here"] = "التقط لقطة شاشة وستُعلَّق هنا",
        ["Copied"] = "تم النسخ",
        ["Text copied"] = "تم نسخ النص",
        ["just now"] = "الآن",
        ["{0} min ago"] = "قبل {0} د",
        ["{0} h ago"] = "قبل {0} س",
        ["Settings"] = "الإعدادات",
        ["The line"] = "الحبل",
        ["Look"] = "المظهر",
        ["Captures"] = "اللقطات",
        ["General"] = "عام",
        ["Show or hide the line"] = "إظهار الحبل أو إخفاؤه",
        ["Click here and press the keys you want, like Ctrl+Alt+T"] = "انقر هنا واضغط المفاتيح التي تريدها، مثل Ctrl+Alt+T",
        ["Distance from the top of the screen"] = "المسافة من أعلى الشاشة",
        ["You can also drag the line itself up or down"] = "يمكنك أيضًا سحب الحبل نفسه للأعلى أو للأسفل",
        ["Line colour"] = "لون الحبل",
        ["Clothespins"] = "المشابك",
        ["Bows at the ends of the line"] = "عُقد على طرفَي الحبل",
        ["Appearance"] = "السمة",
        ["Language"] = "اللغة",
        ["Follow Windows"] = "حسب Windows",
        ["Light"] = "فاتح",
        ["Dark"] = "داكن",
        ["Caught captures are saved to"] = "تُحفظ اللقطات الملتقطة في",
        ["Change…"] = "تغيير…",
        ["Folders watched for new screenshots"] = "المجلدات المراقَبة للقطات الجديدة",
        ["Add folder…"] = "إضافة مجلد…",
        ["Remove"] = "إزالة",
        ["Changes apply right away. Everything lives in {0}."] = "تُطبَّق التغييرات فورًا. كل شيء محفوظ في {0}.",
        ["Clothesline is on the line"] = "Clothesline جاهز",
        ["Take a screenshot and it hangs at the top of the screen. Rest the pointer against the top edge to bring the line down, or press {0}."] = "التقط لقطة شاشة وستُعلَّق أعلى الشاشة. ثبّت المؤشر عند الحافة العلوية لإنزال الحبل، أو اضغط {0}.",
        ["Click a photo to copy it. Press and hold to edit it. Double click to open it.\nDrag it along the line to reorder, into an app to send a copy, into a folder to keep it, or to the Recycle Bin to let it go.\nRest the pointer against the top edge of the screen, or press {0}, to bring the line down."] =
            "انقر على صورة لنسخها. اضغط مطوّلًا لتحريرها. انقر نقرًا مزدوجًا لفتحها.\nاسحبها على طول الحبل لإعادة الترتيب، أو إلى تطبيق لإرسال نسخة، أو إلى مجلد للاحتفاظ بها، أو إلى سلة المحذوفات للتخلص منها.\nثبّت المؤشر عند الحافة العلوية للشاشة، أو اضغط {0}، لإنزال الحبل.",
        ["Bronze"] = "برونزي", ["Gray"] = "رمادي", ["Black"] = "أسود", ["White"] = "أبيض", ["Red"] = "أحمر",
        ["Blue"] = "أزرق", ["Green"] = "أخضر", ["Gold"] = "ذهبي", ["Pink"] = "وردي", ["Purple"] = "بنفسجي",
        ["Wooden"] = "خشبي", ["Aluminium"] = "ألومنيوم", ["Coloured plastic, mixed"] = "بلاستيك ملوّن متنوّع",
        ["Red plastic"] = "بلاستيك أحمر", ["Blue plastic"] = "بلاستيك أزرق", ["Green plastic"] = "بلاستيك أخضر", ["Yellow plastic"] = "بلاستيك أصفر",
    };
}
