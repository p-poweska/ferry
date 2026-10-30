namespace Ferry.Core;

/// <summary>
/// A file parked on the shelf. Ferry only keeps a reference: the file stays
/// where it is until it gets dragged out to its new home.
/// </summary>
public sealed record ShelfItem(string Path, string Name, string Kind, long? ByteCount, bool IsFolder);

/// <summary>Looks up what the shelf shows about a file.</summary>
public interface IFileDescriber
{
    ShelfItem Describe(string path);
    bool Exists(string path);
}

/// <summary>Describes files with plain System.IO, without asking the shell.</summary>
public sealed class BasicFileDescriber : IFileDescriber
{
    public static readonly BasicFileDescriber Instance = new();

    public ShelfItem Describe(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        if (Directory.Exists(path))
        {
            return new ShelfItem(path, name, "File folder", null, true);
        }
        long? size = File.Exists(path) ? new FileInfo(path).Length : null;
        var extension = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return new ShelfItem(path, name, extension.Length > 0 ? $"{extension} File" : "File", size, false);
    }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}

/// <summary>The files waiting on the shelf, oldest first.</summary>
public sealed class Shelf(IFileDescriber describer)
{
    private List<ShelfItem> items = [];

    public Shelf() : this(BasicFileDescriber.Instance) { }

    /// <summary>Raised after every change to <see cref="Items"/>.</summary>
    public event Action? Changed;

    public IReadOnlyList<ShelfItem> Items => items;
    public bool IsEmpty => items.Count == 0;
    public IEnumerable<string> Paths => items.Select(item => item.Path);

    public void Add(IEnumerable<string> paths)
    {
        Update(updated =>
        {
            var known = new HashSet<string>(updated.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
            var added = paths.Select(Normalize).Where(known.Add);
            updated.AddRange(added.Select(describer.Describe));
        });
    }

    public void Remove(IEnumerable<string> paths)
    {
        var removed = new HashSet<string>(paths.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        Update(updated => updated.RemoveAll(item => removed.Contains(item.Path)));
    }

    public void RemoveAll() => Update(updated => updated.Clear());

    /// <summary>Forgets items whose files were moved or deleted outside of Ferry.</summary>
    public void PruneMissing() => Update(updated => updated.RemoveAll(item => !describer.Exists(item.Path)));

    private void Update(Action<List<ShelfItem>> change)
    {
        var updated = new List<ShelfItem>(items);
        change(updated);
        if (updated.SequenceEqual(items))
        {
            return;
        }
        items = updated;
        Changed?.Invoke();
    }

    private static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
}
