using System.Buffers.Binary;
using System.Text;

namespace Ferry.Core;

/// <summary>
/// A file another app offers to hand over on drop without it existing on disk
/// yet, like an Outlook attachment or an image in a browser. Its contents come
/// separately, by index.
/// </summary>
public sealed record VirtualFile(string Name, bool IsFolder, long? ByteCount);

/// <summary>
/// Reads the <c>FileGroupDescriptorW</c> (or the older ANSI
/// <c>FileGroupDescriptor</c>) clipboard format: a count followed by one
/// <c>FILEDESCRIPTOR</c> per file.
/// </summary>
public static class FileGroupDescriptor
{
    private const int NameOffset = 72;
    private const int NameLength = 260;
    private const int UnicodeSize = NameOffset + NameLength * 2;
    private const int AnsiSize = NameOffset + NameLength;

    private const uint HasAttributes = 0x4;
    private const uint HasFileSize = 0x40;
    private const uint DirectoryAttribute = 0x10;

    /// <param name="ansi">Decodes names in the ANSI format; <c>null</c> for <c>FileGroupDescriptorW</c>.</param>
    public static IReadOnlyList<VirtualFile> Parse(ReadOnlySpan<byte> data, Encoding? ansi = null)
    {
        if (data.Length < 4)
        {
            return [];
        }
        var size = ansi is null ? UnicodeSize : AnsiSize;
        var count = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var files = new List<VirtualFile>();
        for (var index = 0; index < count && 4 + (index + 1) * size <= data.Length; index++)
        {
            var descriptor = data.Slice(4 + index * size, size);
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            var attributes = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[36..]);
            var high = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[64..]);
            var low = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[68..]);
            var name = ReadName(descriptor[NameOffset..], ansi);
            var isFolder = (flags & HasAttributes) != 0 && (attributes & DirectoryAttribute) != 0;
            long? byteCount = (flags & HasFileSize) != 0 ? ((long)high << 32) | low : null;
            files.Add(new VirtualFile(name, isFolder, isFolder ? null : byteCount));
        }
        return files;
    }

    private static string ReadName(ReadOnlySpan<byte> bytes, Encoding? ansi)
    {
        if (ansi is not null)
        {
            var end = bytes.IndexOf((byte)0);
            return ansi.GetString(end < 0 ? bytes : bytes[..end]);
        }
        var name = Encoding.Unicode.GetString(bytes[..(NameLength * 2)]);
        var terminator = name.IndexOf('\0');
        return terminator < 0 ? name : name[..terminator];
    }
}

/// <summary>Turns names other apps suggest into safe paths inside the inbox.</summary>
public static class InboxPaths
{
    private static readonly char[] Separators = ['\\', '/'];
    private static readonly char[] InvalidCharacters = ['<', '>', ':', '"', '|', '?', '*'];
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// A relative path that stays inside the folder it's written to: no drive,
    /// no "..", and nothing Windows refuses in a file name. Folders inside it
    /// are joined with <paramref name="separator"/>.
    /// </summary>
    public static string Sanitize(string name, char separator = '\\')
    {
        var parts = name
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePart)
            .Where(part => part.Length > 0)
            .ToList();
        return parts.Count == 0 ? "Untitled" : string.Join(separator, parts);
    }

    /// <summary>The top-level files and folders among <paramref name="paths"/>, in order.</summary>
    public static IReadOnlyList<string> TopLevel(IEnumerable<string> paths) =>
        paths
            .Select(path => path.Split(Separators, 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// <paramref name="name"/>, or "name (2).ext" and so on when it's taken,
    /// the way File Explorer numbers copies.
    /// </summary>
    public static string Unique(string name, ISet<string> taken)
    {
        var folder = name[..(name.LastIndexOfAny(Separators) + 1)];
        var file = name[folder.Length..];
        var dot = file.LastIndexOf('.');
        var (stem, extension) = dot > 0 ? (file[..dot], file[dot..]) : (file, "");
        var candidate = name;
        for (var number = 2; taken.Contains(candidate); number++)
        {
            candidate = $"{folder}{stem} ({number}){extension}";
        }
        taken.Add(candidate);
        return candidate;
    }

    private static string SanitizePart(string part)
    {
        var characters = part
            .Select(character => character < 32 || InvalidCharacters.Contains(character) ? '_' : character)
            .ToArray();
        // Windows drops trailing dots and spaces, which would turn "..." into
        // the folder above.
        var cleaned = new string(characters).Trim().TrimEnd('.');
        if (cleaned.Length == 0)
        {
            return "";
        }
        var stem = cleaned.Split('.')[0];
        return ReservedNames.Contains(stem) ? "_" + cleaned : cleaned;
    }
}
