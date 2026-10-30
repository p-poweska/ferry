import Foundation
import Testing
@testable import Ferry

@MainActor
struct ShelfTests {
    private let directory: URL

    init() throws {
        directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("FerryTests-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    }

    private func makeFile(_ name: String, bytes: Int = 4) throws -> URL {
        let url = directory.appendingPathComponent(name)
        try Data(repeating: 0, count: bytes).write(to: url)
        return url
    }

    @Test func addsFilesInOrderWithoutDuplicates() throws {
        let a = try makeFile("a.txt")
        let b = try makeFile("b.txt")
        let shelf = Shelf()

        shelf.add([a, b, a])
        shelf.add([b])

        #expect(shelf.urls == [a.standardizedFileURL, b.standardizedFileURL])
    }

    @Test func readsFileMetadata() throws {
        let file = try makeFile("photo.png", bytes: 1_024)
        let shelf = Shelf()

        shelf.add([file, directory])

        #expect(shelf.items[0].byteCount == 1_024)
        #expect(shelf.items[0].isFolder == false)
        #expect(shelf.items[1].isFolder == true)
    }

    @Test func notifiesOnlyOnRealChanges() throws {
        let a = try makeFile("a.txt")
        let shelf = Shelf()
        var changes = 0
        shelf.onChange = { changes += 1 }

        shelf.add([a])
        shelf.add([a])
        shelf.remove([directory])
        shelf.removeAll()

        #expect(changes == 2)
        #expect(shelf.isEmpty)
    }

    @Test func prunesFilesThatNoLongerExist() throws {
        let kept = try makeFile("kept.txt")
        let moved = try makeFile("moved.txt")
        let shelf = Shelf()
        shelf.add([kept, moved])

        try FileManager.default.removeItem(at: moved)
        shelf.pruneMissing()

        #expect(shelf.urls == [kept.standardizedFileURL])
    }
}
