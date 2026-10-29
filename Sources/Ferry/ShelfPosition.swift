import AppKit

/// Where the shelf slides in, on the screen with the pointer.
enum ShelfPosition: String, CaseIterable, Identifiable {
    case rightEdge
    case leftEdge
    case topEdge
    case bottomEdge
    case pointer
    /// Wherever the user last dragged the shelf to.
    case custom

    static let margin: CGFloat = 16
    /// Room between the pointer and the shelf, so a drag that's just passing
    /// by doesn't land on it.
    static let pointerGap: CGFloat = 40

    var id: Self { self }

    var title: String {
        switch self {
        case .rightEdge: "Right"
        case .leftEdge: "Left"
        case .topEdge: "Top"
        case .bottomEdge: "Bottom"
        case .pointer: "Pointer"
        case .custom: "Your Spot"
        }
    }

    /// The direction the shelf slides in from.
    var slideOffset: CGVector {
        switch self {
        case .rightEdge: CGVector(dx: 18, dy: 0)
        case .leftEdge: CGVector(dx: -18, dy: 0)
        case .topEdge: CGVector(dx: 0, dy: 18)
        case .bottomEdge: CGVector(dx: 0, dy: -18)
        case .pointer, .custom: CGVector(dx: 0, dy: -10)
        }
    }

    struct Screen {
        var frame: NSRect
        /// The frame minus the menu bar and the Dock.
        var visibleFrame: NSRect
    }

    /// The shelf's frame on the screen under `mouse`. A custom spot is used as
    /// long as it still fits on one of the screens; otherwise the shelf goes
    /// to the right edge.
    func frame(size: NSSize, mouse: NSPoint, customOrigin: NSPoint?, screens: [Screen]) -> NSRect {
        if self == .custom, let customOrigin {
            let frame = NSRect(origin: customOrigin, size: size)
            if screens.contains(where: { $0.visibleFrame.contains(frame) }) {
                return frame
            }
        }

        let area = (screens.first { $0.frame.contains(mouse) } ?? screens.first)?.visibleFrame ?? .zero
        let margin = Self.margin
        var origin: NSPoint
        switch self {
        case .rightEdge, .custom:
            origin = NSPoint(x: area.maxX - size.width - margin, y: area.midY - size.height / 2)
        case .leftEdge:
            origin = NSPoint(x: area.minX + margin, y: area.midY - size.height / 2)
        case .topEdge:
            origin = NSPoint(x: area.midX - size.width / 2, y: area.maxY - size.height - margin)
        case .bottomEdge:
            origin = NSPoint(x: area.midX - size.width / 2, y: area.minY + margin)
        case .pointer:
            // To the right of the pointer, or to its left near the right edge.
            origin = NSPoint(x: mouse.x + Self.pointerGap, y: mouse.y - size.height / 2)
            if origin.x + size.width > area.maxX - margin {
                origin.x = mouse.x - Self.pointerGap - size.width
            }
            origin.x = origin.x.clamped(to: area.minX + margin, area.maxX - size.width - margin)
            origin.y = origin.y.clamped(to: area.minY + margin, area.maxY - size.height - margin)
        }
        return NSRect(origin: origin, size: size)
    }
}

extension ShelfPosition.Screen {
    init(_ screen: NSScreen) {
        self.init(frame: screen.frame, visibleFrame: screen.visibleFrame)
    }
}

private extension CGFloat {
    func clamped(to lower: CGFloat, _ upper: CGFloat) -> CGFloat {
        Swift.min(Swift.max(self, lower), upper)
    }
}
