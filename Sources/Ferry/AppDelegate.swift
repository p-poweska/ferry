import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let shelf = Shelf()
    private let dragMonitor = DragMonitor()
    private var panel: ShelfPanelController!
    private var statusItem: StatusItemController!

    func applicationDidFinishLaunching(_ notification: Notification) {
        panel = ShelfPanelController(shelf: shelf)
        statusItem = StatusItemController(shelf: shelf, panel: panel, dragMonitor: dragMonitor)

        shelf.onChange = { [weak self] in
            self?.statusItem.refresh()
            self?.panel.shelfDidChange()
        }

        dragMonitor.onFileDragBegan = { [weak self] in self?.panel.fileDragBegan() }
        dragMonitor.onFileDragEnded = { [weak self] in self?.panel.fileDragEnded() }
        if Preferences.showWhileDragging {
            dragMonitor.start()
        }
    }

    /// Opening Ferry again while it's running brings up the shelf.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        panel.show()
        return false
    }
}
