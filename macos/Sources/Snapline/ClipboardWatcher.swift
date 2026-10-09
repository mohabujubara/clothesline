import AppKit

/// Catches captures that only reach the clipboard: Cmd+Ctrl+Shift+4 and
/// Cmd+Ctrl+Shift+3 copy the screenshot instead of saving it. A pure image
/// on the clipboard, with no text or files beside it, is written to the
/// inbox folder and hung. Anything we put on the clipboard ourselves carries
/// a file beside the image, so it is never caught.
@MainActor
final class ClipboardWatcher {
    private var timer: Timer?
    private var lastCount = NSPasteboard.general.changeCount
    private let onCapture: (URL, CGSize) -> Void
    private let recentlyHungFromFile: (CGSize) -> Bool
    var enabled = true

    init(onCapture: @escaping (URL, CGSize) -> Void, recentlyHungFromFile: @escaping (CGSize) -> Bool) {
        self.onCapture = onCapture
        self.recentlyHungFromFile = recentlyHungFromFile
    }

    func start() {
        guard timer == nil else { return }
        let t = Timer(timeInterval: 0.8, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    private func tick() {
        let pb = NSPasteboard.general
        let count = pb.changeCount
        guard count != lastCount else { return }
        lastCount = count
        guard enabled else { return }
        // Let the file watcher hang a saved capture first; only catch what never reached a file.
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) { [weak self] in
            guard let self, NSPasteboard.general.changeCount == count else { return }
            self.tryCatch(pb)
        }
    }

    private static let notPureImage: [NSPasteboard.PasteboardType] = [
        .string, .rtf, .html, .fileURL, .URL, NSPasteboard.PasteboardType("public.file-url"),
    ]

    private func tryCatch(_ pb: NSPasteboard) {
        guard let types = pb.types, types.contains(where: { $0 == .png || $0 == .tiff }) else { return }
        if types.contains(where: Self.notPureImage.contains) { return }
        guard let data = pb.data(forType: .png) ?? pb.data(forType: .tiff),
              let image = NSBitmapImageRep(data: data),
              image.pixelsWide >= 8, image.pixelsHigh >= 8 else { return }
        let size = CGSize(width: image.pixelsWide, height: image.pixelsHigh)
        if recentlyHungFromFile(size) { return }
        guard let png = image.representation(using: .png, properties: [:]) else { return }
        let url = Inbox.newCapturePath()
        do {
            try FileManager.default.createDirectory(at: Inbox.folder, withIntermediateDirectories: true)
            try png.write(to: url, options: .atomic)
            log.notice("Caught a clipboard capture: \(url.lastPathComponent, privacy: .public)")
            onCapture(url, size)
        } catch {
            log.error("Clipboard capture failed: \(error.localizedDescription, privacy: .public)")
        }
    }
}

extension Inbox {
    /// A fresh file name for a capture, like the system's.
    static func newCapturePath() -> URL {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd 'at' HH.mm.ss"
        let stamp = formatter.string(from: Date())
        var url = folder.appendingPathComponent("Screenshot \(stamp).png")
        var n = 2
        while FileManager.default.fileExists(atPath: url.path) {
            url = folder.appendingPathComponent("Screenshot \(stamp) (\(n)).png")
            n += 1
        }
        return url
    }
}
