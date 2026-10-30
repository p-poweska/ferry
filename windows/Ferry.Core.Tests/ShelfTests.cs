using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class ShelfTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("FerryTests").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string MakeFile(string name, int bytes = 3)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public void AddsFilesInOrderWithoutDuplicates()
    {
        var shelf = new Shelf();
        var a = MakeFile("a.txt");
        var b = MakeFile("b.txt");

        shelf.Add([a, b]);
        shelf.Add([Path.Combine(folder, "B.TXT"), a]);

        Assert.Equal([a, b], shelf.Paths);
    }

    [Fact]
    public void ReadsFileMetadata()
    {
        var shelf = new Shelf();
        var file = MakeFile("photo.png", bytes: 2048);
        var subfolder = Directory.CreateDirectory(Path.Combine(folder, "Holiday")).FullName;

        shelf.Add([file, subfolder + Path.DirectorySeparatorChar]);

        var (photo, holiday) = (shelf.Items[0], shelf.Items[1]);
        Assert.Equal("photo.png", photo.Name);
        Assert.Equal("PNG File", photo.Kind);
        Assert.Equal(2048, photo.ByteCount);
        Assert.False(photo.IsFolder);
        Assert.Equal("Holiday", holiday.Name);
        Assert.Equal(subfolder, holiday.Path);
        Assert.True(holiday.IsFolder);
    }

    [Fact]
    public void NotifiesOnlyOnRealChanges()
    {
        var shelf = new Shelf();
        var changes = 0;
        shelf.Changed += () => changes++;
        var file = MakeFile("a.txt");

        shelf.Add([file]);
        shelf.Add([file]);
        shelf.Remove([Path.Combine(folder, "missing.txt")]);
        shelf.Remove([file]);
        shelf.RemoveAll();

        Assert.Equal(2, changes);
    }

    [Fact]
    public void PrunesFilesThatNoLongerExist()
    {
        var shelf = new Shelf();
        var kept = MakeFile("kept.txt");
        var moved = MakeFile("moved.txt");
        shelf.Add([kept, moved]);

        File.Delete(moved);
        shelf.PruneMissing();

        Assert.Equal([kept], shelf.Paths);
    }

    [Fact]
    public void DescribesItemsInWords()
    {
        var png = new ShelfItem(@"C:\a.png", "a.png", "PNG File", 180 * 1024, false);
        var folderItem = new ShelfItem(@"C:\b", "b", "File folder", null, true);
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal("1 item", ItemText.Count(1));
        Assert.Equal("3 items", ItemText.Count(3));
        Assert.Equal("File folder", ItemText.Detail(folderItem));
        Assert.Equal("Ready to move", ItemText.Summary([]));
        Assert.Equal("512 bytes", ItemText.FormatBytes(512, culture));
        Assert.Equal("1 KB", ItemText.FormatBytes(1024, culture));
        Assert.Equal("1.46 KB", ItemText.FormatBytes(1500, culture));
        Assert.Equal("180 KB", ItemText.FormatBytes(180 * 1024, culture));
        Assert.Equal("3.4 MB", ItemText.FormatBytes((long)(3.4 * 1024 * 1024), culture));
        Assert.Equal("1 MB", ItemText.FormatBytes(1_048_000, culture));
        Assert.StartsWith("PNG File · 180", ItemText.Detail(png));
        Assert.EndsWith(" · 1 folder", ItemText.Summary([png, folderItem]));
    }
}
