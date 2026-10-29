import AppKit
import Observation

/// The user's settings, saved in `UserDefaults`.
@MainActor
@Observable
final class Preferences {
    /// Settings that something outside the settings window has to act on.
    enum Setting {
        case showWhileDragging
        case position
    }

    private enum Key {
        static let showWhileDragging = "ShowWhileDragging"
        static let position = "ShelfPosition"
        static let customOrigin = "ShelfOrigin"
    }

    /// Called after a setting changes to a new value.
    @ObservationIgnored var onChange: ((Setting) -> Void)?
    @ObservationIgnored private let defaults: UserDefaults

    /// Pop the shelf up whenever files are being dragged in any app.
    var showWhileDragging: Bool {
        didSet {
            defaults.set(showWhileDragging, forKey: Key.showWhileDragging)
            notify(.showWhileDragging, if: showWhileDragging != oldValue)
        }
    }

    var position: ShelfPosition {
        didSet {
            defaults.set(position.rawValue, forKey: Key.position)
            notify(.position, if: position != oldValue)
        }
    }

    /// Where the user last dragged the shelf to, for `ShelfPosition.custom`.
    var customOrigin: NSPoint? {
        didSet {
            defaults.set(customOrigin.map(NSStringFromPoint), forKey: Key.customOrigin)
        }
    }

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        showWhileDragging = defaults.object(forKey: Key.showWhileDragging) as? Bool ?? true
        let customOrigin = defaults.string(forKey: Key.customOrigin).map(NSPointFromString)
        self.customOrigin = customOrigin
        // Before there was a choice, a dragged shelf simply stayed put.
        position = defaults.string(forKey: Key.position).flatMap(ShelfPosition.init)
            ?? (customOrigin == nil ? .rightEdge : .custom)
    }

    private func notify(_ setting: Setting, if changed: Bool) {
        if changed {
            onChange?(setting)
        }
    }
}
