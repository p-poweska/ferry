import AppKit
import SwiftUI

/// Shows, hides and positions the shelf panel.
@MainActor
final class ShelfPanelController: NSObject, NSWindowDelegate {
    private static let originKey = "ShelfOrigin"
    private static let screenMargin: CGFloat = 16

    private let shelf: Shelf
    private let panel = ShelfPanel(size: ShelfView.size)
    private var isAnimating = false

    private(set) var isVisible = false

    /// Where the user last dragged the shelf to, if anywhere.
    private var customOrigin: NSPoint? {
        get { UserDefaults.standard.string(forKey: Self.originKey).map(NSPointFromString) }
        set { UserDefaults.standard.set(newValue.map(NSStringFromPoint), forKey: Self.originKey) }
    }

    init(shelf: Shelf) {
        self.shelf = shelf
        super.init()
        let root = ShelfView(shelf: shelf) { [weak self] in
            self?.closeButtonClicked()
        }
        panel.setContent(NSHostingView(rootView: root))
        panel.delegate = self
    }

    func toggle() {
        isVisible ? hide() : show()
    }

    func show() {
        shelf.pruneMissing()
        guard !isVisible else { return }
        isVisible = true

        let frame = targetFrame()
        isAnimating = true
        panel.setFrame(frame.offsetBy(dx: 18, dy: 0), display: false)
        panel.alphaValue = 0
        panel.orderFrontRegardless()
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.24
            context.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel.animator().setFrame(frame, display: true)
            panel.animator().alphaValue = 1
        } completionHandler: {
            MainActor.assumeIsolated {
                self.isAnimating = false
            }
        }
    }

    func hide() {
        guard isVisible else { return }
        isVisible = false
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.18
            context.timingFunction = CAMediaTimingFunction(name: .easeIn)
            panel.animator().alphaValue = 0
        } completionHandler: {
            MainActor.assumeIsolated {
                // Shown again while fading out: keep it on screen.
                guard !self.isVisible else { return }
                self.panel.orderOut(nil)
            }
        }
    }

    func shelfDidChange() {
        if shelf.isEmpty {
            hide()
        }
    }

    private func closeButtonClicked() {
        shelf.removeAll()
        hide()
    }

    // MARK: - Positioning

    /// The user's spot if it still fits on a screen, otherwise the right edge
    /// of the screen under the mouse.
    private func targetFrame() -> NSRect {
        let size = panel.frame.size
        if let origin = customOrigin {
            let frame = NSRect(origin: origin, size: size)
            if NSScreen.screens.contains(where: { $0.visibleFrame.contains(frame) }) {
                return frame
            }
        }
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { $0.frame.contains(mouse) } ?? NSScreen.main
        let visible = screen?.visibleFrame ?? .zero
        return NSRect(
            x: visible.maxX - size.width - Self.screenMargin,
            y: visible.midY - size.height / 2,
            width: size.width,
            height: size.height
        )
    }

    func windowDidMove(_ notification: Notification) {
        guard isVisible, !isAnimating else { return }
        customOrigin = panel.frame.origin
    }
}
