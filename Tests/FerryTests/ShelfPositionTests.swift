import AppKit
import Testing
@testable import Ferry

struct ShelfPositionTests {
    private let size = NSSize(width: 200, height: 200)
    /// A 1000×800 screen with a 30 pt menu bar, and a second one to its right.
    private let main = ShelfPosition.Screen(
        frame: NSRect(x: 0, y: 0, width: 1000, height: 800),
        visibleFrame: NSRect(x: 0, y: 0, width: 1000, height: 770)
    )
    private let side = ShelfPosition.Screen(
        frame: NSRect(x: 1000, y: 0, width: 800, height: 600),
        visibleFrame: NSRect(x: 1000, y: 0, width: 800, height: 600)
    )

    private func frame(_ position: ShelfPosition, mouse: NSPoint = NSPoint(x: 500, y: 400), customOrigin: NSPoint? = nil) -> NSRect {
        position.frame(size: size, mouse: mouse, customOrigin: customOrigin, screens: [main, side])
    }

    @Test func sitsAtTheChosenEdgeOfTheScreenWithThePointer() {
        #expect(frame(.rightEdge).origin == NSPoint(x: 784, y: 285))
        #expect(frame(.leftEdge).origin == NSPoint(x: 16, y: 285))
        #expect(frame(.topEdge).origin == NSPoint(x: 400, y: 554))
        #expect(frame(.bottomEdge).origin == NSPoint(x: 400, y: 16))
        #expect(frame(.rightEdge, mouse: NSPoint(x: 1200, y: 100)).origin == NSPoint(x: 1584, y: 200))
    }

    @Test func appearsBesideThePointer() {
        #expect(frame(.pointer).origin == NSPoint(x: 540, y: 300))
        // Near the right edge it flips to the left of the pointer.
        #expect(frame(.pointer, mouse: NSPoint(x: 900, y: 400)).origin == NSPoint(x: 660, y: 300))
        // And it never pokes out above or below the screen.
        #expect(frame(.pointer, mouse: NSPoint(x: 500, y: 790)).maxY == 754)
    }

    @Test func usesTheCustomSpotWhileItFitsOnAScreen() {
        let spot = NSPoint(x: 1300, y: 100)
        #expect(frame(.custom, customOrigin: spot).origin == spot)
        // The screen it was on is gone: fall back to the right edge.
        #expect(frame(.custom, customOrigin: NSPoint(x: 3000, y: 100)) == frame(.rightEdge))
        #expect(frame(.custom) == frame(.rightEdge))
    }
}

@MainActor
struct PreferencesTests {
    private let suite = "FerryTests-\(UUID().uuidString)"
    private var defaults: UserDefaults { UserDefaults(suiteName: suite)! }

    @Test func remembersSettingsAcrossLaunches() {
        let preferences = Preferences(defaults: defaults)
        #expect(preferences.showWhileDragging)
        #expect(preferences.position == .rightEdge)

        preferences.showWhileDragging = false
        preferences.position = .pointer
        preferences.customOrigin = NSPoint(x: 10, y: 20)

        let relaunched = Preferences(defaults: defaults)
        #expect(!relaunched.showWhileDragging)
        #expect(relaunched.position == .pointer)
        #expect(relaunched.customOrigin == NSPoint(x: 10, y: 20))
        defaults.removePersistentDomain(forName: suite)
    }

    @Test func keepsAShelfThatWasDraggedBeforePositionsExisted() {
        defaults.set(NSStringFromPoint(NSPoint(x: 10, y: 20)), forKey: "ShelfOrigin")
        #expect(Preferences(defaults: defaults).position == .custom)
        defaults.removePersistentDomain(forName: suite)
    }

    @Test func reportsOnlyRealChanges() {
        let preferences = Preferences(defaults: defaults)
        var changes: [Preferences.Setting] = []
        preferences.onChange = { changes.append($0) }

        preferences.position = .rightEdge
        preferences.position = .leftEdge
        preferences.showWhileDragging = true
        preferences.showWhileDragging = false

        #expect(changes == [.position, .showWhileDragging])
        defaults.removePersistentDomain(forName: suite)
    }
}
