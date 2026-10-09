import SwiftUI

/// The clothespins and the colour of the line. A wooden peg with its spring
/// by default; aluminium or coloured plastic if you prefer. Everything is
/// drawn in code.
enum Pegs {
    static let styles = ["wood", "metal", "mixed", "red", "blue", "green", "yellow"]
    static let ropeColors = ["bronze", "gray", "black", "white", "red", "blue", "green", "gold", "pink", "purple"]

    /// The colour of the line, as chosen in Settings.
    static func rope() -> Color {
        let key = Settings.current.ropeColor.trimmingCharacters(in: .whitespaces).lowercased()
        if key.hasPrefix("#"), let c = Color(hex: key) { return c }
        return ropeColor(key)
    }

    static func ropeColor(_ key: String) -> Color {
        switch key {
        case "gray": return Color(white: 0.55)
        case "black": return Color(white: 0.12)
        case "white": return Color(white: 0.96)
        case "red": return Color(red: 0.72, green: 0.23, blue: 0.23)
        case "blue": return Color(red: 0.23, green: 0.43, blue: 0.72)
        case "green": return Color(red: 0.24, green: 0.56, blue: 0.31)
        case "gold": return Color(red: 0.79, green: 0.64, blue: 0.24)
        case "pink": return Color(red: 0.85, green: 0.43, blue: 0.62)
        case "purple": return Color(red: 0.49, green: 0.35, blue: 0.72)
        default: return Color(red: 0.48, green: 0.31, blue: 0.16)   // bronze: a twisted brown cord
        }
    }

    static let plastic: [Color] = [
        Color(red: 0.89, green: 0.29, blue: 0.29), Color(red: 0.24, green: 0.55, blue: 0.89), Color(red: 0.30, green: 0.72, blue: 0.36),
        Color(red: 0.95, green: 0.76, blue: 0.19), Color(red: 0.94, green: 0.49, blue: 0.23), Color(red: 0.60, green: 0.36, blue: 0.85),
    ]

    static func plasticColor(_ key: String, seed: Int) -> Color {
        switch key {
        case "red": return plastic[0]
        case "blue": return plastic[1]
        case "green": return plastic[2]
        case "yellow": return plastic[3]
        default: return plastic[abs(seed) % plastic.count]
        }
    }
}

extension Color {
    init?(hex: String) {
        var s = hex
        if s.hasPrefix("#") { s.removeFirst() }
        guard s.count == 6, let v = UInt32(s, radix: 16) else { return nil }
        self.init(red: Double((v >> 16) & 0xFF) / 255, green: Double((v >> 8) & 0xFF) / 255, blue: Double(v & 0xFF) / 255)
    }
}

/// The outline of a spring peg seen from the front, in a 12 x 30 box: a
/// rounded head, a waist at the spring, two legs with a gap between them.
struct PegBody: Shape {
    func path(in rect: CGRect) -> Path {
        let sx = rect.width / 12, sy = rect.height / 30
        func p(_ x: CGFloat, _ y: CGFloat) -> CGPoint { CGPoint(x: rect.minX + x * sx, y: rect.minY + y * sy) }
        var path = Path()
        path.move(to: p(3, 0))
        path.addLine(to: p(9, 0))
        path.addQuadCurve(to: p(11.5, 2.5), control: p(11.5, 0))
        path.addLine(to: p(11.5, 9.5))
        path.addQuadCurve(to: p(10.5, 12), control: p(11.5, 11.5))
        path.addLine(to: p(11.5, 13))
        path.addLine(to: p(11.5, 30))
        path.addLine(to: p(7.2, 30))
        path.addLine(to: p(6.6, 21.5))
        path.addLine(to: p(5.4, 21.5))
        path.addLine(to: p(4.8, 30))
        path.addLine(to: p(0.5, 30))
        path.addLine(to: p(0.5, 13))
        path.addLine(to: p(1.5, 12))
        path.addQuadCurve(to: p(0.5, 9.5), control: p(0.5, 11.5))
        path.addLine(to: p(0.5, 2.5))
        path.addQuadCurve(to: p(3, 0), control: p(0.5, 0))
        path.closeSubpath()
        return path
    }
}

/// A peg: wooden, aluminium or coloured plastic. `seed` picks the colour for
/// the mixed style. Gold spring when the photo is kept on purpose.
struct Peg: View {
    var style: String = Settings.current.pegStyle
    var seed: Int = 0
    var pinned = false

    var body: some View {
        switch style {
        case "metal": Clothespin(pinned: pinned)
        case "wood": SpringPeg(fill: wood, outline: Color(red: 0.35, green: 0.24, blue: 0.12).opacity(0.33), grain: true, pinned: pinned)
        default:
            let c = Pegs.plasticColor(style, seed: seed)
            SpringPeg(fill: LinearGradient(colors: [c.opacity(0.85), c.lighter(0.25), c, c.darker(0.28)], startPoint: .leading, endPoint: .trailing),
                      outline: Color.black.opacity(0.3), grain: false, pinned: pinned)
        }
    }

    private var wood: LinearGradient {
        LinearGradient(stops: [
            .init(color: Color(red: 0.78, green: 0.65, blue: 0.45), location: 0),
            .init(color: Color(red: 0.91, green: 0.82, blue: 0.64), location: 0.3),
            .init(color: Color(red: 0.86, green: 0.75, blue: 0.55), location: 0.52),
            .init(color: Color(red: 0.90, green: 0.80, blue: 0.61), location: 0.6),
            .init(color: Color(red: 0.72, green: 0.58, blue: 0.38), location: 1),
        ], startPoint: .leading, endPoint: .trailing)
    }
}

struct SpringPeg<Fill: ShapeStyle>: View {
    let fill: Fill
    let outline: Color
    let grain: Bool
    let pinned: Bool

    var body: some View {
        ZStack(alignment: .top) {
            PegBody()
                .fill(fill)
                .overlay(PegBody().stroke(outline, lineWidth: 0.6))
                .shadow(color: .black.opacity(0.30), radius: 2, y: 1.5)
            if grain {
                Path { p in
                    p.move(to: CGPoint(x: 3.2, y: 2)); p.addLine(to: CGPoint(x: 3.2, y: 10))
                    p.move(to: CGPoint(x: 8.8, y: 2)); p.addLine(to: CGPoint(x: 8.8, y: 10))
                    p.move(to: CGPoint(x: 3.6, y: 15)); p.addLine(to: CGPoint(x: 3.6, y: 27))
                    p.move(to: CGPoint(x: 8.4, y: 15)); p.addLine(to: CGPoint(x: 8.4, y: 27))
                }
                .stroke(Color(red: 0.42, green: 0.28, blue: 0.13).opacity(0.19), lineWidth: 0.5)
            }
            // The split between the halves.
            Path { p in p.move(to: CGPoint(x: 6, y: 0.4)); p.addLine(to: CGPoint(x: 6, y: 11.5)) }
                .stroke(Color.black.opacity(grain ? 0.38 : 0.33), lineWidth: 0.7)
            // The wire spring around the waist, where the line passes.
            RoundedRectangle(cornerRadius: 1.8, style: .continuous)
                .fill(LinearGradient(colors: pinned ? [Color(red: 0.95, green: 0.84, blue: 0.50), Color(red: 0.72, green: 0.54, blue: 0.18)]
                                                   : [Color(white: 0.95), Color(white: 0.55)], startPoint: .top, endPoint: .bottom))
                .overlay(RoundedRectangle(cornerRadius: 1.8, style: .continuous).stroke(Color.black.opacity(0.35), lineWidth: 0.4))
                .frame(width: 11, height: 3.6)
                .offset(y: 8.2)
            Path { p in
                p.move(to: CGPoint(x: 2.2, y: 12))
                p.addCurve(to: CGPoint(x: 6, y: 16.2), control1: CGPoint(x: 1.6, y: 15), control2: CGPoint(x: 3.6, y: 17.2))
                p.addCurve(to: CGPoint(x: 9.8, y: 12), control1: CGPoint(x: 8.4, y: 17.2), control2: CGPoint(x: 10.4, y: 15))
            }
            .stroke(pinned ? Color(red: 0.72, green: 0.54, blue: 0.18) : Color(white: 0.5), lineWidth: 0.9)
        }
        .frame(width: 12, height: 30)
        .allowsHitTesting(false)
    }
}

extension Color {
    func lighter(_ k: Double) -> Color { Color(nsColor: NSColor(self).blended(withFraction: k, of: .white) ?? NSColor(self)) }
    func darker(_ k: Double) -> Color { Color(nsColor: NSColor(self).blended(withFraction: k, of: .black) ?? NSColor(self)) }
}

/// A small bow tied in the line, the way a cord is tied to a hook: two loops, two tails, a knot.
struct Bow: View {
    let color: Color
    var mirrored = false

    var body: some View {
        Path { p in
            p.move(to: .zero)
            p.addCurve(to: CGPoint(x: -12.5, y: -2.5), control1: CGPoint(x: -5, y: -9), control2: CGPoint(x: -14, y: -8))
            p.addCurve(to: .zero, control1: CGPoint(x: -11.5, y: 1.5), control2: CGPoint(x: -4, y: 2))
            p.move(to: .zero)
            p.addCurve(to: CGPoint(x: 12.5, y: -2.5), control1: CGPoint(x: 5, y: -9), control2: CGPoint(x: 14, y: -8))
            p.addCurve(to: .zero, control1: CGPoint(x: 11.5, y: 1.5), control2: CGPoint(x: 4, y: 2))
            p.move(to: .zero)
            p.addCurve(to: CGPoint(x: -6.5, y: 12), control1: CGPoint(x: -1.5, y: 5), control2: CGPoint(x: -3.5, y: 8.5))
            p.move(to: .zero)
            p.addCurve(to: CGPoint(x: 6.5, y: 12), control1: CGPoint(x: 1.5, y: 5), control2: CGPoint(x: 3.5, y: 8.5))
        }
        .stroke(color, style: StrokeStyle(lineWidth: 1.7, lineCap: .round, lineJoin: .round))
        .frame(width: 30, height: 30)
        .rotationEffect(.degrees(mirrored ? 8 : -8))
        .shadow(color: .black.opacity(0.22), radius: 1.5, y: 1)
        .allowsHitTesting(false)
    }
}

/// The original aluminium clip, kept as a style.
struct Clothespin: View {
    var pinned = false

    private var metal: LinearGradient {
        pinned
            ? LinearGradient(stops: [
                .init(color: Color(red: 0.72, green: 0.56, blue: 0.22), location: 0),
                .init(color: Color(red: 0.98, green: 0.88, blue: 0.55), location: 0.35),
                .init(color: Color(red: 0.88, green: 0.74, blue: 0.38), location: 0.65),
                .init(color: Color(red: 0.62, green: 0.46, blue: 0.16), location: 1),
            ], startPoint: .leading, endPoint: .trailing)
            : LinearGradient(stops: [
                .init(color: Color(white: 0.70), location: 0),
                .init(color: Color(white: 0.93), location: 0.35),
                .init(color: Color(white: 0.82), location: 0.65),
                .init(color: Color(white: 0.62), location: 1),
            ], startPoint: .leading, endPoint: .trailing)
    }

    var body: some View {
        RoundedRectangle(cornerRadius: 3.5, style: .continuous)
            .fill(metal)
            .frame(width: 9, height: 26)
            .overlay(
                RoundedRectangle(cornerRadius: 3.5, style: .continuous)
                    .stroke(LinearGradient(colors: [Color.white.opacity(0.9), Color.black.opacity(0.18)], startPoint: .top, endPoint: .bottom), lineWidth: 0.6)
            )
            .overlay(alignment: .top) {
                Capsule().fill(Color.black.opacity(0.32)).frame(width: 5, height: 1.4).padding(.top, 8.5)
            }
            .shadow(color: .black.opacity(0.30), radius: 2, y: 1.5)
            .allowsHitTesting(false)
    }
}
