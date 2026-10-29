import Foundation
import Testing
@testable import Ferry

@MainActor
struct InboxTests {
    private let photo = URL(filePath: "/tmp/Ferry Inbox/IMG_0001.heic")

    /// Photos writes the file on a queue of its own and calls back there.
    @Test func handsOverFilesWrittenOnAnotherQueue() async {
        let file = await written(photo, error: nil)

        #expect(file == photo)
    }

    @Test func reportsFilesThatCouldNotBeWritten() async {
        let file = await written(photo, error: CocoaError(.fileWriteNoPermission))

        #expect(file == nil)
    }

    private func written(_ url: URL, error: (any Error)?) async -> URL? {
        await withCheckedContinuation { continuation in
            let reader = Inbox.reader { file in
                #expect(Thread.isMainThread)
                continuation.resume(returning: file)
            }
            let queue = OperationQueue()
            queue.addOperation { reader(url, error) }
        }
    }
}
