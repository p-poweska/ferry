using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static Ferry.Interop.NativeMethods;

namespace Ferry.Interop;

/// <summary>
/// An OLE drop target for a window and everything inside it. Ferry handles
/// drops at this level rather than through WinUI, because only the raw data
/// object hands over every virtual file an app like Outlook offers.
/// </summary>
[GeneratedComClass]
internal sealed unsafe partial class DropTarget : IDropTarget, IDisposable
{
    private static readonly Guid DragDropHelperClass = new("4657278A-411B-11d2-839A-00C04FD918D0");

    private readonly IDropTargetHelper? helper = ComInterop.TryCreate<IDropTargetHelper>(DragDropHelperClass);
    private readonly HashSet<nint> windows = [];
    private nint pointer;
    private nint owner;
    private IDataObject? current;
    private bool isAccepted;

    /// <summary>Whether to take what's being dragged. Asked again as the drag moves until it says yes.</summary>
    public required Func<IDataObject, bool> CanAccept { get; init; }

    /// <summary>A target that only notices drags tells the source it won't take them.</summary>
    public bool TakesDrops { get; init; } = true;

    /// <summary>True when an acceptable drag comes over the window, false when it leaves or drops.</summary>
    public event Action<bool>? TargetingChanged;

    public event Action<IDataObject>? Dropped;

    /// <summary>
    /// Takes over drops for <paramref name="window"/> and its child windows,
    /// replacing any target they had. Safe to call again when children appear.
    /// </summary>
    public void Register(nint window)
    {
        owner = window;
        if (pointer == 0)
        {
            pointer = ComInterop.PointerFor<IDropTarget>(this);
        }
        RegisterOn(window);
        var handle = GCHandle.Alloc(this);
        try
        {
            EnumChildWindows(window, &RegisterChild, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose()
    {
        foreach (var window in windows)
        {
            RevokeDragDrop(window);
        }
        windows.Clear();
        if (pointer != 0)
        {
            Marshal.Release(pointer);
            pointer = 0;
        }
        ComInterop.Release(helper);
    }

    [UnmanagedCallersOnly]
    private static int RegisterChild(nint window, nint parameter)
    {
        ((DropTarget)GCHandle.FromIntPtr(parameter).Target!).RegisterOn(window);
        return 1;
    }

    private void RegisterOn(nint window)
    {
        if (windows.Add(window))
        {
            RevokeDragDrop(window);
            RegisterDragDrop(window, pointer);
        }
    }

    public int DragEnter(nint dataObject, uint keyState, POINT point, ref uint effect)
    {
        ReleaseCurrent();
        current = ComInterop.Wrap<IDataObject>(dataObject);
        isAccepted = CanAccept(current);
        effect = Effect(effect);
        helper?.DragEnter(owner, dataObject, point, effect);
        if (isAccepted)
        {
            TargetingChanged?.Invoke(true);
        }
        return 0;
    }

    public int DragOver(uint keyState, POINT point, ref uint effect)
    {
        // Some apps add their files to the data a moment after the drag begins.
        if (!isAccepted && current is not null && CanAccept(current))
        {
            isAccepted = true;
            TargetingChanged?.Invoke(true);
        }
        effect = Effect(effect);
        helper?.DragOver(point, effect);
        return 0;
    }

    public int DragLeave()
    {
        helper?.DragLeave();
        EndTargeting();
        ReleaseCurrent();
        return 0;
    }

    public int Drop(nint dataObject, uint keyState, POINT point, ref uint effect)
    {
        ReleaseCurrent();
        var data = ComInterop.Wrap<IDataObject>(dataObject);
        try
        {
            isAccepted = isAccepted || CanAccept(data);
            effect = Effect(effect);
            helper?.Drop(dataObject, point, effect);
            var wasAccepted = isAccepted;
            EndTargeting();
            if (wasAccepted && TakesDrops)
            {
                Dropped?.Invoke(data);
            }
        }
        finally
        {
            ComInterop.Release(data);
        }
        return 0;
    }

    /// <summary>
    /// The shelf only remembers where files are, so it asks for a copy and
    /// never a move: a move would have Outlook delete the message afterwards.
    /// </summary>
    private uint Effect(uint allowed)
    {
        if (!isAccepted || !TakesDrops) return DROPEFFECT_NONE;
        if ((allowed & DROPEFFECT_COPY) != 0) return DROPEFFECT_COPY;
        if ((allowed & DROPEFFECT_LINK) != 0) return DROPEFFECT_LINK;
        return DROPEFFECT_NONE;
    }

    private void EndTargeting()
    {
        if (isAccepted)
        {
            isAccepted = false;
            TargetingChanged?.Invoke(false);
        }
    }

    private void ReleaseCurrent()
    {
        ComInterop.Release(current);
        current = null;
    }
}
