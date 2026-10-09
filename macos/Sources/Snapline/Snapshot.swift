import AppKit
import SwiftUI

/// Renders the line to a PNG without a desktop session to look at:
/// `Snapline --snapshot out.png image1 image2 …`. Used by CI, so the drawing
/// can be checked from anywhere.
@MainActor
enum Snapshot {
    static func run(output: String, images: [String]) -> Int32 {
        let app = NSApplication.shared
        app.setActivationPolicy(.prohibited)

        // A render never touches the settings file.
        Settings.readOnly = true
        let line = Line(persist: false)
        for path in images {
            _ = line.hang(URL(fileURLWithPath: path), quietly: true)
        }

        let width: CGFloat = 1400
        let height = Layout.panelHeight + 40
        let content = ZStack(alignment: .top) {
            LinearGradient(colors: [Color(red: 0.86, green: 0.89, blue: 1.0), Color(red: 0.93, green: 0.88, blue: 0.96), Color(red: 0.99, green: 0.87, blue: 0.89)],
                           startPoint: .topLeading, endPoint: .bottomTrailing)
            LineView(line: line)
                .frame(width: width, height: Layout.panelHeight)
        }
        .frame(width: width, height: height)

        let host = NSHostingView(rootView: content)
        host.frame = NSRect(x: 0, y: 0, width: width, height: height)
        let window = NSWindow(contentRect: host.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        window.isOpaque = false
        window.backgroundColor = .clear
        window.contentView = host
        window.setFrameOrigin(NSPoint(x: -20000, y: -20000))
        window.orderFrontRegardless()

        line.revealed = true
        pump(seconds: 1.8)

        guard let rep = host.bitmapImageRepForCachingDisplay(in: host.bounds) else {
            FileHandle.standardError.write("Could not create a bitmap\n".data(using: .utf8)!)
            return 1
        }
        host.cacheDisplay(in: host.bounds, to: rep)
        guard let data = rep.representation(using: .png, properties: [:]) else { return 1 }
        do {
            try data.write(to: URL(fileURLWithPath: output))
        } catch {
            FileHandle.standardError.write("Could not write \(output): \(error)\n".data(using: .utf8)!)
            return 1
        }
        print("Wrote \(output)")
        return 0
    }

    /// Lets animations and layout run for a while.
    static func pump(seconds: TimeInterval) {
        let until = Date().addingTimeInterval(seconds)
        while Date() < until {
            RunLoop.main.run(mode: .default, before: Date().addingTimeInterval(0.02))
        }
    }
}
