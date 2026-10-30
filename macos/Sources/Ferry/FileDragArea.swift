import AppKit
import SwiftUI

/// Transparent AppKit layer over the shelf's pile or one of its file rows.
/// It accepts files dropped onto the shelf, including ones other apps only
/// promise to write, and starts a multi-file drag back out, which SwiftUI's
/// own drag and drop can't do on macOS.
struct FileDragArea: NSViewRepresentable {
    /// The files to take along, asked for when a drag starts.
    var urls: () -> [URL]
    /// Dropped files, and promises of files still to be written.
    var onDrop: ([URL], [NSFilePromiseReceiver]) -> Void
    var onTargetingChange: (Bool) -> Void
    var onMouseDown: (NSEvent) -> Void = { _ in }
    /// A mouse up that didn't turn into a drag.
    var onClick: (NSEvent) -> Void = { _ in }
    var onDragOutBegan: ([URL]) -> Void
    /// The dragged files, and whether they were dropped somewhere.
    var onDragOutEnded: ([URL], Bool) -> Void
    var menu: () -> NSMenu?

    func makeNSView(context: Context) -> FileDragView {
        FileDragView()
    }

    func updateNSView(_ view: FileDragView, context: Context) {
        view.urls = urls
        view.onDrop = onDrop
        view.onTargetingChange = onTargetingChange
        view.onMouseDown = onMouseDown
        view.onClick = onClick
        view.onDragOutBegan = onDragOutBegan
        view.onDragOutEnded = onDragOutEnded
        view.menuProvider = menu
    }
}

final class FileDragView: NSView, NSDraggingSource {
    var urls: () -> [URL] = { [] }
    var onDrop: ([URL], [NSFilePromiseReceiver]) -> Void = { _, _ in }
    var onTargetingChange: (Bool) -> Void = { _ in }
    var onMouseDown: (NSEvent) -> Void = { _ in }
    var onClick: (NSEvent) -> Void = { _ in }
    var onDragOutBegan: ([URL]) -> Void = { _ in }
    var onDragOutEnded: ([URL], Bool) -> Void = { _, _ in }
    var menuProvider: () -> NSMenu? = { nil }

    private var mouseDownEvent: NSEvent?
    private var draggedURLs: [URL] = []

    override init(frame: NSRect) {
        super.init(frame: frame)
        let promises = NSFilePromiseReceiver.readableDraggedTypes.map { NSPasteboard.PasteboardType($0) }
        registerForDraggedTypes([.fileURL] + promises)
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

    private func operation(for info: NSDraggingInfo) -> NSDragOperation {
        guard info.draggingSource as AnyObject? !== self, info.draggingPasteboard.hasFiles else { return [] }
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
        let (urls, promises) = sender.draggingPasteboard.files()
        guard !urls.isEmpty || !promises.isEmpty else { return false }
        onDrop(urls, promises)
        return true
    }

    // MARK: - Dragging files off the shelf

    override func mouseDown(with event: NSEvent) {
        mouseDownEvent = event
        onMouseDown(event)
    }

    override func mouseUp(with event: NSEvent) {
        if mouseDownEvent != nil {
            onClick(event)
        }
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
        draggedURLs = urls()
        guard !draggedURLs.isEmpty else { return }

        let side: CGFloat = 64
        let point = convert(event.locationInWindow, from: nil)
        let frame = NSRect(x: point.x - side / 2, y: point.y - side / 2, width: side, height: side)
        // Topmost card first, matching the stack shown on the shelf.
        let items = draggedURLs.reversed().map { url in
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
        Self.dragOutOperations(for: context)
    }

    /// Finder moves on the same volume and copies across volumes; holding
    /// Option forces a copy, just like dragging from a Finder window.
    static func dragOutOperations(for context: NSDraggingContext) -> NSDragOperation {
        context == .outsideApplication ? [.move, .copy, .generic, .delete] : []
    }

    func draggingSession(_ session: NSDraggingSession, willBeginAt screenPoint: NSPoint) {
        onDragOutBegan(draggedURLs)
    }

    func draggingSession(
        _ session: NSDraggingSession,
        endedAt screenPoint: NSPoint,
        operation: NSDragOperation
    ) {
        onDragOutEnded(draggedURLs, operation != [])
        draggedURLs = []
    }
}
