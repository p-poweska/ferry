import AppKit
import Testing
@testable import Ferry

struct FileSelectionTests {
    private let a = URL(fileURLWithPath: "/tmp/a.txt")
    private let b = URL(fileURLWithPath: "/tmp/b.txt")
    private let c = URL(fileURLWithPath: "/tmp/c.txt")
    private let d = URL(fileURLWithPath: "/tmp/d.txt")
    private var order: [URL] { [a, b, c, d] }

    @Test func plainPressSelectsOnlyThatRow() {
        var selection = FileSelection()
        selection.press(a, in: order, modifiers: [])
        selection.press(b, in: order, modifiers: [])
        #expect(selection.selected == [b])
    }

    @Test func commandPressTogglesRows() {
        var selection = FileSelection()
        selection.press(a, in: order, modifiers: [])
        selection.press(c, in: order, modifiers: .command)
        #expect(selection.selected == [a, c])

        selection.press(a, in: order, modifiers: .command)
        #expect(selection.selected == [c])
    }

    @Test func shiftPressSelectsRangeFromAnchor() {
        var selection = FileSelection()
        selection.press(b, in: order, modifiers: [])
        selection.press(d, in: order, modifiers: .shift)
        #expect(selection.selected == [b, c, d])

        // The anchor stays put, so the range can shrink back.
        selection.press(a, in: order, modifiers: .shift)
        #expect(selection.selected == [a, b])
    }

    @Test func pressingASelectedRowKeepsTheSelectionForDragging() {
        var selection = FileSelection()
        selection.toggle(a)
        selection.toggle(c)

        selection.press(c, in: order, modifiers: [])
        #expect(selection.selected == [a, c])

        // Releasing without dragging narrows it down to the clicked row.
        selection.click(c, modifiers: [])
        #expect(selection.selected == [c])
    }

    @Test func dragsSelectionInListOrderOrJustTheRow() {
        var selection = FileSelection()
        selection.toggle(d)
        selection.toggle(b)

        #expect(selection.urlsToDrag(from: d, in: order) == [b, d])
        #expect(selection.urlsToDrag(from: a, in: order) == [a])
    }

    @Test func forgetsFilesThatLeftTheShelf() {
        var selection = FileSelection()
        selection.selectAll(order)
        selection.keep(only: [a, c])
        #expect(selection.selected == [a, c])
    }
}
