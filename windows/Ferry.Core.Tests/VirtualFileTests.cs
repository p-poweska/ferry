using System.Buffers.Binary;
using System.Text;
using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class VirtualFileTests
{
    private const uint HasAttributes = 0x4;
    private const uint HasFileSize = 0x40;

    /// <summary>Builds a FILEGROUPDESCRIPTOR the way Outlook and browsers write one.</summary>
    private static byte[] Descriptor(bool unicode, params (string Name, uint Flags, uint Attributes, long Size)[] files)
    {
        var size = unicode ? 592 : 332;
        var data = new byte[4 + files.Length * size];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)files.Length);
        for (var index = 0; index < files.Length; index++)
        {
            var descriptor = data.AsSpan(4 + index * size, size);
            var (name, flags, attributes, length) = files[index];
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor, flags);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor[36..], attributes);
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor[64..], (uint)(length >> 32));
            BinaryPrimitives.WriteUInt32LittleEndian(descriptor[68..], (uint)length);
            var encoded = unicode ? Encoding.Unicode.GetBytes(name) : Encoding.Latin1.GetBytes(name);
            encoded.CopyTo(descriptor[72..]);
        }
        return data;
    }

    [Fact]
    public void ReadsOutlookAttachments()
    {
        var data = Descriptor(
            unicode: true,
            ("Invoice.pdf", HasFileSize, 0, 52_431),
            ("Zdjęcie z wakacji.jpg", HasFileSize, 0, 6_000_000_000));

        var files = FileGroupDescriptor.Parse(data);

        Assert.Equal(
            [new VirtualFile("Invoice.pdf", false, 52_431), new VirtualFile("Zdjęcie z wakacji.jpg", false, 6_000_000_000)],
            files);
    }

    [Fact]
    public void ReadsFoldersAndMissingSizes()
    {
        var data = Descriptor(
            unicode: true,
            ("Photos", HasAttributes, 0x10, 0),
            (@"Photos\beach.png", HasAttributes, 0x20, 0));

        var files = FileGroupDescriptor.Parse(data);

        Assert.Equal([new VirtualFile("Photos", true, null), new VirtualFile(@"Photos\beach.png", false, null)], files);
    }

    [Fact]
    public void ReadsTheOlderAnsiFormat()
    {
        var data = Descriptor(unicode: false, ("caf\u00e9.txt", HasFileSize, 0, 12));

        var files = FileGroupDescriptor.Parse(data, Encoding.Latin1);

        Assert.Equal([new VirtualFile("caf\u00e9.txt", false, 12)], files);
    }

    [Fact]
    public void StopsAtTheEndOfShortData()
    {
        var data = Descriptor(unicode: true, ("a.txt", 0, 0, 0), ("b.txt", 0, 0, 0));

        Assert.Single(FileGroupDescriptor.Parse(data.AsSpan(0, data.Length - 10)));
        Assert.Empty(FileGroupDescriptor.Parse([1, 0]));
    }

    [Theory]
    [InlineData("report.docx", "report.docx")]
    [InlineData(@"Photos\beach.png", @"Photos\beach.png")]
    [InlineData("Photos/beach.png", @"Photos\beach.png")]
    [InlineData(@"..\..\Windows\evil.dll", @"Windows\evil.dll")]
    [InlineData(@"C:\Users\x\file.txt", @"C_\Users\x\file.txt")]
    [InlineData("Re: Lunch?.msg", "Re_ Lunch_.msg")]
    [InlineData("trailing dots...", "trailing dots")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("...", "Untitled")]
    [InlineData("", "Untitled")]
    public void KeepsSuggestedNamesInsideTheInbox(string suggested, string expected)
    {
        Assert.Equal(expected, InboxPaths.Sanitize(suggested));
    }

    [Fact]
    public void FindsTheTopLevelItems()
    {
        var paths = new[] { "a.txt", @"Folder\b.txt", @"Folder\Sub\c.txt", "folder", "d.txt" };

        Assert.Equal(["a.txt", "Folder", "d.txt"], InboxPaths.TopLevel(paths));
    }

    [Fact]
    public void NumbersNamesThatAreTaken()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal("image001.png", InboxPaths.Unique("image001.png", taken));
        Assert.Equal("image001 (2).png", InboxPaths.Unique("image001.png", taken));
        Assert.Equal("image001 (3).png", InboxPaths.Unique("image001.png", taken));
        Assert.Equal("Image001 (4).png", InboxPaths.Unique("Image001.png", taken));
        Assert.Equal(@"Photos\beach.png", InboxPaths.Unique(@"Photos\beach.png", taken));
        Assert.Equal(@"Photos\beach (2).png", InboxPaths.Unique(@"Photos\beach.png", taken));
        Assert.Equal("README (2)", InboxPaths.Unique("README", new HashSet<string> { "README" }));
    }
}
