using Ferry.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>
/// A button that shows the shortcut and records a new one: click it, then
/// press the keys. Esc cancels, Backspace turns the shortcut off.
/// </summary>
internal sealed class ShortcutRecorder
{
    private const int Escape = 0x1B;
    private const int Backspace = 0x08;
    private const int Delete = 0x2E;

    private readonly Settings settings;
    private bool isRecording;
    private string? hint;

    public ShortcutRecorder(Settings settings)
    {
        this.settings = settings;
        View = new Button { MinWidth = 150 };
        View.Click += (_, _) => SetRecording(!isRecording);
        View.PreviewKeyDown += OnKeyDown;
        View.LostFocus += (_, _) => SetRecording(false);
        Update();
    }

    public Button View { get; }

    /// <summary>Recording started or stopped; the global shortcut should step aside meanwhile.</summary>
    public event Action<bool>? RecordingChanged;

    public void Update()
    {
        View.Content = isRecording
            ? hint ?? "Type a shortcut…"
            : settings.Shortcut?.ToDisplayString(KeyNames.Name) ?? "Off";
    }

    private void SetRecording(bool recording)
    {
        if (recording == isRecording)
        {
            return;
        }
        isRecording = recording;
        hint = null;
        Update();
        RecordingChanged?.Invoke(recording);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!isRecording)
        {
            return;
        }
        e.Handled = true;
        var key = (int)e.Key;
        var modifiers = PressedModifiers();
        if (KeyShortcut.IsModifierKey(key))
        {
            hint = modifiers == KeyModifiers.None ? null : new KeyShortcut(0, modifiers).ToDisplayString(_ => "…");
            Update();
            return;
        }
        if (modifiers == KeyModifiers.None && key == Escape)
        {
            SetRecording(false);
            return;
        }
        if (modifiers == KeyModifiers.None && key is Backspace or Delete)
        {
            settings.Shortcut = null;
            SetRecording(false);
            return;
        }
        var shortcut = new KeyShortcut(key, modifiers);
        if (!shortcut.IsValid)
        {
            hint = "Add Ctrl or Alt";
            Update();
            return;
        }
        settings.Shortcut = shortcut;
        SetRecording(false);
    }

    private static KeyModifiers PressedModifiers()
    {
        static bool IsDown(int key) => GetKeyState(key) < 0;
        var modifiers = KeyModifiers.None;
        if (IsDown(0x11)) modifiers |= KeyModifiers.Control;
        if (IsDown(0x12)) modifiers |= KeyModifiers.Alt;
        if (IsDown(0x10)) modifiers |= KeyModifiers.Shift;
        if (IsDown(0x5B) || IsDown(0x5C)) modifiers |= KeyModifiers.Windows;
        return modifiers;
    }
}
