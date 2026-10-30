using System.Text;
using Ferry.Core;
using Ferry.Interop;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>
/// Reads files out of a drag: real ones from File Explorer, and virtual ones
/// like Outlook attachments or browser images, which arrive as a list of
/// names (<c>FileGroupDescriptorW</c>) plus contents by index (<c>FileContents</c>).
/// </summary>
internal static unsafe class DroppedFiles
{
    private static readonly ushort UnicodeDescriptor = RegisterClipboardFormat("FileGroupDescriptorW");
    private static readonly ushort AnsiDescriptor = RegisterClipboardFormat("FileGroupDescriptor");
    private static readonly ushort FileContents = RegisterClipboardFormat("FileContents");

    /// <summary>Whether there are files here, real or virtual.</summary>
    public static bool HasFiles(IDataObject data) =>
        Has(data, UnicodeDescriptor) || Has(data, AnsiDescriptor) || Has(data, CF_HDROP);

    /// <summary>
    /// Paths to put on the shelf. Virtual files come first: an app that
    /// offers them wants to write the file itself, and a path next to them
    /// may point at a temporary copy that disappears. They're written to the
    /// inbox during the drop, while the other app still answers.
    /// </summary>
    public static IReadOnlyList<string> Read(IDataObject data)
    {
        var descriptor = ReadBytes(data, UnicodeDescriptor) is { } unicode
            ? FileGroupDescriptor.Parse(unicode)
            : ReadBytes(data, AnsiDescriptor) is { } ansi
                ? FileGroupDescriptor.Parse(ansi, Encoding.GetEncoding((int)GetACP()))
                : [];
        if (descriptor.Count > 0)
        {
            var received = Inbox.Temporary.Receive(descriptor, (index, path, byteCount) => WriteContents(data, index, path, byteCount));
            if (received.Count > 0)
            {
                return received;
            }
        }
        return ReadPaths(data);
    }

    private static bool Has(IDataObject data, ushort format) => data.QueryGetData(FORMATETC.For(format)) == 0;

    private static byte[]? ReadBytes(IDataObject data, ushort format)
    {
        if (data.GetData(FORMATETC.For(format), out var medium) != 0)
        {
            return null;
        }
        try
        {
            return medium.Tymed == TYMED_HGLOBAL ? Copy(medium.Handle, null) : null;
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static List<string> ReadPaths(IDataObject data)
    {
        var paths = new List<string>();
        if (data.GetData(FORMATETC.For(CF_HDROP), out var medium) != 0)
        {
            return paths;
        }
        try
        {
            var count = DragQueryFile(medium.Handle, uint.MaxValue, null, 0);
            for (uint index = 0; index < count; index++)
            {
                var length = DragQueryFile(medium.Handle, index, null, 0);
                var buffer = new char[length + 1];
                fixed (char* characters = buffer)
                {
                    DragQueryFile(medium.Handle, index, characters, length + 1);
                }
                paths.Add(new string(buffer, 0, (int)length));
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
        return paths;
    }

    /// <summary>Writes the contents of virtual file <paramref name="index"/> to <paramref name="path"/>.</summary>
    private static bool WriteContents(IDataObject data, int index, string path, long? byteCount)
    {
        var format = FORMATETC.For(FileContents, TYMED_HGLOBAL | TYMED_ISTREAM | TYMED_ISTORAGE, index);
        if (data.GetData(format, out var medium) != 0)
        {
            return false;
        }
        try
        {
            switch (medium.Tymed)
            {
                case TYMED_HGLOBAL:
                    File.WriteAllBytes(path, Copy(medium.Handle, byteCount));
                    return true;
                case TYMED_ISTREAM:
                    return CopyStream(medium.Handle, path);
                case TYMED_ISTORAGE:
                    // Outlook hands over whole messages this way.
                    return SaveStorage(medium.Handle, path);
                default:
                    return false;
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    /// <summary>
    /// The bytes in a global memory block, which may be rounded up past the
    /// end of the file.
    /// </summary>
    private static byte[] Copy(nint memory, long? byteCount)
    {
        var size = (long)GlobalSize(memory);
        if (byteCount is { } known && known < size)
        {
            size = known;
        }
        var bytes = GlobalLock(memory);
        try
        {
            return new ReadOnlySpan<byte>(bytes, (int)size).ToArray();
        }
        finally
        {
            GlobalUnlock(memory);
        }
    }

    private static bool CopyStream(nint pointer, string path)
    {
        var stream = ComInterop.Wrap<Interop.IStream>(pointer);
        try
        {
            using var file = File.Create(path);
            var buffer = new byte[81920];
            fixed (byte* start = buffer)
            {
                while (true)
                {
                    var result = stream.Read((nint)start, (uint)buffer.Length, out var read);
                    if (result < 0)
                    {
                        return false;
                    }
                    if (read == 0)
                    {
                        return true;
                    }
                    file.Write(buffer, 0, (int)read);
                }
            }
        }
        finally
        {
            ComInterop.Release(stream);
        }
    }

    private static bool SaveStorage(nint pointer, string path)
    {
        if (StgCreateDocfile(path, STGM_CREATE | STGM_READWRITE | STGM_SHARE_EXCLUSIVE, 0, out var destination) < 0)
        {
            return false;
        }
        var source = ComInterop.Wrap<IStorage>(pointer);
        var target = ComInterop.Wrap<IStorage>(destination);
        try
        {
            return source.CopyTo(0, 0, 0, destination) >= 0 && target.Commit(0) >= 0;
        }
        finally
        {
            ComInterop.Release(source);
            ComInterop.Release(target);
            System.Runtime.InteropServices.Marshal.Release(destination);
        }
    }
}
