import Foundation
import Vision

/// Reads the text in a screenshot with the OCR built into macOS. No network,
/// no service: the same engine Live Text uses.
enum Ocr {
    static func read(_ url: URL) async -> String? {
        await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                let request = VNRecognizeTextRequest()
                request.recognitionLevel = .accurate
                request.usesLanguageCorrection = true
                if #available(macOS 13.0, *) { request.automaticallyDetectsLanguage = true }
                // Arabic and English first, then whatever the user's Mac speaks;
                // only languages this version of Vision knows, or the request fails outright.
                let supported = (try? request.supportedRecognitionLanguages()) ?? []
                var wanted = ["ar", "en-US"] + Locale.preferredLanguages
                wanted = wanted.filter { lang in supported.contains { $0 == lang || $0.hasPrefix(lang.prefix(2)) } }
                var chosen: [String] = []
                for lang in wanted {
                    let match = supported.first { $0 == lang } ?? supported.first { $0.hasPrefix(lang.prefix(2)) }
                    if let match, !chosen.contains(match) { chosen.append(match) }
                }
                if !chosen.isEmpty { request.recognitionLanguages = chosen }
                let handler = VNImageRequestHandler(url: url, options: [:])
                do {
                    try handler.perform([request])
                    let lines = (request.results ?? []).compactMap { $0.topCandidates(1).first?.string.trimmingCharacters(in: .whitespaces) }
                        .filter { !$0.isEmpty }
                    continuation.resume(returning: lines.joined(separator: "\n"))
                } catch {
                    log.error("OCR failed: \(error.localizedDescription, privacy: .public)")
                    continuation.resume(returning: nil)
                }
            }
        }
    }
}
