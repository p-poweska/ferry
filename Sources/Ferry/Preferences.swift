import Foundation

enum Preferences {
    private static let showWhileDraggingKey = "ShowWhileDragging"

    /// Pop the shelf up whenever files are being dragged in any app.
    static var showWhileDragging: Bool {
        get { UserDefaults.standard.object(forKey: showWhileDraggingKey) as? Bool ?? true }
        set { UserDefaults.standard.set(newValue, forKey: showWhileDraggingKey) }
    }
}
