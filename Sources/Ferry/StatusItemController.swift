import AppKit

/// The menu bar icon: shows that Ferry is running and how many files are
/// waiting on the shelf.
@MainActor
final class StatusItemController: NSObject, NSMenuDelegate {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let shelf: Shelf

    init(shelf: Shelf) {
        self.shelf = shelf
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
        menu.addItem(MenuItem("Clear Shelf", symbol: "xmark.circle", isEnabled: count > 0) { [shelf] in
            shelf.removeAll()
        })

        menu.addItem(.separator())
        menu.addItem(MenuItem("Launch at Login", isOn: LaunchAtLogin.isEnabled) {
            LaunchAtLogin.toggle()
        })

        menu.addItem(.separator())
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
