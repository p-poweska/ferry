import AppKit

/// Notices when the user starts dragging files in any app.
///
/// Global mouse monitors don't need Accessibility access. A drag session
/// writes to the shared drag pasteboard, so a change in its `changeCount`
/// between mouse down and mouse dragged means a real drag has begun.
@MainActor
final class DragMonitor {
    var onFileDragBegan: () -> Void = {}
    var onFileDragEnded: () -> Void = {}

    private let pasteboard = NSPasteboard(name: .drag)
    private var changeCountAtMouseDown = 0
    private var isDragging = false
    private var isDraggingFiles = false
    private var monitors: [Any] = []

    func start() {
        guard monitors.isEmpty else { return }
        changeCountAtMouseDown = pasteboard.changeCount
        monitors = [
            monitor(.leftMouseDown) { $0.mouseDown() },
            monitor(.leftMouseDragged) { $0.mouseDragged() },
            monitor(.leftMouseUp) { $0.mouseUp() },
        ].compactMap(\.self)
    }

    func stop() {
        monitors.forEach(NSEvent.removeMonitor)
        monitors = []
        isDragging = false
        isDraggingFiles = false
    }

    private func monitor(_ type: NSEvent.EventTypeMask, _ handler: @escaping (DragMonitor) -> Void) -> Any? {
        NSEvent.addGlobalMonitorForEvents(matching: type) { [weak self] _ in
            MainActor.assumeIsolated {
                if let self { handler(self) }
            }
        }
    }

    private func mouseDown() {
        changeCountAtMouseDown = pasteboard.changeCount
        isDragging = false
        isDraggingFiles = false
    }

    private func mouseDragged() {
        guard !isDragging, pasteboard.changeCount != changeCountAtMouseDown else { return }
        isDragging = true
        isDraggingFiles = pasteboard.canReadObject(
            forClasses: [NSURL.self],
            options: [.urlReadingFileURLsOnly: true]
        )
        if isDraggingFiles {
            onFileDragBegan()
        }
    }

    private func mouseUp() {
        if isDraggingFiles {
            onFileDragEnded()
        }
        isDragging = false
        isDraggingFiles = false
    }
}
