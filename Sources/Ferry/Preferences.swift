import Foundation
import Observation

/// The user's settings, saved in `UserDefaults`.
@MainActor
@Observable
final class Preferences {
    /// Settings that something outside the settings window has to act on.
    enum Setting {
        case showWhileDragging
    }

    private enum Key {
        static let showWhileDragging = "ShowWhileDragging"
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

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        showWhileDragging = defaults.object(forKey: Key.showWhileDragging) as? Bool ?? true
    }

    private func notify(_ setting: Setting, if changed: Bool) {
        if changed {
            onChange?(setting)
        }
    }
}
