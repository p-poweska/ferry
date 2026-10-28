import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let shelf = Shelf()
    private var statusItem: StatusItemController!

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = StatusItemController(shelf: shelf)

        shelf.onChange = { [weak self] in
            self?.statusItem.refresh()
        }
    }
}
