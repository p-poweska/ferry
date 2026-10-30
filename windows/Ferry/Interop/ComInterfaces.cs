using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static Ferry.Interop.NativeMethods;

namespace Ferry.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct FORMATETC
{
    public ushort Format;
    public nint TargetDevice;
    public uint Aspect;
    public int Index;
    public uint Tymed;

    public static FORMATETC For(ushort format, uint tymed = TYMED_HGLOBAL, int index = -1) =>
        new() { Format = format, Aspect = DVASPECT_CONTENT, Index = index, Tymed = tymed };
}

[StructLayout(LayoutKind.Sequential)]
internal struct STGMEDIUM
{
    public uint Tymed;
    /// <summary>An HGLOBAL, IStream* or IStorage*, depending on <see cref="Tymed"/>.</summary>
    public nint Handle;
    public nint ReleaseOwner;
}

[GeneratedComInterface]
[Guid("00000122-0000-0000-C000-000000000046")]
internal partial interface IDropTarget
{
    [PreserveSig] int DragEnter(nint dataObject, uint keyState, POINT point, ref uint effect);
    [PreserveSig] int DragOver(uint keyState, POINT point, ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop(nint dataObject, uint keyState, POINT point, ref uint effect);
}

[GeneratedComInterface]
[Guid("0000010e-0000-0000-C000-000000000046")]
internal partial interface IDataObject
{
    [PreserveSig] int GetData(in FORMATETC format, out STGMEDIUM medium);
    [PreserveSig] int GetDataHere(in FORMATETC format, ref STGMEDIUM medium);
    [PreserveSig] int QueryGetData(in FORMATETC format);
    [PreserveSig] int GetCanonicalFormatEtc(in FORMATETC format, out FORMATETC canonical);
    [PreserveSig] int SetData(in FORMATETC format, in STGMEDIUM medium, [MarshalAs(UnmanagedType.Bool)] bool release);
    [PreserveSig] int EnumFormatEtc(uint direction, out nint enumerator);
    [PreserveSig] int DAdvise(in FORMATETC format, uint flags, nint sink, out uint connection);
    [PreserveSig] int DUnadvise(uint connection);
    [PreserveSig] int EnumDAdvise(out nint enumerator);
}

/// <summary>Just the reading half of IStream, which comes first in its vtable.</summary>
[GeneratedComInterface]
[Guid("0000000c-0000-0000-C000-000000000046")]
internal partial interface IStream
{
    [PreserveSig] int Read(nint buffer, uint size, out uint read);
    [PreserveSig] int Write(nint buffer, uint size, out uint written);
}

/// <summary>IStorage up to Commit, enough to copy one into a file.</summary>
[GeneratedComInterface]
[Guid("0000000b-0000-0000-C000-000000000046")]
internal partial interface IStorage
{
    [PreserveSig] int CreateStream(nint name, uint mode, uint reserved1, uint reserved2, out nint stream);
    [PreserveSig] int OpenStream(nint name, nint reserved1, uint mode, uint reserved2, out nint stream);
    [PreserveSig] int CreateStorage(nint name, uint mode, uint reserved1, uint reserved2, out nint storage);
    [PreserveSig] int OpenStorage(nint name, nint priority, uint mode, nint exclude, uint reserved, out nint storage);
    [PreserveSig] int CopyTo(uint excludeCount, nint excludeIds, nint excludeNames, nint destination);
    [PreserveSig] int MoveElementTo(nint name, nint destination, nint newName, uint flags);
    [PreserveSig] int Commit(uint flags);
}

/// <summary>Draws the source app's drag image over Ferry's windows.</summary>
[GeneratedComInterface]
[Guid("4657278B-411B-11d2-839A-00C04FD918D0")]
internal partial interface IDropTargetHelper
{
    [PreserveSig] int DragEnter(nint window, nint dataObject, in POINT point, uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int DragOver(in POINT point, uint effect);
    [PreserveSig] int Drop(nint dataObject, in POINT point, uint effect);
    [PreserveSig] int Show([MarshalAs(UnmanagedType.Bool)] bool show);
}

[GeneratedComInterface]
[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
internal partial interface IVirtualDesktopManager
{
    [PreserveSig] int IsWindowOnCurrentVirtualDesktop(nint window, out int onCurrentDesktop);
    [PreserveSig] int GetWindowDesktopId(nint window, out Guid desktopId);
    [PreserveSig] int MoveWindowToDesktop(nint window, in Guid desktopId);
}

/// <summary>Turns COM pointers into the interfaces above and back.</summary>
internal static class ComInterop
{
    private static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>
    /// A managed wrapper of its own around <paramref name="pointer"/>; the
    /// caller keeps its reference. Let it go with <see cref="Release"/>.
    /// </summary>
    public static T Wrap<T>(nint pointer) where T : class =>
        (T)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);

    /// <summary>Drops the reference a wrapper from <see cref="Wrap"/> holds.</summary>
    public static void Release(object? wrapper)
    {
        if (wrapper is ComObject comObject)
        {
            comObject.FinalRelease();
        }
    }

    /// <summary>A COM pointer to <paramref name="instance"/> as <typeparamref name="T"/>, which the caller owns.</summary>
    public static nint PointerFor<T>(T instance) where T : class
    {
        var unknown = Wrappers.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, typeof(T).GUID, out var pointer));
            return pointer;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>Creates a COM object, or returns <c>null</c> when Windows doesn't have it.</summary>
    public static T? TryCreate<T>(Guid classId) where T : class
    {
        if (CoCreateInstance(classId, 0, CLSCTX_ALL, typeof(T).GUID, out var pointer) < 0)
        {
            return null;
        }
        try
        {
            return Wrap<T>(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }
}
