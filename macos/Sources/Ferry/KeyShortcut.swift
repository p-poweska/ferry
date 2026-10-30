import AppKit
import Carbon.HIToolbox

/// A key combination. The key is stored as a hardware key code, so the
/// shortcut keeps working whatever keyboard layout is active.
struct KeyShortcut: Codable, Hashable, Sendable {
    let keyCode: UInt16
    private let modifierBits: UInt

    static let supportedModifiers: NSEvent.ModifierFlags = [.control, .option, .shift, .command]
    /// ⌃⌥F, for Ferry.
    static let standard = KeyShortcut(keyCode: UInt16(kVK_ANSI_F), modifiers: [.control, .option])

    init(keyCode: UInt16, modifiers: NSEvent.ModifierFlags) {
        self.keyCode = keyCode
        modifierBits = modifiers.intersection(Self.supportedModifiers).rawValue
    }

    init(event: NSEvent) {
        self.init(keyCode: event.keyCode, modifiers: event.modifierFlags)
    }

    var modifiers: NSEvent.ModifierFlags {
        NSEvent.ModifierFlags(rawValue: modifierBits)
    }

    /// Needs ⌃, ⌥ or ⌘ so it doesn't swallow ordinary typing; function keys
    /// can go on their own.
    var isValid: Bool {
        !modifiers.isDisjoint(with: [.control, .option, .command]) || Self.functionKeys.contains(Int(keyCode))
    }

    /// The modifiers in Carbon's format, for `RegisterEventHotKey`.
    var carbonModifiers: UInt32 {
        var flags = 0
        if modifiers.contains(.control) { flags |= controlKey }
        if modifiers.contains(.option) { flags |= optionKey }
        if modifiers.contains(.shift) { flags |= shiftKey }
        if modifiers.contains(.command) { flags |= cmdKey }
        return UInt32(flags)
    }

    /// The way menus write it, like "⌃⌥F".
    var displayString: String {
        Self.symbols(for: modifiers) + (Self.specialKeys[Int(keyCode)]?.name ?? Self.character(for: keyCode)?.uppercased() ?? "?")
    }

    /// For showing the shortcut next to a menu item.
    var keyEquivalent: String {
        if let special = Self.specialKeys[Int(keyCode)] {
            return special.keyEquivalent.map { String(Character(UnicodeScalar($0)!)) } ?? ""
        }
        return Self.character(for: keyCode)?.lowercased() ?? ""
    }

    /// Modifier symbols in the order macOS shows them.
    static func symbols(for modifiers: NSEvent.ModifierFlags) -> String {
        [(NSEvent.ModifierFlags.control, "⌃"), (.option, "⌥"), (.shift, "⇧"), (.command, "⌘")]
            .filter { modifiers.contains($0.0) }
            .map(\.1)
            .joined()
    }

    private enum CodingKeys: String, CodingKey {
        case keyCode
        case modifierBits = "modifiers"
    }

    // MARK: - Key names

    private static let functionKeys = [
        kVK_F1, kVK_F2, kVK_F3, kVK_F4, kVK_F5, kVK_F6, kVK_F7, kVK_F8, kVK_F9, kVK_F10,
        kVK_F11, kVK_F12, kVK_F13, kVK_F14, kVK_F15, kVK_F16, kVK_F17, kVK_F18, kVK_F19, kVK_F20,
    ]

    /// Keys that don't type a character, with their menu key equivalents.
    private static let specialKeys: [Int: (name: String, keyEquivalent: Int?)] = {
        var keys: [Int: (name: String, keyEquivalent: Int?)] = [
            kVK_Space: ("Space", 0x20),
            kVK_Return: ("↩", 0x0D),
            kVK_ANSI_KeypadEnter: ("⌤", 0x03),
            kVK_Tab: ("⇥", 0x09),
            kVK_Delete: ("⌫", 0x08),
            kVK_ForwardDelete: ("⌦", NSDeleteFunctionKey),
            kVK_Escape: ("⎋", 0x1B),
            kVK_LeftArrow: ("←", NSLeftArrowFunctionKey),
            kVK_RightArrow: ("→", NSRightArrowFunctionKey),
            kVK_UpArrow: ("↑", NSUpArrowFunctionKey),
            kVK_DownArrow: ("↓", NSDownArrowFunctionKey),
            kVK_Home: ("↖", NSHomeFunctionKey),
            kVK_End: ("↘", NSEndFunctionKey),
            kVK_PageUp: ("⇞", NSPageUpFunctionKey),
            kVK_PageDown: ("⇟", NSPageDownFunctionKey),
        ]
        for (index, key) in functionKeys.enumerated() {
            keys[key] = ("F\(index + 1)", NSF1FunctionKey + index)
        }
        return keys
    }()

    /// What the key types on the current keyboard layout, without modifiers.
    private static func character(for keyCode: UInt16) -> String? {
        guard let source = TISCopyCurrentASCIICapableKeyboardLayoutInputSource()?.takeRetainedValue(),
              let layout = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData)
        else { return nil }
        let data = Unmanaged<CFData>.fromOpaque(layout).takeUnretainedValue() as Data

        var deadKeyState: UInt32 = 0
        var characters = [UniChar](repeating: 0, count: 4)
        var length = 0
        let status = data.withUnsafeBytes { bytes in
            UCKeyTranslate(
                bytes.baseAddress!.assumingMemoryBound(to: UCKeyboardLayout.self),
                keyCode,
                UInt16(kUCKeyActionDisplay),
                0,
                UInt32(LMGetKbdType()),
                OptionBits(kUCKeyTranslateNoDeadKeysBit),
                &deadKeyState,
                characters.count,
                &length,
                &characters
            )
        }
        guard status == noErr, length > 0 else { return nil }
        let string = String(utf16CodeUnits: characters, count: length)
        return string.trimmingCharacters(in: .whitespacesAndNewlines.union(.controlCharacters)).isEmpty ? nil : string
    }
}
