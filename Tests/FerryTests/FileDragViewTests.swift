import AppKit
import Testing
import UniformTypeIdentifiers
@testable import Ferry

@MainActor
struct FileDragViewTests {
    private let view = FileDragView()
    private let file = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("ferry-test.txt")

    @Test func acceptsDroppedFilesWithoutAskingToMoveThem() {
        var dropped: [URL] = []
        var targeted: [Bool] = []
        view.onDrop = { urls, _ in dropped = urls }
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

    @Test func acceptsFilesThatOtherAppsPromiseToWrite() {
        var promised: [NSFilePromiseReceiver] = []
        view.onDrop = { _, promises in promised = promises }
        let photo = PromisedPhoto(name: "IMG_0001.png")
        let info = FakeDraggingInfo(writing: [photo.provider], mask: .copy)

        #expect(view.draggingEntered(info) == .copy)
        #expect(view.performDragOperation(info))
        #expect(promised.count == 1)
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

/// Plays the part of Photos: promises a picture and writes it on request.
final class PromisedPhoto: NSObject, NSFilePromiseProviderDelegate {
    let name: String
    private(set) lazy var provider = NSFilePromiseProvider(fileType: UTType.png.identifier, delegate: self)

    init(name: String) {
        self.name = name
    }

    func filePromiseProvider(_ filePromiseProvider: NSFilePromiseProvider, fileNameForType fileType: String) -> String {
        name
    }

    func filePromiseProvider(
        _ filePromiseProvider: NSFilePromiseProvider,
        writePromiseTo url: URL,
        completionHandler: @escaping (Error?) -> Void
    ) {
        do {
            try Data([0x89, 0x50, 0x4E, 0x47]).write(to: url)
            completionHandler(nil)
        } catch {
            completionHandler(error)
        }
    }
}

/// A stand-in for the system's drag info, backed by a private pasteboard.
@MainActor
private final class FakeDraggingInfo: NSObject, @preconcurrency NSDraggingInfo {
    let draggingPasteboard: NSPasteboard
    let draggingSource: Any?
    let draggingSourceOperationMask: NSDragOperation

    convenience init(urls: [URL], source: Any? = nil, mask: NSDragOperation = .every) {
        self.init(writing: urls.map { $0 as NSURL }, source: source, mask: mask)
    }

    init(writing objects: [NSPasteboardWriting], source: Any? = nil, mask: NSDragOperation = .every) {
        draggingPasteboard = NSPasteboard(name: NSPasteboard.Name("FerryTests-\(UUID().uuidString)"))
        draggingPasteboard.clearContents()
        draggingPasteboard.writeObjects(objects)
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
