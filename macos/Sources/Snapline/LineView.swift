import SwiftUI

enum Layout {
    static let panelHeight: CGFloat = 240
    static let ropeTop: CGFloat = 10
    static let spacing: CGFloat = 174
    static let cardWidth: CGFloat = 150
    static let pinAbove: CGFloat = 9.5
    static let tagWidth: CGFloat = 34
    static let tagHeight: CGFloat = 26

    /// The rope hangs as a parabola from edge to edge of the screen.
    static func sag(width: CGFloat) -> CGFloat { min(58, width * 0.036) }

    static func ropeY(x: CGFloat, width: CGFloat) -> CGFloat {
        guard width > 0 else { return ropeTop }
        let f = x / width
        return ropeTop + 4 * sag(width: width) * f * (1 - f)
    }

    /// Whether a point (top-left origin) is close enough to the rope to grab it.
    static func isNearRope(_ p: CGPoint, width: CGFloat) -> Bool {
        abs(p.y - ropeY(x: p.x, width: width)) <= 7
    }

    static func x(index: Int, count: Int, width: CGFloat) -> CGFloat {
        let total = CGFloat(max(count - 1, 0)) * spacing
        return width / 2 - total / 2 + CGFloat(index) * spacing
    }

    /// Where each live photo hangs: spread evenly, or where you put it. A new
    /// photo takes the nearest free place to the middle.
    @MainActor
    static func positions(for line: Line, width: CGFloat) -> [UUID: CGFloat] {
        let live = line.items.filter { !$0.falling }
        var result: [UUID: CGFloat] = [:]
        if Settings.current.autoArrange || width <= 0 {
            for (i, item) in live.enumerated() { result[item.id] = x(index: i, count: live.count, width: width) }
            return result
        }
        let half = cardWidth / 2
        var taken: [CGFloat] = live.compactMap { $0.spot.map { CGFloat($0) * width } }
        for item in live {
            var cx: CGFloat
            if let spot = item.spot {
                cx = CGFloat(spot) * width
            } else {
                cx = width / 2
                search: for step in 0..<40 {
                    for candidate in [width / 2 + CGFloat(step) * spacing, width / 2 - CGFloat(step) * spacing] {
                        if candidate < half || candidate > width - half { continue }
                        if taken.allSatisfy({ abs($0 - candidate) >= spacing * 0.9 }) { cx = candidate; break search }
                    }
                }
                taken.append(cx)
                line.setSpot(item.id, Double(cx / width), save: true)
            }
            result[item.id] = min(max(cx, half), max(half, width - half))
        }
        return result
    }
}

struct LineView: View {
    @ObservedObject var line: Line
    /// The pointer is over the rope: the cursor says it can be dragged.
    @State private var overRope = false

    var body: some View {
        GeometryReader { geo in
            let width = geo.size.width
            let positions = Layout.positions(for: line, width: width)
            ZStack(alignment: .topLeading) {
                Rope(width: width)
                    .id(line.look)
                RopeClick(width: width)
                    .frame(width: width, height: Layout.panelHeight)

                if Settings.current.bows && width > 400 {
                    let color = Pegs.rope()
                    Bow(color: color).position(x: width * 0.055, y: Layout.ropeY(x: width * 0.055, width: width) + 1)
                    Bow(color: color, mirrored: true).position(x: width * 0.945, y: Layout.ropeY(x: width * 0.945, width: width) + 1)
                }

                if line.liveCount == 0 {
                    Hint()
                        .position(x: width / 2, y: Layout.ropeY(x: width / 2, width: width) + 34)
                        .transition(.opacity)
                }

                // A small paper tag always hangs at the end of the line: the menu, within reach.
                MenuTag(line: line)
                    .position(x: width - 30, y: Layout.ropeY(x: width - 30, width: width) - Layout.pinAbove * 0.75 + (Layout.tagHeight + 20) / 2)

                ForEach(line.items) { item in
                    let x = positions[item.id] ?? width / 2
                    let ropeY = Layout.ropeY(x: x, width: width)
                    PeggedView(item: item, line: line, width: width)
                        .frame(width: Layout.cardWidth, height: Layout.panelHeight - ropeY, alignment: .top)
                        .position(x: x, y: ropeY - Layout.pinAbove + (Layout.panelHeight - ropeY) / 2)
                }
            }
            .animation(.spring(response: 0.55, dampingFraction: 0.78), value: positions)
            .animation(.easeInOut(duration: 0.3), value: line.items.isEmpty)
            // Tucked away, the whole line waits above the top edge and slides
            // out from under the menu bar, the way an auto-hiding Dock does.
            .offset(y: line.revealed ? 0 : -(Layout.panelHeight + 12))
            .animation(line.revealed ? .spring(response: 0.42, dampingFraction: 0.82)
                                     : .easeIn(duration: 0.22), value: line.revealed)
        }
        .onPreferenceChange(HitRectsKey.self) { rects in
            line.hitRects = rects
        }
    }
}

private struct Hint: View {
    var body: some View {
        Text(L("Take a screenshot and it will hang here"))
            .font(.system(size: 12, weight: .medium, design: .rounded))
            .foregroundStyle(.secondary)
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(.regularMaterial, in: Capsule())
            .onTapGesture { Capture.snip() }
            .background(GeometryReader { g in
                Color.clear.preference(key: HitRectsKey.self, value: [Hint.id: g.frame(in: .global)])
            })
    }
    static let id = UUID()
}

/// A yellow paper tag with a plus, pegged near the end of the line. Click it for the menu.
struct MenuTag: View {
    @ObservedObject var line: Line

    var body: some View {
        VStack(spacing: -7) {
            Peg(style: Settings.current.pegStyle, seed: 1).scaleEffect(0.75, anchor: .top).frame(height: 22).zIndex(1)
            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .fill(LinearGradient(colors: [Color(red: 1, green: 0.96, blue: 0.63), Color(red: 0.95, green: 0.87, blue: 0.43)], startPoint: .top, endPoint: .bottom))
                .overlay(RoundedRectangle(cornerRadius: 3, style: .continuous).stroke(Color.black.opacity(0.25), lineWidth: 0.6))
                .overlay(Image(systemName: "plus").font(.system(size: 11, weight: .bold)).foregroundStyle(Color(red: 0.35, green: 0.28, blue: 0.12)))
                .frame(width: Layout.tagWidth, height: Layout.tagHeight)
                .rotationEffect(.degrees(-4), anchor: .top)
                .shadow(color: .black.opacity(0.25), radius: 4, y: 2)
        }
        .contentShape(Rectangle())
        .onTapGesture { NotificationCenter.default.post(name: .snaplineMenuRequested, object: nil) }
        .help(L("Menu"))
        .background(GeometryReader { g in
            Color.clear.preference(key: HitRectsKey.self, value: [MenuTag.id: g.frame(in: .global)])
        })
    }
    static let id = UUID()
}

extension Notification.Name {
    static let snaplineMenuRequested = Notification.Name("snaplineMenuRequested")
}

/// The rope itself takes a right-click for the menu, and nothing else.
struct RopeClick: NSViewRepresentable {
    let width: CGFloat

    func makeNSView(context: Context) -> RopeClickView { RopeClickView() }
    func updateNSView(_ view: RopeClickView, context: Context) { view.width = width }
}

final class RopeClickView: NSView {
    var width: CGFloat = 0

    override var isFlipped: Bool { true }

    override func hitTest(_ point: NSPoint) -> NSView? {
        let local = convert(point, from: superview)
        return Layout.isNearRope(local, width: width) ? self : nil
    }

    override func rightMouseDown(with event: NSEvent) {
        NotificationCenter.default.post(name: .snaplineMenuRequested, object: nil)
    }

    override func mouseDown(with event: NSEvent) {}
}

/// A thin cord in the colour you chose, with a faint highlight and a soft
/// shadow, fading at both ends so it seems to come from beyond the screen.
struct Rope: View {
    let width: CGFloat

    private var path: Path {
        Path { p in
            let top = Layout.ropeTop
            p.move(to: CGPoint(x: -20, y: top))
            p.addQuadCurve(
                to: CGPoint(x: width + 20, y: top),
                control: CGPoint(x: width / 2, y: top + 2 * Layout.sag(width: width)))
        }
    }

    var body: some View {
        let color = Pegs.rope()
        ZStack {
            path.stroke(Color.black.opacity(0.22), lineWidth: 1.4).offset(y: 1.2).blur(radius: 1.2)
            path.stroke(color, lineWidth: 1.6)
            path.stroke(color.lighter(0.55).opacity(0.7), lineWidth: 0.5).offset(y: -0.45)
        }
        .mask(
            LinearGradient(stops: [
                .init(color: .clear, location: 0),
                .init(color: .black, location: 0.08),
                .init(color: .black, location: 0.92),
                .init(color: .clear, location: 1),
            ], startPoint: .leading, endPoint: .trailing)
        )
        .allowsHitTesting(false)
    }
}

struct HitRectsKey: PreferenceKey {
    static let defaultValue: [UUID: CGRect] = [:]
    static func reduce(value: inout [UUID: CGRect], nextValue: () -> [UUID: CGRect]) {
        value.merge(nextValue()) { $1 }
    }
}

/// Starts a new screenshot, the way Cmd+Shift+5 would.
enum Capture {
    static func snip() {
        let task = Process()
        task.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
        task.arguments = ["-i", "-U"]
        try? task.run()
    }
}
