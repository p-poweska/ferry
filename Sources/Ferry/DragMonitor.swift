import AppKit

/// Notices when the user starts dragging files in any app, including photos,
/// images and mail that apps promise to write as files on drop.
///
/// Global mouse monitors don't need Accessibility access. A drag session
/// writes to the shared drag pasteboard, so a change in its `changeCount`
/// between mouse down and mouse dragged means a real drag has begun.
@MainActor
final class DragMonitor {
    var onFileDragBegan: () -> Void = {}
    var onFileDragEnded: () -> Void = {}

    private let pasteboard: NSPasteboard
    /// The pasteboard as it was last looked at.
    private var checkedChangeCount = 0
    private var isDraggingFiles = false
    private var monitors: [Any] = []

    init(pasteboard: NSPasteboard = NSPasteboard(name: .drag)) {
        self.pasteboard = pasteboard
    }

    func start() {
        guard monitors.isEmpty else { return }
        checkedChangeCount = pasteboard.changeCount
        monitors = [
            monitor(.leftMouseDown) { $0.mouseDown() },
            monitor(.leftMouseDragged) { $0.mouseDragged() },
            monitor(.leftMouseUp) { $0.mouseUp() },
        ].compactMap(\.self)
    }

    func stop() {
        monitors.forEach(NSEvent.removeMonitor)
        monitors = []
        isDraggingFiles = false
    }

    private func monitor(_ type: NSEvent.EventTypeMask, _ handler: @escaping (DragMonitor) -> Void) -> Any? {
        NSEvent.addGlobalMonitorForEvents(matching: type) { [weak self] _ in
            MainActor.assumeIsolated {
                if let self { handler(self) }
            }
        }
    }

    func mouseDown() {
        checkedChangeCount = pasteboard.changeCount
        isDraggingFiles = false
    }

    func mouseDragged() {
        // Safari empties the pasteboard first and adds the files a moment
        // later, so look again every time it changes.
        guard !isDraggingFiles, pasteboard.changeCount != checkedChangeCount else { return }
        checkedChangeCount = pasteboard.changeCount
        isDraggingFiles = pasteboard.hasFiles
        if isDraggingFiles {
            onFileDragBegan()
        }
    }

    func mouseUp() {
        if isDraggingFiles {
            onFileDragEnded()
        }
        isDraggingFiles = false
    }
}
