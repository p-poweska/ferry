import AppKit
import SwiftUI

struct ShelfView: View {
    static let size = CGSize(width: 208, height: 232)
    private static let padding: CGFloat = 10
    private static let zoneRadius = ShelfPanel.cornerRadius - padding

    let shelf: Shelf
    var onClose: () -> Void

    @State private var isTargeted = false
    @State private var isDraggingOut = false

    var body: some View {
        VStack(spacing: 10) {
            dropZone
            ShelfFooter(items: shelf.items, onClose: onClose)
        }
        .padding(Self.padding)
        .frame(width: Self.size.width, height: Self.size.height)
        .background(WindowDragArea())
        .animation(.spring(response: 0.35, dampingFraction: 0.72), value: shelf.items)
        .animation(.spring(response: 0.25, dampingFraction: 0.8), value: isTargeted)
        .animation(.easeOut(duration: 0.15), value: isDraggingOut)
    }

    private var dropZone: some View {
        let shape = RoundedRectangle(cornerRadius: Self.zoneRadius, style: .continuous)
        return ZStack {
            shape.fill(isTargeted ? Color.accentColor.opacity(0.16) : Color.primary.opacity(0.05))
            shape.strokeBorder(
                borderColor,
                style: StrokeStyle(lineWidth: 1.5, dash: shelf.isEmpty && !isTargeted ? [5, 4] : [])
            )

            if shelf.isEmpty {
                EmptyShelfPrompt(isTargeted: isTargeted)
                    .transition(.opacity)
            } else {
                FileStack(items: shelf.items)
                    .opacity(isDraggingOut ? 0.35 : 1)
                    .transition(.scale(scale: 0.7).combined(with: .opacity))
            }
        }
        .overlay(alignment: .topTrailing) {
            if shelf.items.count > 1 {
                CountBadge(count: shelf.items.count)
                    .padding(8)
                    .transition(.scale.combined(with: .opacity))
            }
        }
        .scaleEffect(isTargeted ? 1.03 : 1)
        .overlay {
            FileDragArea(
                urls: shelf.urls,
                onDrop: { shelf.add($0) },
                onTargetingChange: { isTargeted = $0 },
                onDragOutChange: { isDraggingOut = $0 },
                onDragOutComplete: { shelf.remove($0) },
                menu: contextMenu
            )
        }
    }

    private var borderColor: Color {
        if isTargeted { return .accentColor }
        return shelf.isEmpty ? Color.primary.opacity(0.22) : .clear
    }

    private func contextMenu() -> NSMenu? {
        let urls = shelf.urls
        guard !urls.isEmpty else { return nil }
        let menu = NSMenu()
        menu.addItem(MenuItem("Open", symbol: "arrow.up.forward.app") {
            urls.forEach { NSWorkspace.shared.open($0) }
        })
        menu.addItem(MenuItem("Show in Finder", symbol: "folder") {
            NSWorkspace.shared.activateFileViewerSelecting(urls)
        })
        menu.addItem(.separator())
        menu.addItem(MenuItem("Clear Shelf", symbol: "xmark.circle") { [shelf] in
            shelf.removeAll()
        })
        return menu
    }
}

// MARK: - Drop zone content

private struct EmptyShelfPrompt: View {
    let isTargeted: Bool

    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: "tray.and.arrow.down")
                .font(.system(size: 30, weight: .light))
                .foregroundStyle(isTargeted ? Color.accentColor : Color.secondary)
                .symbolEffect(.bounce, value: isTargeted)
            Text(isTargeted ? "Release to add" : "Drop files here")
                .font(.system(size: 12, weight: .medium))
                .foregroundStyle(.secondary)
                .contentTransition(.opacity)
        }
    }
}

/// Up to three cards fanned out like a small pile, newest on top.
private struct FileStack: View {
    let items: [ShelfItem]

    var body: some View {
        let visible = Array(items.suffix(3))
        ZStack {
            ForEach(Array(visible.enumerated()), id: \.element.id) { index, item in
                let depth = visible.count - 1 - index
                FileThumbnail(url: item.url)
                    .frame(width: 100, height: 100)
                    .shadow(color: .black.opacity(0.18), radius: 5, y: 2)
                    .rotationEffect(.degrees(Self.angles[depth]))
                    .offset(x: Self.offsets[depth], y: depth == 0 ? 0 : 3)
                    .transition(.scale(scale: 0.6).combined(with: .opacity))
            }
        }
    }

    private static let angles: [Double] = [0, -9, 8]
    private static let offsets: [CGFloat] = [0, -18, 18]
}

private struct FileThumbnail: View {
    let url: URL
    @State private var thumbnail: Thumbnail?

    init(url: URL) {
        self.url = url
        _thumbnail = State(initialValue: Thumbnails.cached(for: url))
    }

    var body: some View {
        Group {
            if let thumbnail, thumbnail.isPreview {
                let shape = RoundedRectangle(cornerRadius: 6, style: .continuous)
                Image(nsImage: thumbnail.image)
                    .resizable()
                    .scaledToFit()
                    .clipShape(shape)
                    .overlay(shape.strokeBorder(.white.opacity(0.9), lineWidth: 2))
            } else {
                Image(nsImage: thumbnail?.image ?? Thumbnails.icon(for: url))
                    .resizable()
                    .scaledToFit()
            }
        }
        .task(id: url) {
            thumbnail = await Thumbnails.thumbnail(for: url, side: 128)
        }
    }
}

private struct CountBadge: View {
    let count: Int

    var body: some View {
        Text("\(count)")
            .font(.system(size: 11, weight: .bold, design: .rounded))
            .monospacedDigit()
            .foregroundStyle(.white)
            .padding(.horizontal, 6)
            .frame(minWidth: 20, minHeight: 20)
            .background(Capsule().fill(Color.accentColor))
            .contentTransition(.numericText())
    }
}

// MARK: - Footer

private struct ShelfFooter: View {
    let items: [ShelfItem]
    var onClose: () -> Void

    @State private var isHoveringClose = false

    var body: some View {
        HStack(spacing: 8) {
            VStack(alignment: .leading, spacing: 1) {
                Text(title)
                    .font(.system(size: 12, weight: .semibold))
                    .lineLimit(1)
                    .truncationMode(.middle)
                Text(subtitle)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 9, weight: .bold))
                    .frame(width: 20, height: 20)
                    .background(Circle().fill(Color.primary.opacity(isHoveringClose ? 0.16 : 0.08)))
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .foregroundStyle(.secondary)
            .onHover { isHoveringClose = $0 }
            .help(items.isEmpty ? "Close" : "Clear Shelf")
        }
        .padding(.horizontal, 4)
    }

    private var title: String {
        switch items.count {
        case 0: "Ferry"
        case 1: items[0].name
        default: StatusItemController.itemCount(items.count)
        }
    }

    private var subtitle: String {
        if items.isEmpty { return "Drop, switch, drag out" }
        if items.count == 1, let item = items.first {
            if item.isFolder { return item.kind }
            return [item.kind, item.byteCount.map(Self.format)].compactMap(\.self).joined(separator: " · ")
        }
        let bytes = items.compactMap { $0.isFolder ? nil : $0.byteCount }.reduce(0, +)
        let folders = items.filter(\.isFolder).count
        var parts: [String] = []
        if bytes > 0 { parts.append(Self.format(bytes)) }
        if folders > 0 { parts.append(folders == 1 ? "1 folder" : "\(folders) folders") }
        return parts.isEmpty ? "Ready to move" : parts.joined(separator: " · ")
    }

    private static func format(_ bytes: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }
}

// MARK: - Window dragging

/// Lets the user reposition the shelf by dragging its background.
private struct WindowDragArea: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView { HandleView() }
    func updateNSView(_ view: NSView, context: Context) {}

    private final class HandleView: NSView {
        override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

        override func mouseDown(with event: NSEvent) {
            window?.performDrag(with: event)
        }
    }
}
