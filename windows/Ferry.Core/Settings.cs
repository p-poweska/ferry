using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ferry.Core;

/// <summary>The user's settings, saved as JSON after every change.</summary>
public sealed class Settings
{
    /// <summary>Settings that something outside the settings window has to act on.</summary>
    public enum Setting
    {
        ShowWhileDragging,
        Position,
        Shortcut,
    }

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string? path;
    private bool isLoading;

    /// <summary>Settings kept only in memory, starting from the defaults.</summary>
    public Settings() { }

    private Settings(string path)
    {
        this.path = path;
    }

    /// <summary>Raised after a setting changes to a new value.</summary>
    public event Action<Setting>? Changed;

    /// <summary>Pop the shelf up whenever files are being dragged in any app.</summary>
    public bool ShowWhileDragging
    {
        get;
        set => Set(ref field, value, Setting.ShowWhileDragging);
    } = true;

    public ShelfPosition Position
    {
        get;
        set => Set(ref field, value, Setting.Position);
    } = ShelfPosition.RightEdge;

    /// <summary>Where the user last dragged the shelf to, for <see cref="ShelfPosition.Custom"/>.</summary>
    public ScreenPoint? CustomOrigin
    {
        get;
        set
        {
            field = value;
            Save();
        }
    }

    /// <summary>Shows or hides the shelf from any app; <c>null</c> when turned off.</summary>
    public KeyShortcut? Shortcut
    {
        get;
        set => Set(ref field, value, Setting.Shortcut);
    } = KeyShortcut.Standard;

    /// <summary>
    /// Reads the settings saved at <paramref name="path"/>. Anything missing or
    /// unreadable keeps its default.
    /// </summary>
    public static Settings Load(string path)
    {
        var settings = new Settings(path) { isLoading = true };
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject json)
            {
                settings.Read(json);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Start over with the defaults rather than not starting at all.
        }
        settings.isLoading = false;
        return settings;
    }

    private void Read(JsonObject json)
    {
        if (json["ShowWhileDragging"] is JsonValue show && show.TryGetValue(out bool isOn))
        {
            ShowWhileDragging = isOn;
        }
        if (json["Position"] is JsonValue position && position.TryGetValue(out string? name)
            && Enum.TryParse(name, ignoreCase: true, out ShelfPosition parsed) && Enum.IsDefined(parsed))
        {
            Position = parsed;
        }
        if (json["CustomOrigin"] is JsonObject origin
            && origin["X"] is JsonValue x && x.TryGetValue(out double originX)
            && origin["Y"] is JsonValue y && y.TryGetValue(out double originY))
        {
            CustomOrigin = new ScreenPoint(originX, originY);
        }
        // Saved as null when turned off, so "off" isn't mistaken for "never set".
        if (json.TryGetPropertyValue("Shortcut", out var shortcut))
        {
            Shortcut = shortcut is JsonObject keys
                && keys["Key"] is JsonValue key && key.TryGetValue(out int code)
                && keys["Modifiers"] is JsonValue modifiers && modifiers.TryGetValue(out int flags)
                ? new KeyShortcut(code, (KeyModifiers)flags)
                : null;
        }
    }

    private void Set<T>(ref T field, T value, Setting setting)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        Save();
        Changed?.Invoke(setting);
    }

    private void Save()
    {
        if (path is null || isLoading)
        {
            return;
        }
        var json = new JsonObject
        {
            ["ShowWhileDragging"] = ShowWhileDragging,
            ["Position"] = Position.ToString(),
            ["CustomOrigin"] = CustomOrigin is { } origin ? new JsonObject { ["X"] = origin.X, ["Y"] = origin.Y } : null,
            ["Shortcut"] = Shortcut is { } shortcut
                ? new JsonObject { ["Key"] = shortcut.Key, ["Modifiers"] = (int)shortcut.Modifiers }
                : null,
        };
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json.ToJsonString(WriteOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The setting still applies until Ferry quits.
        }
    }
}
