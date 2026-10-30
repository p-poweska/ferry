import Carbon.HIToolbox
import SwiftUI

/// Click it, then press the new shortcut. Esc cancels and Delete turns the
/// shortcut off.
struct ShortcutRecorder: View {
    @Binding var shortcut: KeyShortcut?
    /// Called with `true` when recording starts and `false` when it ends.
    var onRecordingChange: (Bool) -> Void = { _ in }

    @State private var isRecording = false
    @State private var heldModifiers: NSEvent.ModifierFlags = []
    @State private var monitor: KeyMonitor?
    @State private var isHovering = false

    var body: some View {
        let shape = RoundedRectangle(cornerRadius: 6, style: .continuous)
        Button(action: toggleRecording) {
            Text(title)
                .font(.system(size: 12, weight: shortcut != nil && !isRecording ? .medium : .regular))
                .foregroundStyle(shortcut != nil && !isRecording ? .primary : .secondary)
                .lineLimit(1)
                .frame(minWidth: 112)
                .padding(.leading, 8)
                .padding(.trailing, shortcut != nil && !isRecording ? 24 : 8)
                .frame(height: 24)
                .background(shape.fill(isRecording ? Color.accentColor.opacity(0.12) : Color.primary.opacity(isHovering ? 0.09 : 0.05)))
                .overlay(shape.strokeBorder(isRecording ? Color.accentColor : Color.primary.opacity(0.15)))
                .contentShape(shape)
        }
        .buttonStyle(.plain)
        .overlay(alignment: .trailing) {
            if shortcut != nil && !isRecording {
                Button {
                    shortcut = nil
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundStyle(.tertiary)
                        .padding(.trailing, 6)
                }
                .buttonStyle(.plain)
                .help("Turn Off Shortcut")
            }
        }
        .onHover { isHovering = $0 }
        .help(isRecording ? "Press a shortcut, or Esc to cancel" : "Click to record a new shortcut")
        .onDisappear(perform: stopRecording)
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didResignKeyNotification)) { _ in
            stopRecording()
        }
    }

    private var title: String {
        if isRecording {
            return heldModifiers.isEmpty ? "Type Shortcut" : KeyShortcut.symbols(for: heldModifiers)
        }
        return shortcut?.displayString ?? "Record Shortcut"
    }

    private func toggleRecording() {
        isRecording ? stopRecording() : startRecording()
    }

    private func startRecording() {
        isRecording = true
        heldModifiers = []
        monitor = KeyMonitor(handler: handle)
        onRecordingChange(true)
    }

    private func stopRecording() {
        guard isRecording else { return }
        isRecording = false
        monitor = nil
        onRecordingChange(false)
    }

    private func handle(_ event: NSEvent) -> NSEvent? {
        if event.type == .flagsChanged {
            heldModifiers = event.modifierFlags.intersection(KeyShortcut.supportedModifiers)
            return event
        }
        let pressed = KeyShortcut(event: event)
        switch (Int(event.keyCode), pressed.modifiers.isEmpty) {
        case (kVK_Escape, true):
            stopRecording()
        case (kVK_Delete, true), (kVK_ForwardDelete, true):
            shortcut = nil
            stopRecording()
        default:
            guard pressed.isValid else {
                NSSound.beep()
                break
            }
            shortcut = pressed
            stopRecording()
        }
        return nil
    }
}

/// Watches key presses in Ferry's own windows until it's released.
@MainActor
private final class KeyMonitor {
    private var token: Any?

    init(handler: @escaping (NSEvent) -> NSEvent?) {
        // Local monitors run on the main thread.
        token = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .flagsChanged], handler: handler)
    }

    isolated deinit {
        if let token {
            NSEvent.removeMonitor(token)
        }
    }
}
