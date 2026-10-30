import AppKit

/// The menu bar icon: shows that Ferry is running and how many files are
/// waiting on the shelf.
@MainActor
final class StatusItemController: NSObject, NSMenuDelegate {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let shelf: Shelf
    private let panel: ShelfPanelController
    private let preferences: Preferences
    private let openSettings: () -> Void

    init(shelf: Shelf, panel: ShelfPanelController, preferences: Preferences, openSettings: @escaping () -> Void) {
        self.shelf = shelf
        self.panel = panel
        self.preferences = preferences
        self.openSettings = openSettings
        super.init()

        let menu = NSMenu()
        menu.delegate = self
        statusItem.menu = menu

        if let button = statusItem.button {
            button.imagePosition = .imageLeading
            button.font = .monospacedDigitSystemFont(ofSize: NSFont.systemFontSize, weight: .medium)
        }
        refresh()
    }

    func refresh() {
        guard let button = statusItem.button else { return }
        let count = shelf.items.count
        let image = NSImage(
            systemSymbolName: count > 0 ? "ferry.fill" : "ferry",
            accessibilityDescription: "Ferry"
        )
        image?.isTemplate = true
        button.image = image
        button.title = count > 0 ? "\(count)" : ""
        button.toolTip = count > 0 ? "Ferry — \(Self.itemCount(count)) on the shelf" : "Ferry"
    }

    // MARK: - NSMenuDelegate

    func menuNeedsUpdate(_ menu: NSMenu) {
        shelf.pruneMissing()
        menu.removeAllItems()

        let count = shelf.items.count
        menu.addItem(.sectionHeader(title: count > 0 ? "\(Self.itemCount(count)) on the shelf" : "Shelf is empty"))
        let toggle = MenuItem(panel.isVisible ? "Hide Shelf" : "Show Shelf", symbol: "tray") { [panel] in
            panel.toggle()
        }
        if let shortcut = preferences.shortcut {
            toggle.keyEquivalent = shortcut.keyEquivalent
            toggle.keyEquivalentModifierMask = shortcut.modifiers
        }
        menu.addItem(toggle)
        menu.addItem(MenuItem("Clear Shelf", symbol: "xmark.circle", isEnabled: count > 0) { [shelf] in
            shelf.removeAll()
        })

        menu.addItem(.separator())
        menu.addItem(MenuItem("Settings…", key: ",", handler: openSettings))
        menu.addItem(MenuItem("About Ferry") {
            NSApp.activate()
            NSApp.orderFrontStandardAboutPanel(nil)
        })
        menu.addItem(MenuItem("Quit Ferry", key: "q") {
            NSApp.terminate(nil)
        })
    }

    static func itemCount(_ count: Int) -> String {
        count == 1 ? "1 item" : "\(count) items"
    }
}
