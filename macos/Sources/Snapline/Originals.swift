import CryptoKit
import Foundation

/// The untouched copy of a screenshot that is being marked up, so "Revert to
/// original" always has something to go back to. One copy per file, named
/// after the file's full path, so two files with the same name never share
/// one. The copy lives only while the photo hangs on the line: taking it
/// down, trashing it or discarding it removes the copy too, so no unblurred
/// version of anything outlives the picture it came from.
enum Originals {
    static let folder = Settings.folder.appendingPathComponent("Originals", isDirectory: true)

    static func backup(for url: URL) -> URL {
        let path = url.standardizedFileURL.path
        let digest = SHA256.hash(data: Data(path.utf8)).prefix(8).map { String(format: "%02x", $0) }.joined()
        return folder.appendingPathComponent("\(digest)-\(url.lastPathComponent)")
    }

    static func exists(for url: URL) -> Bool { FileManager.default.fileExists(atPath: backup(for: url).path) }

    /// Keeps a copy of the file as it is now, unless one is already kept.
    static func keep(_ url: URL) throws {
        let target = backup(for: url)
        guard !FileManager.default.fileExists(atPath: target.path) else { return }
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try FileManager.default.copyItem(at: url, to: target)
    }

    /// Puts the kept copy back in place of the file. The copy stays until the file leaves the line.
    static func revert(_ url: URL) throws {
        let source = backup(for: url)
        guard FileManager.default.fileExists(atPath: source.path) else { return }
        // Copy first, replace second: the file is never gone, even if something fails halfway.
        let staging = folder.appendingPathComponent("revert-\(UUID().uuidString)")
        try FileManager.default.copyItem(at: source, to: staging)
        _ = try FileManager.default.replaceItemAt(url, withItemAt: staging)
    }

    static func forget(_ url: URL) {
        try? FileManager.default.removeItem(at: backup(for: url))
    }

    /// At launch: only copies of photos still on the line are kept. Anything else goes.
    static func sweep(hanging: [URL]) {
        guard !Settings.readOnly, let files = try? FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil) else { return }
        let wanted = Set(hanging.map { backup(for: $0).standardizedFileURL.path })
        for file in files where !wanted.contains(file.standardizedFileURL.path) {
            try? FileManager.default.removeItem(at: file)
        }
    }
}
