import AppKit
import Observation

/// A file parked on the shelf. Ferry only keeps a reference: the file stays
/// where it is until it gets dragged out to its new home.
struct ShelfItem: Identifiable, Hashable {
    let url: URL
    let name: String
    let kind: String
    let byteCount: Int64?
    let isFolder: Bool

    var id: URL { url }

    init(url: URL) {
        let values = try? url.resourceValues(forKeys: [
            .localizedNameKey, .localizedTypeDescriptionKey,
            .fileSizeKey, .isDirectoryKey, .isPackageKey,
        ])
        self.url = url
        name = values?.localizedName ?? url.lastPathComponent
        kind = values?.localizedTypeDescription ?? "Document"
        byteCount = values?.fileSize.map(Int64.init)
        isFolder = (values?.isDirectory ?? false) && !(values?.isPackage ?? false)
    }
}

@MainActor
@Observable
final class Shelf {
    private(set) var items: [ShelfItem] = []

    /// Promised files that haven't arrived yet.
    private(set) var incomingCount = 0 {
        didSet {
            if incomingCount != oldValue {
                onChange?()
            }
        }
    }

    /// Called after every change to `items` or `incomingCount`.
    @ObservationIgnored var onChange: (() -> Void)?

    var isEmpty: Bool { items.isEmpty }
    var isReceiving: Bool { incomingCount > 0 }
    var urls: [URL] { items.map(\.url) }

    func add(_ urls: [URL]) {
        update { items in
            var known = Set(items.map(\.url))
            let added = urls
                .map(\.standardizedFileURL)
                .filter { known.insert($0).inserted }
            items.append(contentsOf: added.map(ShelfItem.init))
        }
    }

    /// Files another app promised to write, like photos from Photos, images
    /// from a browser or attachments from Mail. They land in the inbox and
    /// join the shelf as they arrive.
    func receive(_ promises: [NSFilePromiseReceiver]) {
        for promise in promises {
            incomingCount += max(promise.fileNames.count, 1)
            Inbox.receive(promise) { [weak self] url in
                self?.promisedFileArrived(url)
            }
        }
    }

    /// `nil` when the other app couldn't deliver the file.
    private func promisedFileArrived(_ url: URL?) {
        // Add it before counting down, so the shelf never looks empty in between.
        if let url {
            add([url])
        }
        incomingCount = max(incomingCount - 1, 0)
    }

    func remove(_ urls: [URL]) {
        let removed = Set(urls.map(\.standardizedFileURL))
        update { $0.removeAll { removed.contains($0.url) } }
    }

    func removeAll() {
        update { $0.removeAll() }
    }

    /// Forgets items whose files were moved or deleted outside of Ferry.
    func pruneMissing() {
        update { $0.removeAll { !FileManager.default.fileExists(atPath: $0.url.path) } }
    }

    private func update(_ change: (inout [ShelfItem]) -> Void) {
        var updated = items
        change(&updated)
        guard updated != items else { return }
        items = updated
        onChange?()
    }
}
