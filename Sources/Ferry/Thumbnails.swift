import AppKit
import QuickLookThumbnailing

/// A file's preview: real content (image, PDF page…) when Quick Look can
/// render one, the Finder icon otherwise.
final class Thumbnail {
    let image: NSImage
    let isPreview: Bool

    init(image: NSImage, isPreview: Bool) {
        self.image = image
        self.isPreview = isPreview
    }
}

@MainActor
enum Thumbnails {
    private static let cache = NSCache<NSURL, Thumbnail>()

    static func cached(for url: URL) -> Thumbnail? {
        cache.object(forKey: url as NSURL)
    }

    static func icon(for url: URL) -> NSImage {
        NSWorkspace.shared.icon(forFile: url.path)
    }

    static func thumbnail(for url: URL, side: CGFloat) async -> Thumbnail {
        if let cached = cached(for: url) {
            return cached
        }
        let request = QLThumbnailGenerator.Request(
            fileAt: url,
            size: CGSize(width: side, height: side),
            scale: NSScreen.main?.backingScaleFactor ?? 2,
            representationTypes: .thumbnail
        )
        let thumbnail: Thumbnail
        if let representation = try? await QLThumbnailGenerator.shared.generateBestRepresentation(for: request) {
            thumbnail = Thumbnail(image: representation.nsImage, isPreview: true)
        } else {
            thumbnail = Thumbnail(image: icon(for: url), isPreview: false)
        }
        cache.setObject(thumbnail, forKey: url as NSURL)
        return thumbnail
    }
}
