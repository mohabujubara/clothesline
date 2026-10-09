import Foundation

/// Looks up text in `Resources/<language>.lproj/Localizable.strings`, keyed by
/// the English text itself. The language follows the system, or the one
/// chosen in Settings, and anything missing falls back to English.
///
/// The folders are copied into the app by `scripts/build-app.sh`. Running the
/// bare binary, outside the app, shows English.
func L(_ english: String) -> String {
    Localization.text(english)
}

enum Localization {
    /// "en" or "ar".
    nonisolated(unsafe) static var language: String = resolve()
    static var isRTL: Bool { language == "ar" }

    nonisolated(unsafe) private static var table: [String: String]? = nil

    static func refresh() {
        language = resolve()
        table = nil
    }

    private static func resolve() -> String {
        let chosen = Settings.current.language
        if chosen == "en" || chosen == "ar" { return chosen }
        for preferred in Locale.preferredLanguages {
            if preferred.hasPrefix("ar") { return "ar" }
            if preferred.hasPrefix("en") { return "en" }
        }
        return "en"
    }

    static func text(_ english: String) -> String {
        if language == "en" { return english }
        if table == nil { table = load(language) }
        return table?[english] ?? english
    }

    private static func load(_ lang: String) -> [String: String] {
        guard let path = Bundle.main.path(forResource: "Localizable", ofType: "strings", inDirectory: "\(lang).lproj"),
              let dict = NSDictionary(contentsOfFile: path) as? [String: String] else { return [:] }
        return dict
    }
}
