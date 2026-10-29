import AppKit
import Carbon.HIToolbox
import Testing
@testable import Ferry

struct KeyShortcutTests {
    @Test func writesModifiersInMenuOrder() {
        let shortcut = KeyShortcut(keyCode: UInt16(kVK_Space), modifiers: [.command, .shift, .option, .control])
        #expect(shortcut.displayString == "⌃⌥⇧⌘Space")
        #expect(shortcut.keyEquivalent == " ")
    }

    @Test func namesFunctionAndArrowKeys() {
        #expect(KeyShortcut(keyCode: UInt16(kVK_F5), modifiers: []).displayString == "F5")
        #expect(KeyShortcut(keyCode: UInt16(kVK_LeftArrow), modifiers: .command).displayString == "⌘←")
    }

    @Test func needsAModifierUnlessItsAFunctionKey() {
        #expect(KeyShortcut.standard.isValid)
        #expect(!KeyShortcut(keyCode: UInt16(kVK_ANSI_F), modifiers: []).isValid)
        #expect(!KeyShortcut(keyCode: UInt16(kVK_ANSI_F), modifiers: .shift).isValid)
        #expect(KeyShortcut(keyCode: UInt16(kVK_F5), modifiers: []).isValid)
    }

    @Test func convertsModifiersForCarbon() {
        #expect(KeyShortcut.standard.carbonModifiers == UInt32(controlKey | optionKey))
    }

    @Test func dropsModifiersThatCantBePartOfAShortcut() {
        let shortcut = KeyShortcut(keyCode: 3, modifiers: [.command, .capsLock, .function])
        #expect(shortcut.modifiers == .command)
    }

    @Test func survivesBeingSaved() throws {
        let data = try JSONEncoder().encode(KeyShortcut.standard)
        #expect(try JSONDecoder().decode(KeyShortcut.self, from: data) == .standard)
    }
}
