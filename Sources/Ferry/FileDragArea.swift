import AppKit
import SwiftUI

/// Transparent AppKit layer over the shelf's drop zone. It accepts files
/// dropped onto the shelf and starts a multi-file drag back out, which
/// SwiftUI's own drag and drop can't do on macOS.
struct FileDragArea: NSViewRepresentable {
    var urls: [URL]
    var onDrop: ([URL]) -> Void
    var onTargetingChange: (Bool) -> Void
    var onDragOutChange: (Bool) -> Void
    var onDragOutComplete: ([URL]) -> Void
    var menu: () -> NSMenu?

    func makeNSView(context: Context) -> FileDragView {
        FileDragView()
    }

    func updateNSView(_ view: FileDragView, context: Context) {
        view.urls = urls
        view.onDrop = onDrop
        view.onTargetingChange = onTargetingChange
        view.onDragOutChange = onDragOutChange
        view.onDragOutComplete = onDragOutComplete
        view.menuProvider = menu
    }
}

final class FileDragView: NSView, NSDraggingSource {
    var urls: [URL] = []
    var onDrop: ([URL]) -> Void = { _ in }
    var onTargetingChange: (Bool) -> Void = { _ in }
    var onDragOutChange: (Bool) -> Void = { _ in }
    var onDragOutComplete: ([URL]) -> Void = { _ in }
    var menuProvider: () -> NSMenu? = { nil }

    private var mouseDownEvent: NSEvent?
    private var draggedURLs: [URL] = []

    override init(frame: NSRect) {
        super.init(frame: frame)
        registerForDraggedTypes([.fileURL])
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var mouseDownCanMoveWindow: Bool { false }

    override func menu(for event: NSEvent) -> NSMenu? {
        menuProvider()
    }

    // MARK: - Dropping files onto the shelf

    private func fileURLs(on pasteboard: NSPasteboard) -> [URL] {
        let options: [NSPasteboard.ReadingOptionKey: Any] = [.urlReadingFileURLsOnly: true]
        return pasteboard.readObjects(forClasses: [NSURL.self], options: options) as? [URL] ?? []
    }

    private func operation(for info: NSDraggingInfo) -> NSDragOperation {
        guard info.draggingSource as AnyObject? !== self,
              !fileURLs(on: info.draggingPasteboard).isEmpty
        else { return [] }
        // The shelf only remembers where files are, so ask the source for a
        // plain drop instead of a copy or a move.
        let preferred: [NSDragOperation] = [.generic, .copy, .link]
        return preferred.first { info.draggingSourceOperationMask.contains($0) } ?? []
    }

    override func draggingEntered(_ sender: NSDraggingInfo) -> NSDragOperation {
        let operation = operation(for: sender)
        onTargetingChange(operation != [])
        return operation
    }

    override func draggingUpdated(_ sender: NSDraggingInfo) -> NSDragOperation {
        operation(for: sender)
    }

    override func draggingExited(_ sender: NSDraggingInfo?) {
        onTargetingChange(false)
    }

    override func draggingEnded(_ sender: NSDraggingInfo) {
        onTargetingChange(false)
    }

    override func prepareForDragOperation(_ sender: NSDraggingInfo) -> Bool {
        operation(for: sender) != []
    }

    override func performDragOperation(_ sender: NSDraggingInfo) -> Bool {
        let urls = fileURLs(on: sender.draggingPasteboard)
        guard !urls.isEmpty else { return false }
        onDrop(urls)
        return true
    }

    // MARK: - Dragging files off the shelf

    override func mouseDown(with event: NSEvent) {
        mouseDownEvent = event
    }

    override func mouseUp(with event: NSEvent) {
        mouseDownEvent = nil
    }

    override func mouseDragged(with event: NSEvent) {
        guard let start = mouseDownEvent else { return }
        let dx = event.locationInWindow.x - start.locationInWindow.x
        let dy = event.locationInWindow.y - start.locationInWindow.y
        guard hypot(dx, dy) > 3 else { return }
        mouseDownEvent = nil
        beginDragOut(with: start)
    }

    private func beginDragOut(with event: NSEvent) {
        guard !urls.isEmpty else { return }
        draggedURLs = urls

        let side: CGFloat = 64
        let point = convert(event.locationInWindow, from: nil)
        let frame = NSRect(x: point.x - side / 2, y: point.y - side / 2, width: side, height: side)
        // Topmost card first, matching the stack shown on the shelf.
        let items = urls.reversed().map { url in
            let item = NSDraggingItem(pasteboardWriter: url as NSURL)
            item.setDraggingFrame(frame, contents: Thumbnails.cached(for: url)?.image ?? Thumbnails.icon(for: url))
            return item
        }

        let session = beginDraggingSession(with: items, event: event, source: self)
        session.draggingFormation = .stack
        session.animatesToStartingPositionsOnCancelOrFail = true
    }

    func draggingSession(
        _ session: NSDraggingSession,
        sourceOperationMaskFor context: NSDraggingContext
    ) -> NSDragOperation {
        // Finder moves on the same volume and copies across volumes; holding
        // Option forces a copy, just like dragging from a Finder window.
        context == .outsideApplication ? [.move, .copy, .generic, .delete] : []
    }

    func draggingSession(_ session: NSDraggingSession, willBeginAt screenPoint: NSPoint) {
        onDragOutChange(true)
    }

    func draggingSession(
        _ session: NSDraggingSession,
        endedAt screenPoint: NSPoint,
        operation: NSDragOperation
    ) {
        onDragOutChange(false)
        if operation != [] {
            onDragOutComplete(draggedURLs)
        }
        draggedURLs = []
    }
}
