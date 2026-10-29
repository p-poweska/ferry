import AppKit
import SwiftUI

/// The settings window, opened from the menu bar icon.
@MainActor
final class SettingsWindowController {
    private let preferences: Preferences
    private var window: NSWindow?

    init(preferences: Preferences) {
        self.preferences = preferences
    }

    func show() {
        let window = self.window ?? makeWindow()
        if !window.isVisible {
            // Fresh content every time it opens, so it picks up changes made
            // elsewhere, like the login item in System Settings.
            let controller = NSHostingController(rootView: SettingsView(preferences: preferences))
            window.contentViewController = controller
            window.setContentSize(controller.view.fittingSize)
            if self.window == nil {
                window.center()
            }
        }
        self.window = window
        NSApp.activate()
        window.makeKeyAndOrderFront(nil)
    }

    private func makeWindow() -> NSWindow {
        let window = SettingsWindow(
            contentRect: .zero,
            styleMask: [.titled, .closable],
            backing: .buffered,
            defer: true
        )
        window.title = "Ferry Settings"
        window.isReleasedWhenClosed = false
        window.collectionBehavior = [.moveToActiveSpace]
        return window
    }
}

private final class SettingsWindow: NSWindow {
    /// A menu bar app has no main menu to close windows with ⌘W.
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        let modifiers = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        if modifiers == .command, event.charactersIgnoringModifiers == "w" {
            performClose(nil)
            return true
        }
        return super.performKeyEquivalent(with: event)
    }
}
