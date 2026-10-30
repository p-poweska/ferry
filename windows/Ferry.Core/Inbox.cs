namespace Ferry.Core;

/// <summary>
/// Where virtual files get written. Outlook, browsers and zip folders don't
/// hand over a file when you drag something out of them; they offer its
/// contents. Ferry only keeps references, so it writes them here first.
/// </summary>
public sealed class Inbox(string folder)
{
    public static Inbox Temporary { get; } = new(Path.Combine(Path.GetTempPath(), "Ferry Inbox"));

    public string Folder => folder;

    /// <summary>
    /// Clears out files left from the last run. The shelf starts out empty,
    /// so nothing points at them any more.
    /// </summary>
    public void Empty()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not there yet, or a file is still open somewhere; try next time.
        }
    }

    /// <summary>
    /// Writes <paramref name="files"/> into a folder of their own, so names
    /// can't collide with an earlier drop, and returns the top-level files and
    /// folders that made it. <paramref name="writeContents"/> writes file
    /// number <c>index</c> to a path and says whether it worked.
    /// </summary>
    public IReadOnlyList<string> Receive(IReadOnlyList<VirtualFile> files, Func<int, string, long?, bool> writeContents)
    {
        var drop = Path.Combine(folder, Guid.NewGuid().ToString("N"));
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var written = new List<string>();
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var relative = InboxPaths.Sanitize(file.Name, Path.DirectorySeparatorChar);
            try
            {
                if (file.IsFolder)
                {
                    Directory.CreateDirectory(Path.Combine(drop, relative));
                    written.Add(relative);
                    continue;
                }
                // Outlook happily attaches two different "image001.png" files.
                relative = InboxPaths.Unique(relative, taken);
                var path = Path.Combine(drop, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (writeContents(index, path, file.ByteCount))
                {
                    written.Add(relative);
                }
                else
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Leave this one out and keep the rest.
            }
        }
        return InboxPaths.TopLevel(written)
            .Select(name => Path.Combine(drop, name))
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .ToList();
    }
}
