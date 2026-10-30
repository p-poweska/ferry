import AppKit
import SwiftUI

/// Shows, hides and positions the shelf panel.
@MainActor
final class ShelfPanelController: NSObject, NSWindowDelegate {
    private let shelf: Shelf
    private let preferences: Preferences
    private let panel = ShelfPanel(size: ShelfView.size)
    /// Popped up by a file drag elsewhere rather than opened by the user.
    private var isShownForDrag = false
    private var hideAfterDrag: Task<Void, Never>?

    private(set) var isVisible = false

    init(shelf: Shelf, preferences: Preferences) {
        self.shelf = shelf
        self.preferences = preferences
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
        let slide = preferences.position.slideOffset
        panel.wasMovedByUser = false
        panel.setFrame(frame.offsetBy(dx: slide.dx, dy: slide.dy), display: false)
        panel.alphaValue = 0
        panel.orderFrontRegardless()
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.24
            context.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel.animator().setFrame(frame, display: true)
            panel.animator().alphaValue = 1
        }
    }

    func hide() {
        guard isVisible else { return }
        isVisible = false
        isShownForDrag = false
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

    func fileDragBegan() {
        // The next drag started before the last one's shelf went away.
        hideAfterDrag?.cancel()
        guard !isVisible else { return }
        isShownForDrag = true
        show()
    }

    func fileDragEnded() {
        guard isShownForDrag else { return }
        // The drop reaches the shelf a moment after the drag source sees the
        // mouse go up, so wait before deciding the shelf wasn't used.
        hideAfterDrag = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(400))
            guard let self, !Task.isCancelled, self.isShownForDrag else { return }
            self.isShownForDrag = false
            self.hideIfUnused()
        }
    }

    func shelfDidChange() {
        hideIfUnused()
    }

    private func hideIfUnused() {
        if shelf.isEmpty && !shelf.isReceiving {
            hide()
        }
    }

    private func closeButtonClicked() {
        shelf.removeAll()
        hide()
    }

    // MARK: - Positioning

    /// Moves a visible shelf to its new spot as soon as the setting changes.
    func positionDidChange() {
        // Dragging the shelf around picks "Your Spot", and it's already there.
        guard isVisible, !(preferences.position == .custom && panel.wasMovedByUser) else { return }
        panel.wasMovedByUser = false
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.3
            context.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            panel.animator().setFrame(targetFrame(), display: true)
        }
    }

    private func targetFrame() -> NSRect {
        preferences.position.frame(
            size: panel.frame.size,
            mouse: NSEvent.mouseLocation,
            customOrigin: preferences.customOrigin,
            screens: NSScreen.screens.map(ShelfPosition.Screen.init)
        )
    }

    func windowDidMove(_ notification: Notification) {
        guard panel.wasMovedByUser else { return }
        preferences.customOrigin = panel.frame.origin
        preferences.position = .custom
    }
}
