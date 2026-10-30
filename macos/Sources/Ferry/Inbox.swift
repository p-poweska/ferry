import AppKit

/// Where files promised by other apps get written. Photos, browsers and Mail
/// don't hand over a file when you drag something out of them; they promise
/// to write one wherever it's dropped. Ferry only keeps references, so it
/// takes the files here first.
@MainActor
enum Inbox {
    static let folder = FileManager.default.temporaryDirectory
        .appending(path: "Ferry Inbox", directoryHint: .isDirectory)

    private static let queue: OperationQueue = {
        let queue = OperationQueue()
        queue.qualityOfService = .userInitiated
        return queue
    }()

    /// Clears out files left from the last run. The shelf starts out empty,
    /// so nothing points at them any more.
    static func empty() {
        try? FileManager.default.removeItem(at: folder)
    }

    /// Asks the other app to write the promised files, each drop into a
    /// folder of its own so names can't collide. `arrived` gets every file as
    /// it lands, or `nil` when one couldn't be delivered.
    static func receive(_ promise: NSFilePromiseReceiver, arrived: @escaping @MainActor @Sendable (URL?) -> Void) {
        let destination = folder.appending(path: UUID().uuidString, directoryHint: .isDirectory)
        try? FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
        promise.receivePromisedFiles(atDestination: destination, options: [:], operationQueue: queue, reader: reader(arrived))
    }

    /// Runs on the queue the other app writes on, so unlike the rest of the
    /// inbox it can't belong to the main actor; Swift would stop the app
    /// there. It hands each file over to the main actor instead.
    nonisolated static func reader(
        _ arrived: @escaping @MainActor @Sendable (URL?) -> Void
    ) -> @Sendable (URL, (any Error)?) -> Void {
        { url, error in
            let file = error == nil ? url : nil
            Task { @MainActor in
                arrived(file)
            }
        }
    }
}

extension NSPasteboard {
    /// Promises come first: an app that offers one wants to write the file
    /// itself, and a URL next to it may point inside its library.
    private static var fileClasses: [AnyClass] { [NSFilePromiseReceiver.self, NSURL.self] }
    private static var fileOptions: [ReadingOptionKey: Any] { [.urlReadingFileURLsOnly: true] }

    /// Whether there are files here, real or promised.
    var hasFiles: Bool {
        canReadObject(forClasses: Self.fileClasses, options: Self.fileOptions)
    }

    /// Files to keep a reference to, and promises of files still to be written.
    func files() -> (urls: [URL], promises: [NSFilePromiseReceiver]) {
        let objects = readObjects(forClasses: Self.fileClasses, options: Self.fileOptions) ?? []
        return (objects.compactMap { $0 as? URL }, objects.compactMap { $0 as? NSFilePromiseReceiver })
    }
}
