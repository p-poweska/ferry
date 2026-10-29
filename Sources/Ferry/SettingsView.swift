import SwiftUI

struct SettingsView: View {
    @Bindable var preferences: Preferences

    @State private var launchAtLogin = LaunchAtLogin.isEnabled

    var body: some View {
        Form {
            Section("Shelf") {
                Toggle(isOn: $preferences.showWhileDragging) {
                    Text("Show while dragging files")
                    Text("Slides in as soon as you start dragging files in any app.")
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
