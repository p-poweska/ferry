namespace Ferry.Core;

/// <summary>Modifier keys, with the values <c>RegisterHotKey</c> expects.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// A key combination. The key is a Windows virtual-key code, so the shortcut
/// is named after what the key types on the current layout.
/// </summary>
public readonly record struct KeyShortcut(int Key, KeyModifiers Modifiers)
{
    /// <summary>Ctrl+Alt+F, for Ferry.</summary>
    public static readonly KeyShortcut Standard = new('F', KeyModifiers.Control | KeyModifiers.Alt);

    private const int F1 = 0x70;
    private const int F24 = 0x87;

    /// <summary>
    /// Needs Ctrl, Alt or the Windows key so it doesn't swallow ordinary
    /// typing; function keys can go on their own.
    /// </summary>
    public bool IsValid =>
        !IsModifierKey(Key) &&
        ((Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Windows)) != 0 || Key is >= F1 and <= F24);

    /// <summary>Shift, Ctrl, Alt and the Windows keys, on either side.</summary>
    public static bool IsModifierKey(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    /// <summary>
    /// The way Windows writes it, like "Ctrl+Alt+F". <paramref name="keyName"/>
    /// names keys this doesn't know, such as punctuation on the current layout.
    /// </summary>
    public string ToDisplayString(Func<int, string?>? keyName = null)
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Win");
        if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(KnownName(Key) ?? keyName?.Invoke(Key) ?? $"Key {Key}");
        return string.Join("+", parts);
    }

    public override string ToString() => ToDisplayString();

    private static string? KnownName(int key) => key switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)key).ToString(),
        >= F1 and <= F24 => $"F{key - F1 + 1}",
        >= 0x60 and <= 0x69 => $"Num {key - 0x60}",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        _ => null,
    };
}
