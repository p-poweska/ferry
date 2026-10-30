import AppKit

@main
@MainActor
enum FerryApp {
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        // Menu bar only: no Dock icon, no app menu. Also set via LSUIElement
        // in Info.plist, but this keeps `swift run` behaving the same way.
        app.setActivationPolicy(.accessory)
        withExtendedLifetime(delegate) {
            app.run()
        }
    }
}
