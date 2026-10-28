import AppKit
import SwiftUI

struct ShelfView: View {
    static let size = CGSize(width: 208, height: 232)
    private static let padding: CGFloat = 10
    private static let zoneRadius = ShelfPanel.cornerRadius - padding
    private static let listPadding: CGFloat = 5

    let shelf: Shelf
    var onClose: () -> Void

    @State private var isTargeted = false
    @State private var isExpanded = false
    @State private var selection = FileSelection()
    @State private var draggingOut: Set<URL> = []

    /// Newest first, matching the pile.
    private var listedItems: [ShelfItem] { shelf.items.reversed() }
    private var showsList: Bool { isExpanded && shelf.items.count > 1 }

    var body: some View {
        VStack(spacing: 10) {
            dropZone
            ShelfFooter(
                items: shelf.items,
                selection: showsList ? selection : nil,
                onToggleList: { isExpanded.toggle() },
                onClose: onClose
            )
        }
        .padding(Self.padding)
        .frame(width: Self.size.width, height: Self.size.height)
        .background(WindowDragArea())
        .onChange(of: shelf.urls) { _, urls in
            selection.keep(only: urls)
            if urls.count < 2 {
                isExpanded = false
            }
        }
        .onChange(of: isExpanded) {
            selection.clear()
        }
        .animation(.spring(response: 0.35, dampingFraction: 0.72), value: shelf.items)
        .animation(.spring(response: 0.3, dampingFraction: 0.85), value: showsList)
        .animation(.spring(response: 0.25, dampingFraction: 0.8), value: isTargeted)
        .animation(.easeOut(duration: 0.12), value: selection)
        .animation(.easeOut(duration: 0.15), value: draggingOut)
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
            } else if showsList {
                fileList
                    .clipShape(shape)
                    .transition(.opacity.combined(with: .scale(scale: 0.96)))
            } else {
                FileStack(items: shelf.items)
                    .opacity(draggingOut.isEmpty ? 1 : 0.35)
                    .transition(.scale(scale: 0.7).combined(with: .opacity))
            }
        }
        .overlay(alignment: .topTrailing) {
            if shelf.items.count > 1 && !showsList {
                CountBadge(count: shelf.items.count)
                    .padding(8)
                    .transition(.scale.combined(with: .opacity))
            }
        }
        .scaleEffect(isTargeted ? 1.03 : 1)
        .overlay {
            if !showsList {
                pileDragArea
            }
        }
    }

    private var borderColor: Color {
        if isTargeted { return .accentColor }
        return shelf.isEmpty ? Color.primary.opacity(0.22) : .clear
    }

    // MARK: - Pile

    /// The pile drags everything at once; clicking it lists the files.
    private var pileDragArea: FileDragArea {
        dragArea(
            urls: { shelf.urls },
            onMouseDown: { event in
                if event.clickCount == 2, shelf.items.count == 1 {
                    NSWorkspace.shared.open(shelf.urls[0])
                }
            },
            onClick: { _ in
                if shelf.items.count > 1 {
                    isExpanded = true
                }
            },
            menu: { pileMenu() }
        )
    }

    private func pileMenu() -> NSMenu? {
        let urls = shelf.urls
        guard !urls.isEmpty else { return nil }
        let menu = Self.fileMenu(for: urls)
        if urls.count > 1 {
            menu.addItem(MenuItem("Show Files", symbol: "list.bullet") {
                isExpanded = true
            })
        }
        menu.addItem(MenuItem("Clear Shelf", symbol: "xmark.circle") { [shelf] in
            shelf.removeAll()
        })
        return menu
    }

    // MARK: - File list

    private var fileList: some View {
        let items = listedItems
        let order = items.map(\.url)
        return ScrollView {
            VStack(spacing: 1) {
                ForEach(items) { item in
                    FileRow(
                        item: item,
                        isSelected: selection.contains(item.url),
                        onToggle: { selection.toggle(item.url) },
                        dragArea: rowDragArea(for: item.url, in: order)
                    )
                    .opacity(draggingOut.contains(item.url) ? 0.35 : 1)
                    .transition(.opacity)
                }
            }
            .padding(Self.listPadding)
        }
        .scrollIndicators(.automatic)
    }

    /// A row drags itself, or the whole selection when it's part of it.
    private func rowDragArea(for url: URL, in order: [URL]) -> FileDragArea {
        dragArea(
            urls: { selection.urlsToDrag(from: url, in: order) },
            onMouseDown: { event in
                if event.clickCount == 2 {
                    NSWorkspace.shared.open(url)
                } else {
                    selection.press(url, in: order, modifiers: event.modifierFlags)
                }
            },
            onClick: { event in
                selection.click(url, modifiers: event.modifierFlags)
            },
            menu: { rowMenu(for: selection.urlsToDrag(from: url, in: order), allURLs: order) }
        )
    }

    private func rowMenu(for urls: [URL], allURLs: [URL]) -> NSMenu {
        let menu = Self.fileMenu(for: urls)
        menu.addItem(MenuItem("Select All", symbol: "checklist") {
            selection.selectAll(allURLs)
        })
        menu.addItem(MenuItem("Remove from Shelf", symbol: "minus.circle") { [shelf] in
            shelf.remove(urls)
        })
        return menu
    }

    // MARK: - Shared

    private func dragArea(
        urls: @escaping () -> [URL],
        onMouseDown: @escaping (NSEvent) -> Void,
        onClick: @escaping (NSEvent) -> Void,
        menu: @escaping () -> NSMenu?
    ) -> FileDragArea {
        FileDragArea(
            urls: urls,
            onDrop: { shelf.add($0) },
            onTargetingChange: { isTargeted = $0 },
            onMouseDown: onMouseDown,
            onClick: onClick,
            onDragOutBegan: { draggingOut = Set($0) },
            onDragOutEnded: { urls, wasDropped in
                draggingOut = []
                if wasDropped {
                    shelf.remove(urls)
                }
            },
            menu: menu
        )
    }

    /// Open and Show in Finder, followed by a separator for extra items.
    private static func fileMenu(for urls: [URL]) -> NSMenu {
        let menu = NSMenu()
        menu.addItem(MenuItem("Open", symbol: "arrow.up.forward.app") {
            urls.forEach { NSWorkspace.shared.open($0) }
        })
        menu.addItem(MenuItem("Show in Finder", symbol: "folder") {
            NSWorkspace.shared.activateFileViewerSelecting(urls)
        })
        menu.addItem(.separator())
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

/// One file in the list: drag it out on its own, or tick a few and drag
/// them together.
private struct FileRow: View {
    let item: ShelfItem
    let isSelected: Bool
    var onToggle: () -> Void
    let dragArea: FileDragArea

    @State private var isHovering = false

    var body: some View {
        HStack(spacing: 0) {
            HStack(spacing: 8) {
                FileThumbnail(url: item.url, cornerRadius: 3, borderWidth: 1)
                    .frame(width: 26, height: 26)
                VStack(alignment: .leading, spacing: 0) {
                    Text(item.name)
                        .font(.system(size: 12, weight: .medium))
                        .lineLimit(1)
                        .truncationMode(.middle)
                    Text(item.detail)
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 0)
            }
            .padding(.leading, 6)
            .padding(.vertical, 4)
            .overlay { dragArea }

            Button(action: onToggle) {
                Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                    .font(.system(size: 14))
                    .foregroundStyle(isSelected ? Color.accentColor : Color.secondary.opacity(isHovering ? 0.8 : 0.45))
                    .contentTransition(.symbolEffect(.replace))
                    .frame(width: 28, height: 34)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .help(isSelected ? "Deselect" : "Select")
        }
        .background(
            RoundedRectangle(cornerRadius: 6, style: .continuous)
                .fill(rowFill)
        )
        .onHover { isHovering = $0 }
    }

    private var rowFill: Color {
        if isSelected { return Color.accentColor.opacity(0.18) }
        return isHovering ? Color.primary.opacity(0.06) : .clear
    }
}

private struct FileThumbnail: View {
    let url: URL
    let cornerRadius: CGFloat
    let borderWidth: CGFloat
    @State private var thumbnail: Thumbnail?

    init(url: URL, cornerRadius: CGFloat = 6, borderWidth: CGFloat = 2) {
        self.url = url
        self.cornerRadius = cornerRadius
        self.borderWidth = borderWidth
        _thumbnail = State(initialValue: Thumbnails.cached(for: url))
    }

    var body: some View {
        Group {
            if let thumbnail, thumbnail.isPreview {
                let shape = RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                Image(nsImage: thumbnail.image)
                    .resizable()
                    .scaledToFit()
                    .clipShape(shape)
                    .overlay(shape.strokeBorder(.white.opacity(0.9), lineWidth: borderWidth))
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
    /// Set while the file list is showing.
    let selection: FileSelection?
    var onToggleList: () -> Void
    var onClose: () -> Void

    var body: some View {
        HStack(spacing: 6) {
            VStack(alignment: .leading, spacing: 1) {
                Text(title)
                    .font(.system(size: 12, weight: .semibold))
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .contentTransition(.numericText())
                Text(subtitle)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
            if items.count > 1 {
                FooterButton(
                    symbol: selection == nil ? "list.bullet" : "square.stack",
                    help: selection == nil ? "Show Files" : "Show as Pile",
                    action: onToggleList
                )
                .transition(.scale.combined(with: .opacity))
            }
            FooterButton(symbol: "xmark", help: items.isEmpty ? "Close" : "Clear Shelf", action: onClose)
        }
        .padding(.horizontal, 4)
    }

    private var selectedItems: [ShelfItem] {
        guard let selection else { return [] }
        return items.filter { selection.contains($0.url) }
    }

    private var title: String {
        if !selectedItems.isEmpty {
            return "\(selectedItems.count) of \(items.count) selected"
        }
        switch items.count {
        case 0: return "Ferry"
        case 1: return items[0].name
        default: return StatusItemController.itemCount(items.count)
        }
    }

    private var subtitle: String {
        switch items.count {
        case 0: return "Drop, switch, drag out"
        case 1: return items[0].detail
        default:
            if selection == nil { return ShelfItem.summary(of: items) }
            return selectedItems.isEmpty ? "Select files to drag" : ShelfItem.summary(of: selectedItems)
        }
    }
}

private struct FooterButton: View {
    let symbol: String
    let help: String
    var action: () -> Void

    @State private var isHovering = false

    var body: some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 9, weight: .bold))
                .frame(width: 20, height: 20)
                .background(Circle().fill(Color.primary.opacity(isHovering ? 0.16 : 0.08)))
                .contentShape(Circle())
                .contentTransition(.symbolEffect(.replace))
        }
        .buttonStyle(.plain)
        .foregroundStyle(.secondary)
        .onHover { isHovering = $0 }
        .help(help)
    }
}

private extension ShelfItem {
    /// "PNG image · 180 KB", or just the kind for folders.
    var detail: String {
        guard !isFolder, let byteCount else { return kind }
        return "\(kind) · \(Self.format(byteCount))"
    }

    /// Total size of the files plus a folder count, e.g. "3,4 MB · 1 folder".
    static func summary(of items: [ShelfItem]) -> String {
        let bytes = items.compactMap { $0.isFolder ? nil : $0.byteCount }.reduce(0, +)
        let folders = items.filter(\.isFolder).count
        var parts: [String] = []
        if bytes > 0 { parts.append(format(bytes)) }
        if folders > 0 { parts.append(folders == 1 ? "1 folder" : "\(folders) folders") }
        return parts.isEmpty ? "Ready to move" : parts.joined(separator: " · ")
    }

    static func format(_ bytes: Int64) -> String {
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
