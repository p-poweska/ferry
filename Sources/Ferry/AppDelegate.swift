import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let shelf = Shelf()
    private var panel: ShelfPanelController!
    private var statusItem: StatusItemController!

    func applicationDidFinishLaunching(_ notification: Notification) {
        panel = ShelfPanelController(shelf: shelf)
        statusItem = StatusItemController(shelf: shelf, panel: panel)

        shelf.onChange = { [weak self] in
            self?.statusItem.refresh()
            self?.panel.shelfDidChange()
        }
    }
}
