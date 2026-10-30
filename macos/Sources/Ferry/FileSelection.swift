import AppKit

/// Finder-style selection for the shelf's file list: pick a few files and
/// drag just those, leaving the rest on the shelf.
struct FileSelection: Equatable {
    private(set) var selected: Set<URL> = []
    /// The row a ⇧-click extends from.
    private var anchor: URL?

    var isEmpty: Bool { selected.isEmpty }
    var count: Int { selected.count }

    func contains(_ url: URL) -> Bool {
        selected.contains(url)
    }

    /// Mouse down on a row. ⌘ toggles it, ⇧ selects the range from the last
    /// clicked row, and a plain press selects just this row unless it's
    /// already selected, so the whole selection can be dragged away.
    mutating func press(_ url: URL, in order: [URL], modifiers: NSEvent.ModifierFlags) {
        if modifiers.contains(.command) {
            toggle(url)
        } else if modifiers.contains(.shift) {
            let anchor = anchor ?? url
            guard let from = order.firstIndex(of: anchor), let to = order.firstIndex(of: url) else { return }
            selected = Set(order[min(from, to)...max(from, to)])
            self.anchor = anchor
        } else if !selected.contains(url) {
            select(url)
        }
    }

    /// A plain click that didn't become a drag narrows the selection to the row.
    mutating func click(_ url: URL, modifiers: NSEvent.ModifierFlags) {
        guard modifiers.isDisjoint(with: [.command, .shift]) else { return }
        select(url)
    }

    mutating func toggle(_ url: URL) {
        if selected.remove(url) == nil {
            selected.insert(url)
        }
        anchor = url
    }

    mutating func selectAll(_ urls: [URL]) {
        selected = Set(urls)
    }

    mutating func clear() {
        selected = []
        anchor = nil
    }

    /// Forgets files that are no longer on the shelf.
    mutating func keep(only urls: [URL]) {
        selected.formIntersection(urls)
        if let anchor, !urls.contains(anchor) {
            self.anchor = nil
        }
    }

    /// Dragging a selected row takes the whole selection along, in list
    /// order; any other row goes on its own.
    func urlsToDrag(from url: URL, in order: [URL]) -> [URL] {
        selected.contains(url) ? order.filter(selected.contains) : [url]
    }

    private mutating func select(_ url: URL) {
        selected = [url]
        anchor = url
    }
}
