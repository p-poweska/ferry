import Carbon.HIToolbox
import Observation

/// A system-wide keyboard shortcut. Carbon hot keys work in every app
/// without Accessibility access, unlike global key monitors.
@MainActor
@Observable
final class GlobalHotKey {
    @ObservationIgnored var onPress: () -> Void = {}

    /// Set when the shortcut couldn't be claimed, usually because another
    /// app already uses it.
    private(set) var isTaken = false

    /// Steps aside while a new shortcut is being recorded, so pressing the
    /// current one records it instead of toggling the shelf.
    var isPaused = false {
        didSet { update() }
    }

    @ObservationIgnored private var shortcut: KeyShortcut?
    @ObservationIgnored private var hotKey: EventHotKeyRef?
    @ObservationIgnored private var handler: EventHandlerRef?

    func register(_ shortcut: KeyShortcut?) {
        self.shortcut = shortcut
        update()
    }

    private func update() {
        if let hotKey {
            UnregisterEventHotKey(hotKey)
            self.hotKey = nil
        }
        guard let shortcut, !isPaused else {
            isTaken = false
            return
        }
        installHandler()
        let id = EventHotKeyID(signature: 0x4652_5259 /* FRRY */, id: 1)
        let status = RegisterEventHotKey(
            UInt32(shortcut.keyCode),
            shortcut.carbonModifiers,
            id,
            GetApplicationEventTarget(),
            0,
            &hotKey
        )
        isTaken = status != noErr
    }

    private func installHandler() {
        guard handler == nil else { return }
        var pressed = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(
            GetApplicationEventTarget(),
            { _, _, context in
                guard let context else { return OSStatus(eventNotHandledErr) }
                let hotKey = Unmanaged<GlobalHotKey>.fromOpaque(context).takeUnretainedValue()
                MainActor.assumeIsolated { hotKey.onPress() }
                return noErr
            },
            1,
            &pressed,
            Unmanaged.passUnretained(self).toOpaque(),
            &handler
        )
    }

    isolated deinit {
        if let hotKey { UnregisterEventHotKey(hotKey) }
        if let handler { RemoveEventHandler(handler) }
    }
}
