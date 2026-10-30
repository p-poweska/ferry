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
        var items = paths.Select(ILCreateFromPath).Where(item => item != 0).ToArray();
        if (items.Length == 0)
        {
            return DROPEFFECT_NONE;
        }
        try
        {
            nint data;
            fixed (nint* list = items)
            {
                if (SHCreateDataObject(0, (uint)items.Length, list, 0, DataObjectInterface, out data) < 0)
                {
                    return DROPEFFECT_NONE;
                }
            }
            IsDraggingOut = true;
            try
            {
                return SHDoDragDrop(window, data, 0, DROPEFFECT_COPY | DROPEFFECT_MOVE | DROPEFFECT_LINK, out var effect) >= 0
                    ? effect
                    : DROPEFFECT_NONE;
            }
            finally
            {
                IsDraggingOut = false;
                Marshal.Release(data);
            }
        }
        finally
        {
            Array.ForEach(items, ILFree);
        }
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
