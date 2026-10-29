import SwiftUI

struct SettingsView: View {
    @Bindable var preferences: Preferences
    let hotKey: GlobalHotKey

    @State private var launchAtLogin = LaunchAtLogin.isEnabled

    var body: some View {
        Form {
            Section("Shelf") {
                Toggle(isOn: $preferences.showWhileDragging) {
                    Text("Show while dragging files")
                    Text("Slides in as soon as you start dragging files in any app.")
                }
                LabeledContent {
                    ShortcutRecorder(shortcut: $preferences.shortcut) { isRecording in
                        hotKey.isPaused = isRecording
                    }
                } label: {
                    Text("Keyboard shortcut")
                    if hotKey.isTaken {
                        Text("Another app is already using this shortcut.")
                            .foregroundStyle(.red)
                    } else {
                        Text("Shows or hides the shelf from any app.")
                    }
                }
                VStack(alignment: .leading, spacing: 12) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Position")
                        Text("Where the shelf slides in. Drag it by its edge to put it anywhere else.")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                    PositionPicker(selection: $preferences.position, customSpot: customSpot)
                }
            }
            Section {
                Toggle("Launch at login", isOn: launchAtLoginBinding)
            }
        }
        .formStyle(.grouped)
        .frame(width: 440)
        .fixedSize()
    }

    /// Where the user's own spot is, as a fraction of the room on its screen
    /// (0 is the left or bottom edge).
    private var customSpot: CGPoint? {
        guard let origin = preferences.customOrigin else { return nil }
        let size = ShelfView.size
        let shelf = NSRect(origin: origin, size: size)
        guard let area = NSScreen.screens.first(where: { $0.visibleFrame.intersects(shelf) })?.visibleFrame else {
            return nil
        }
        return CGPoint(
            x: ((origin.x - area.minX) / max(area.width - size.width, 1)).clamped(),
            y: ((origin.y - area.minY) / max(area.height - size.height, 1)).clamped()
        )
    }

    /// The login item lives in System Settings, so read it back after every change.
    private var launchAtLoginBinding: Binding<Bool> {
        Binding {
            launchAtLogin
        } set: { isOn in
            if isOn != LaunchAtLogin.isEnabled {
                LaunchAtLogin.toggle()
            }
            launchAtLogin = LaunchAtLogin.isEnabled
        }
    }
}

/// A row of tiny screens showing where the shelf slides in.
private struct PositionPicker: View {
    @Binding var selection: ShelfPosition
    let customSpot: CGPoint?

    var body: some View {
        HStack(spacing: 10) {
            ForEach(ShelfPosition.allCases.filter { $0 != .custom || customSpot != nil }) { position in
                Button {
                    selection = position
                } label: {
                    VStack(spacing: 5) {
                        MiniScreen(position: position, isSelected: selection == position, customSpot: customSpot)
                            .frame(height: 38)
                        Text(position.title)
                            .font(.caption)
                            .foregroundStyle(selection == position ? .primary : .secondary)
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(position.title)
                .accessibilityAddTraits(selection == position ? .isSelected : [])
            }
        }
        .animation(.easeOut(duration: 0.15), value: selection)
    }
}

private struct MiniScreen: View {
    let position: ShelfPosition
    let isSelected: Bool
    let customSpot: CGPoint?

    private static let shelf = CGSize(width: 9, height: 11)
    private static let menuBar: CGFloat = 4
    private static let inset: CGFloat = 3

    @State private var isHovering = false

    var body: some View {
        let screen = RoundedRectangle(cornerRadius: 5, style: .continuous)
        GeometryReader { proxy in
            let center = shelfCenter(in: proxy.size)
            ZStack(alignment: .topLeading) {
                Rectangle()
                    .fill(Color.primary.opacity(0.1))
                    .frame(height: Self.menuBar)
                if position == .pointer {
                    Image(systemName: "cursorarrow")
                        .font(.system(size: 9))
                        .foregroundStyle(.secondary)
                        .position(x: center.x - 11, y: center.y + 2)
                }
                RoundedRectangle(cornerRadius: 2.5, style: .continuous)
                    .fill(isSelected ? Color.accentColor : Color.secondary.opacity(0.55))
                    .frame(width: Self.shelf.width, height: Self.shelf.height)
                    .position(center)
            }
        }
        .background(screen.fill(isSelected ? Color.accentColor.opacity(0.12) : Color.primary.opacity(isHovering ? 0.09 : 0.05)))
        .clipShape(screen)
        .overlay(
            screen.strokeBorder(
                isSelected ? Color.accentColor : Color.primary.opacity(0.15),
                lineWidth: isSelected ? 2 : 1
            )
        )
        .onHover { isHovering = $0 }
    }

    private func shelfCenter(in size: CGSize) -> CGPoint {
        let shelf = Self.shelf
        let inset = Self.inset
        let top = Self.menuBar + inset + shelf.height / 2
        let bottom = size.height - inset - shelf.height / 2
        let left = inset + shelf.width / 2
        let right = size.width - inset - shelf.width / 2
        switch position {
        case .rightEdge: return CGPoint(x: right, y: (top + bottom) / 2)
        case .leftEdge: return CGPoint(x: left, y: (top + bottom) / 2)
        case .topEdge: return CGPoint(x: size.width / 2, y: top)
        case .bottomEdge: return CGPoint(x: size.width / 2, y: bottom)
        case .pointer: return CGPoint(x: size.width / 2 + 6, y: (top + bottom) / 2)
        case .custom:
            let spot = customSpot ?? CGPoint(x: 1, y: 0.5)
            // Screen coordinates grow upwards, SwiftUI's downwards.
            return CGPoint(x: left + spot.x * (right - left), y: bottom - spot.y * (bottom - top))
        }
    }
}

private extension CGFloat {
    func clamped() -> CGFloat {
        Swift.min(Swift.max(self, 0), 1)
    }
}
