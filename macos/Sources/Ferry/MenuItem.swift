import AppKit

/// An `NSMenuItem` that runs a closure instead of going through target/action.
final class MenuItem: NSMenuItem {
    private let handler: () -> Void

    init(
        _ title: String,
        symbol: String? = nil,
        key: String = "",
        isOn: Bool = false,
        isEnabled: Bool = true,
        handler: @escaping () -> Void
    ) {
        self.handler = handler
        super.init(title: title, action: #selector(runHandler), keyEquivalent: key)
        target = self
        state = isOn ? .on : .off
        self.isEnabled = isEnabled
        if let symbol {
            image = NSImage(systemSymbolName: symbol, accessibilityDescription: nil)
        }
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    @objc private func runHandler() {
        handler()
    }
}
