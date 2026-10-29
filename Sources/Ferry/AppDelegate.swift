import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private let shelf = Shelf()
    private let preferences = Preferences()
    private let dragMonitor = DragMonitor()
    private let hotKey = GlobalHotKey()
    private var panel: ShelfPanelController!
    private var statusItem: StatusItemController!
    private lazy var settings = SettingsWindowController(preferences: preferences, hotKey: hotKey)

    func applicationDidFinishLaunching(_ notification: Notification) {
        Inbox.empty()
        panel = ShelfPanelController(shelf: shelf, preferences: preferences)
        statusItem = StatusItemController(shelf: shelf, panel: panel, preferences: preferences) { [weak self] in
            self?.settings.show()
        }

        shelf.onChange = { [weak self] in
            self?.statusItem.refresh()
            self?.panel.shelfDidChange()
        }

        dragMonitor.onFileDragBegan = { [weak self] in self?.panel.fileDragBegan() }
        dragMonitor.onFileDragEnded = { [weak self] in self?.panel.fileDragEnded() }
        if preferences.showWhileDragging {
            dragMonitor.start()
        }

        hotKey.onPress = { [weak self] in self?.panel.toggle() }
        hotKey.register(preferences.shortcut)

        preferences.onChange = { [weak self] setting in
            self?.apply(setting)
        }
    }

    private func apply(_ setting: Preferences.Setting) {
        switch setting {
        case .showWhileDragging:
            if preferences.showWhileDragging {
                dragMonitor.start()
            } else {
                dragMonitor.stop()
            }
        case .position:
            panel.positionDidChange()
        case .shortcut:
            hotKey.register(preferences.shortcut)
        }
    }

    /// Opening Ferry again while it's running brings up the shelf.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        panel.show()
        return false
    }
}
