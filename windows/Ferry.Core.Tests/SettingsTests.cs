using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("FerryTests").FullName;
    private string SettingsFile => Path.Combine(folder, "Ferry", "settings.json");

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void StartsWithTheDefaults()
    {
        var settings = Settings.Load(SettingsFile);

        Assert.True(settings.ShowWhileDragging);
        Assert.Equal(ShelfPosition.RightEdge, settings.Position);
        Assert.Null(settings.CustomOrigin);
        Assert.Equal(KeyShortcut.Standard, settings.Shortcut);
    }

    [Fact]
    public void RemembersEverySetting()
    {
        var settings = Settings.Load(SettingsFile);
        settings.ShowWhileDragging = false;
        settings.Position = ShelfPosition.Custom;
        settings.CustomOrigin = new ScreenPoint(120, 340.5);
        settings.Shortcut = new KeyShortcut('D', KeyModifiers.Control | KeyModifiers.Shift);

        var reloaded = Settings.Load(SettingsFile);

        Assert.False(reloaded.ShowWhileDragging);
        Assert.Equal(ShelfPosition.Custom, reloaded.Position);
        Assert.Equal(new ScreenPoint(120, 340.5), reloaded.CustomOrigin);
        Assert.Equal(new KeyShortcut('D', KeyModifiers.Control | KeyModifiers.Shift), reloaded.Shortcut);
    }

    [Fact]
    public void RemembersATurnedOffShortcut()
    {
        Settings.Load(SettingsFile).Shortcut = null;

        Assert.Null(Settings.Load(SettingsFile).Shortcut);
    }

    [Fact]
    public void ReportsOnlyRealChanges()
    {
        var settings = Settings.Load(SettingsFile);
        var changes = new List<Settings.Setting>();
        settings.Changed += changes.Add;

        settings.ShowWhileDragging = true;
        settings.Position = ShelfPosition.LeftEdge;
        settings.Position = ShelfPosition.LeftEdge;
        settings.Shortcut = KeyShortcut.Standard;
        settings.Shortcut = null;

        Assert.Equal([Settings.Setting.Position, Settings.Setting.Shortcut], changes);
    }

    [Fact]
    public void IgnoresWhatItCannotRead()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, """{ "Position": "Diagonal", "ShowWhileDragging": "yes" }""");

        var settings = Settings.Load(SettingsFile);

        Assert.Equal(ShelfPosition.RightEdge, settings.Position);
        Assert.True(settings.ShowWhileDragging);
        Assert.Equal(KeyShortcut.Standard, settings.Shortcut);
    }

    [Fact]
    public void SurvivesABrokenFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, "{ not json");

        Assert.Equal(ShelfPosition.RightEdge, Settings.Load(SettingsFile).Position);
    }
}
