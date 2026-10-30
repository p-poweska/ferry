using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Ferry.Core;
using Ferry.Interop;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>What File Explorer knows about files, and what it can do with them.</summary>
internal static unsafe class ShellFiles
{
    private static readonly Guid DataObjectInterface = new("0000010e-0000-0000-C000-000000000046");

    /// <summary>True while files are being dragged off the shelf, so Ferry ignores its own drag.</summary>
    public static bool IsDraggingOut { get; private set; }

    public static void Open(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                // No app for it, or the user cancelled the "open with" prompt.
            }
        }
    }

    /// <summary>Opens a File Explorer window per folder with the files selected.</summary>
    public static void ShowInExplorer(IEnumerable<string> paths)
    {
        foreach (var group in paths.GroupBy(path => Path.GetDirectoryName(path) ?? path, StringComparer.OrdinalIgnoreCase))
        {
            var folder = ILCreateFromPath(group.Key);
            var items = group.Select(ILCreateFromPath).Where(item => item != 0).ToArray();
            try
            {
                fixed (nint* list = items)
                {
                    SHOpenFolderAndSelectItems(folder, (uint)items.Length, list, 0);
                }
            }
            finally
            {
                ILFree(folder);
                Array.ForEach(items, ILFree);
            }
        }
    }

    /// <summary>
    /// Drags <paramref name="paths"/> out the way File Explorer does: moved on
    /// the same drive, copied to another one, with Ctrl, Shift and Alt
    /// working as usual. Returns the drop effect; Explorer moves the files
    /// itself, so Ferry never deletes anything.
    /// </summary>
    public static uint DragOut(nint window, IReadOnlyList<string> paths)
    {
        var data = CreateDataObject(paths);
        if (data == 0)
        {
            Log.Write($"Couldn't make a drag for {paths.Count} file(s)");
            return DROPEFFECT_NONE;
        }
        IsDraggingOut = true;
        try
        {
            var result = SHDoDragDrop(window, data, 0, DROPEFFECT_COPY | DROPEFFECT_MOVE | DROPEFFECT_LINK, out var effect);
            Log.Write($"Dragged {paths.Count} file(s) out: result 0x{result:X8}, effect {effect}");
            return result >= 0 ? effect : DROPEFFECT_NONE;
        }
        finally
        {
            IsDraggingOut = false;
            Marshal.Release(data);
        }
    }

    /// <summary>
    /// The data object File Explorer itself would make for these files: from
    /// their folder when they share one, which also brings Explorer's drag
    /// image, or else a plain list of paths (CF_HDROP).
    /// </summary>
    private static nint CreateDataObject(IReadOnlyList<string> paths)
    {
        var folders = paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (folders is [{ } folder] && FromFolder(folder, paths) is not 0 and var fromFolder)
        {
            return fromFolder;
        }
        if (SHCreateDataObject(0, 0, null, 0, DataObjectInterface, out var data) < 0)
        {
            return 0;
        }
        var wrapper = ComInterop.Wrap<IDataObject>(data);
        try
        {
            var medium = new STGMEDIUM { Tymed = TYMED_HGLOBAL, Handle = FileList(paths) };
            if (wrapper.SetData(FORMATETC.For(CF_HDROP), medium, release: true) >= 0)
            {
                return data;
            }
            ReleaseStgMedium(ref medium);
            Marshal.Release(data);
            return 0;
        }
        finally
        {
            ComInterop.Release(wrapper);
        }
    }

    /// <summary>A data object for files in one folder, built from the folder and each file's last ID.</summary>
    private static nint FromFolder(string folder, IReadOnlyList<string> paths)
    {
        var parent = ILCreateFromPath(folder);
        var items = paths.Select(ILCreateFromPath).ToArray();
        try
        {
            if (parent == 0 || items.Contains(0))
            {
                return 0;
            }
            var children = items.Select(ILFindLastID).ToArray();
            fixed (nint* list = children)
            {
                return SHCreateDataObject(parent, (uint)children.Length, list, 0, DataObjectInterface, out var data) >= 0 ? data : 0;
            }
        }
        finally
        {
            ILFree(parent);
            Array.ForEach(items, ILFree);
        }
    }

    /// <summary>A DROPFILES block: the header, then each path, then an empty one.</summary>
    private static nint FileList(IReadOnlyList<string> paths)
    {
        var text = string.Concat(paths.Select(path => path + "\0")) + "\0";
        var size = sizeof(DROPFILES) + text.Length * sizeof(char);
        var memory = GlobalAlloc(GHND, (nuint)size);
        var bytes = (byte*)GlobalLock(memory);
        try
        {
            *(DROPFILES*)bytes = new DROPFILES { FilesOffset = (uint)sizeof(DROPFILES), Wide = 1 };
            text.AsSpan().CopyTo(new Span<char>(bytes + sizeof(DROPFILES), text.Length));
        }
        finally
        {
            GlobalUnlock(memory);
        }
        return memory;
    }
}

/// <summary>Describes files the way File Explorer names their types, like "PNG File".</summary>
internal sealed unsafe class ShellFileDescriber : IFileDescriber
{
    public ShelfItem Describe(string path)
    {
        var item = BasicFileDescriber.Instance.Describe(path);
        var info = new SHFILEINFOW();
        if (SHGetFileInfo(path, 0, ref info, (uint)sizeof(SHFILEINFOW), SHGFI_TYPENAME) == 0)
        {
            return item;
        }
        var typeName = new string(info.TypeName);
        return typeName.Length > 0 ? item with { Kind = typeName } : item;
    }

    public bool Exists(string path) => BasicFileDescriber.Instance.Exists(path);
}

/// <summary>Names of keys on the current keyboard layout.</summary>
internal static unsafe class KeyNames
{
    public static string? Name(int key)
    {
        var scanCode = MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);
        if (scanCode == 0)
        {
            return null;
        }
        var buffer = stackalloc char[64];
        var length = GetKeyNameText((int)(scanCode << 16), buffer, 64);
        return length > 0 ? new string(buffer, 0, length) : null;
    }
}
