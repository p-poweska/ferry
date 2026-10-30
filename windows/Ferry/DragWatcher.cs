using System.Runtime.InteropServices;
using Ferry.Core;
using Ferry.Interop;
using Microsoft.UI.Dispatching;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>
/// Notices when the user drags files in any app. Windows has no global
/// "a drag began" event, so Ferry watches the left mouse button with a
/// low-level mouse hook, which needs no special permissions. While the button
/// is held down and moving, thin invisible drop targets wait along the edge
/// where the shelf lives; a file drag that reaches one brings the shelf up.
/// They're only there mid-drag, so they never get in the way of clicks.
/// </summary>
internal sealed class DragWatcher : IDisposable
{
    /// <summary>Thick enough to catch a pointer thrown at the edge, thin enough to miss.</summary>
    private const double StripThickness = 2;

    private readonly DispatcherQueue dispatcher;
    private readonly List<EdgeStrip> strips = [];
    private MouseHook? hook;
    private bool isArmed;
    private bool isDraggingFiles;

    public DragWatcher(DispatcherQueue dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public event Action? FileDragBegan;
    public event Action? FileDragEnded;

    /// <summary>Where the shelf appears, which decides the edges to watch.</summary>
    public ShelfPosition Position { get; set; } = ShelfPosition.RightEdge;

    public bool IsEnabled
    {
        get => hook is not null;
        set
        {
            if (value && hook is null)
            {
                hook = new MouseHook(dispatcher, Arm, ButtonReleased);
            }
            else if (!value && hook is not null)
            {
                hook.Dispose();
                hook = null;
                Disarm();
                isDraggingFiles = false;
            }
        }
    }

    public void Dispose()
    {
        IsEnabled = false;
        foreach (var strip in strips)
        {
            strip.Dispose();
        }
        strips.Clear();
    }

    /// <summary>The button is down and the pointer moved far enough to be a drag.</summary>
    private void Arm()
    {
        if (isArmed)
        {
            return;
        }
        isArmed = true;
        var scale = Monitors.ScaleAt(Monitors.Pointer());
        var rects = Position.DragStrips(Monitors.All(), Math.Round(StripThickness * scale)).ToList();
        while (strips.Count < rects.Count)
        {
            strips.Add(new EdgeStrip(CanAccept, FileDragNoticed));
        }
        for (var index = 0; index < strips.Count; index++)
        {
            if (index < rects.Count)
            {
                strips[index].Show(rects[index]);
            }
            else
            {
                strips[index].Hide();
            }
        }
    }

    private void Disarm()
    {
        isArmed = false;
        foreach (var strip in strips)
        {
            strip.Hide();
        }
    }

    private void ButtonReleased()
    {
        Disarm();
        if (isDraggingFiles)
        {
            isDraggingFiles = false;
            FileDragEnded?.Invoke();
        }
    }

    private void FileDragNoticed()
    {
        if (!isDraggingFiles)
        {
            isDraggingFiles = true;
            FileDragBegan?.Invoke();
        }
    }

    /// <summary>Files from another app; the shelf's own drags don't count.</summary>
    private static bool CanAccept(IDataObject data) => !ShellFiles.IsDraggingOut && DroppedFiles.HasFiles(data);
}

/// <summary>
/// A practically invisible window along one edge of a screen that notices
/// file drags reaching it. It never takes the drop itself.
/// </summary>
internal sealed class EdgeStrip : NativeWindow
{
    private readonly DropTarget dropTarget;

    public EdgeStrip(Func<IDataObject, bool> canAccept, Action noticed)
        : base("FerryEdgeStrip", WS_POPUP, WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_LAYERED)
    {
        // Fully transparent windows let the pointer through; one step above isn't.
        SetLayeredWindowAttributes(Handle, 0, 1, LWA_ALPHA);
        dropTarget = new DropTarget { CanAccept = canAccept, TakesDrops = false };
        dropTarget.TargetingChanged += isTargeted =>
        {
            if (isTargeted)
            {
                noticed();
            }
        };
        dropTarget.Register(Handle);
    }

    public void Show(ScreenRect rect) =>
        SetWindowPos(Handle, HWND_TOPMOST, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);

    public void Hide() => ShowWindow(Handle, SW_HIDE);

    public override void Dispose()
    {
        dropTarget.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// A low-level mouse hook on a thread of its own, so a busy UI thread can't
/// make Windows drop it. It reports a press that turned into a drag and the
/// release after it.
/// </summary>
internal sealed unsafe class MouseHook : IDisposable
{
    private static MouseHook? current;

    private readonly DispatcherQueue dispatcher;
    private readonly Action dragged;
    private readonly Action released;
    private readonly Thread thread;
    private readonly int dragWidth = GetSystemMetrics(SM_CXDRAG);
    private readonly int dragHeight = GetSystemMetrics(SM_CYDRAG);
    private uint threadId;
    private POINT pressedAt;
    private bool isPressed;
    private bool isDragging;

    public MouseHook(DispatcherQueue dispatcher, Action dragged, Action released)
    {
        this.dispatcher = dispatcher;
        this.dragged = dragged;
        this.released = released;
        current = this;
        using var started = new ManualResetEventSlim();
        thread = new Thread(() => Run(started)) { IsBackground = true, Name = "Ferry mouse hook" };
        thread.Start();
        started.Wait();
    }

    public void Dispose()
    {
        PostThreadMessage(threadId, WM_QUIT, 0, 0);
        thread.Join(TimeSpan.FromSeconds(1));
        current = null;
    }

    private void Run(ManualResetEventSlim started)
    {
        threadId = GetCurrentThreadId();
        var hook = SetWindowsHookEx(WH_MOUSE_LL, &Callback, GetModuleHandle(null), 0);
        started.Set();
        // The hook is called from this thread's message loop.
        while (GetMessage(out _, 0, 0, 0) > 0)
        {
        }
        UnhookWindowsHookEx(hook);
    }

    [UnmanagedCallersOnly]
    private static nint Callback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            current?.Handle((uint)wParam, ((MSLLHOOKSTRUCT*)lParam)->Point);
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private void Handle(uint message, POINT point)
    {
        switch (message)
        {
            case WM_LBUTTONDOWN:
                isPressed = true;
                isDragging = false;
                pressedAt = point;
                break;
            case WM_MOUSEMOVE when isPressed && !isDragging:
                if (Math.Abs(point.X - pressedAt.X) > dragWidth || Math.Abs(point.Y - pressedAt.Y) > dragHeight)
                {
                    isDragging = true;
                    dispatcher.TryEnqueue(() => dragged());
                }
                break;
            case WM_LBUTTONUP when isPressed:
                isPressed = false;
                if (isDragging)
                {
                    isDragging = false;
                    dispatcher.TryEnqueue(() => released());
                }
                break;
        }
    }
}
