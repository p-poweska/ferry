import AppKit
import Testing
@testable import Ferry

@MainActor
struct FileDragViewTests {
    private let view = FileDragView()
    private let file = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("ferry-test.txt")

    @Test func acceptsDroppedFilesWithoutAskingToMoveThem() {
        var dropped: [URL] = []
        var targeted: [Bool] = []
        view.onDrop = { dropped = $0 }
        view.onTargetingChange = { targeted.append($0) }
        let info = FakeDraggingInfo(urls: [file])

        #expect(view.draggingEntered(info) == .generic)
        #expect(view.prepareForDragOperation(info))
        #expect(view.performDragOperation(info))
        view.draggingEnded(info)

        #expect(dropped == [file])
        #expect(targeted == [true, false])
    }

    @Test func fallsBackToCopyWhenGenericIsNotOffered() {
        let info = FakeDraggingInfo(urls: [file], mask: [.copy, .move])
        #expect(view.draggingEntered(info) == .copy)
    }

    @Test func rejectsDragsWithoutFiles() {
        let info = FakeDraggingInfo(urls: [URL(string: "https://example.com")!])
        #expect(view.draggingEntered(info) == [])
        #expect(!view.performDragOperation(info))
    }

    @Test func ignoresItsOwnDragComingBack() {
        let info = FakeDraggingInfo(urls: [file], source: view)
        #expect(view.draggingEntered(info) == [])
    }

    @Test func reportsPressAndClickWhenTheMouseDoesNotDrag() {
        var events: [String] = []
        view.onMouseDown = { _ in events.append("down") }
        view.onClick = { _ in events.append("click") }

        view.mouseDown(with: mouseEvent(.leftMouseDown))
        view.mouseUp(with: mouseEvent(.leftMouseUp))

        #expect(events == ["down", "click"])
    }

    private func mouseEvent(_ type: NSEvent.EventType) -> NSEvent {
        NSEvent.mouseEvent(
            with: type, location: .zero, modifierFlags: [], timestamp: 0, windowNumber: 0,
            context: nil, eventNumber: 0, clickCount: 1, pressure: 1
        )!
    }

    @Test func letsFilesBeMovedOnlyOutsideTheApp() {
        let outside = FileDragView.dragOutOperations(for: .outsideApplication)
        #expect(outside.contains(.move))
        #expect(outside.contains(.copy))
        #expect(FileDragView.dragOutOperations(for: .withinApplication) == [])
    }
}

/// A stand-in for the system's drag info, backed by a private pasteboard.
@MainActor
private final class FakeDraggingInfo: NSObject, @preconcurrency NSDraggingInfo {
    let draggingPasteboard: NSPasteboard
    let draggingSource: Any?
    let draggingSourceOperationMask: NSDragOperation

    init(urls: [URL], source: Any? = nil, mask: NSDragOperation = .every) {
        draggingPasteboard = NSPasteboard(name: NSPasteboard.Name("FerryTests-\(UUID().uuidString)"))
        draggingPasteboard.clearContents()
        draggingPasteboard.writeObjects(urls.map { $0 as NSURL })
        draggingSource = source
        draggingSourceOperationMask = mask
    }

    isolated deinit {
        draggingPasteboard.releaseGlobally()
    }

    var draggingDestinationWindow: NSWindow? { nil }
    var draggingLocation: NSPoint { .zero }
    var draggedImageLocation: NSPoint { .zero }
    var draggedImage: NSImage? { nil }
    var draggingSequenceNumber: Int { 1 }
    var draggingFormation: NSDraggingFormation = .default
    var animatesToDestination = false
    var numberOfValidItemsForDrop = 1
    var springLoadingHighlight: NSSpringLoadingHighlight { .none }

    func slideDraggedImage(to screenPoint: NSPoint) {}
    func resetSpringLoading() {}
    func enumerateDraggingItems(
        options enumOpts: NSDraggingItemEnumerationOptions = [],
        for view: NSView?,
        classes classArray: [AnyClass],
        searchOptions: [NSPasteboard.ReadingOptionKey: Any] = [:],
        using block: (NSDraggingItem, Int, UnsafeMutablePointer<ObjCBool>) -> Void
    ) {}
}
