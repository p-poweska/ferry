import AppKit
import Testing
@testable import Ferry

@MainActor
struct DragMonitorTests {
    private let pasteboard = NSPasteboard(name: NSPasteboard.Name("FerryTests-\(UUID().uuidString)"))
    private let file = URL(filePath: "/tmp/photo.jpeg")

    private func monitor() -> (DragMonitor, events: () -> [String]) {
        let monitor = DragMonitor(pasteboard: pasteboard)
        var events: [String] = []
        monitor.onFileDragBegan = { events.append("began") }
        monitor.onFileDragEnded = { events.append("ended") }
        return (monitor, { events })
    }

    @Test func noticesADragOfFiles() {
        let (monitor, events) = monitor()

        monitor.mouseDown()
        pasteboard.clearContents()
        pasteboard.writeObjects([file as NSURL])
        monitor.mouseDragged()
        monitor.mouseDragged()
        monitor.mouseUp()

        #expect(events() == ["began", "ended"])
        pasteboard.releaseGlobally()
    }

    /// Safari empties the pasteboard as the drag begins and adds the
    /// image a moment later.
    @Test func noticesFilesAddedAfterTheDragBegan() {
        let (monitor, events) = monitor()

        monitor.mouseDown()
        pasteboard.clearContents()
        monitor.mouseDragged()
        #expect(events().isEmpty)

        pasteboard.clearContents()
        pasteboard.writeObjects([file as NSURL])
        monitor.mouseDragged()

        #expect(events() == ["began"])
        pasteboard.releaseGlobally()
    }

    @Test func ignoresDragsWithoutFiles() {
        let (monitor, events) = monitor()

        monitor.mouseDown()
        monitor.mouseDragged()
        pasteboard.clearContents()
        pasteboard.writeObjects(["Some text" as NSString])
        monitor.mouseDragged()
        monitor.mouseUp()

        #expect(events().isEmpty)
        pasteboard.releaseGlobally()
    }
}
