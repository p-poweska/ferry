using System.Globalization;

namespace Ferry.Core;

/// <summary>The words the shelf, its list and the tray menu use for files.</summary>
public static class ItemText
{
    public static string Count(int count) => count == 1 ? "1 item" : $"{count} items";

    /// <summary>"PNG File · 180 KB", or just the kind for folders.</summary>
    public static string Detail(ShelfItem item) =>
        item.IsFolder || item.ByteCount is not { } bytes ? item.Kind : $"{item.Kind} · {FormatBytes(bytes)}";

    /// <summary>Total size of the files plus a folder count, e.g. "3.4 MB · 1 folder".</summary>
    public static string Summary(IEnumerable<ShelfItem> items)
    {
        var list = items.ToList();
        var bytes = list.Where(item => !item.IsFolder).Sum(item => item.ByteCount ?? 0);
        var folders = list.Count(item => item.IsFolder);
        var parts = new List<string>();
        if (bytes > 0) parts.Add(FormatBytes(bytes));
        if (folders > 0) parts.Add(folders == 1 ? "1 folder" : $"{folders} folders");
        return parts.Count == 0 ? "Ready to move" : string.Join(" · ", parts);
    }

    /// <summary>Sizes the way File Explorer writes them: 1024-based, three significant digits.</summary>
    public static string FormatBytes(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
        {
            return string.Format(culture, "{0} bytes", bytes);
        }
        string[] units = ["KB", "MB", "GB", "TB", "PB"];
        var value = bytes / 1024.0;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
        return value.ToString(format, culture) + " " + units[unit];
    }
}
