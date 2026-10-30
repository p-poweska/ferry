using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class InboxTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("FerryTests").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static bool Write(int index, string path, long? byteCount)
    {
        File.WriteAllText(path, $"file {index}");
        return true;
    }

    [Fact]
    public void WritesEachDropIntoAFolderOfItsOwn()
    {
        var inbox = new Inbox(Path.Combine(folder, "Ferry Inbox"));

        var first = inbox.Receive([new VirtualFile("Invoice.pdf", false, 6)], Write);
        var second = inbox.Receive([new VirtualFile("Invoice.pdf", false, 6)], Write);

        Assert.Equal("file 0", File.ReadAllText(Assert.Single(first)));
        Assert.Equal("Invoice.pdf", Path.GetFileName(Assert.Single(second)));
        Assert.NotEqual(first[0], second[0]);
    }

    [Fact]
    public void KeepsOnlyTopLevelItemsOnTheShelf()
    {
        var inbox = new Inbox(folder);
        VirtualFile[] files =
        [
            new("Photos", true, null),
            new(@"Photos\beach.png", false, null),
            new("notes.txt", false, null),
        ];

        var received = inbox.Receive(files, Write);

        Assert.Equal(["Photos", "notes.txt"], received.Select(Path.GetFileName));
        Assert.Equal("file 1", File.ReadAllText(Path.Combine(received[0], "beach.png")));
    }

    [Fact]
    public void NumbersAttachmentsWithTheSameName()
    {
        var received = new Inbox(folder).Receive(
            [new VirtualFile("image001.png", false, null), new VirtualFile("image001.png", false, null)],
            Write);

        Assert.Equal(["image001.png", "image001 (2).png"], received.Select(Path.GetFileName));
    }

    [Fact]
    public void LeavesOutFilesThatCouldNotBeWritten()
    {
        var received = new Inbox(folder).Receive(
            [new VirtualFile("broken.bin", false, null), new VirtualFile("fine.txt", false, null)],
            (index, path, _) => index == 1 && Write(index, path, null));

        Assert.Equal(["fine.txt"], received.Select(Path.GetFileName));
    }

    [Fact]
    public void EmptiesWhatTheLastRunLeft()
    {
        var inbox = new Inbox(Path.Combine(folder, "Ferry Inbox"));
        var received = inbox.Receive([new VirtualFile("old.txt", false, null)], Write);

        inbox.Empty();
        inbox.Empty();

        Assert.False(File.Exists(received[0]));
    }
}
