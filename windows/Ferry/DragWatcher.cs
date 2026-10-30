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
/// is held down and moving, it looks for signs of a drag and drop under way:
/// the drag image Windows draws for File Explorer, the desktop and browsers,
/// or OLE's hidden window holding the mouse. Thin invisible drop targets along
/// the shelf's edge catch any other file drag that reaches them. It's all only
/// there mid-drag, so it never gets in the way of clicks.
/// </summary>
internal sealed class DragWatcher : IDisposable
{
    /// <summary>Thick enough to catch a pointer thrown at the edge, thin enough to miss.</summary>
    private const double StripThickness = 2;
    /// <summary>The window the shell draws a drag image in.</summary>
    private const string DragImageClass = "SysDragImage";
    /// <summary>OLE's hidden window, which holds the mouse while DoDragDrop runs.</summary>
    private const string OleDragClass = "CLIPBRDWNDCLASS";
    /// <summary>Enough unnoticed drags in the log to see why, without filling it.</summary>
    private const int UnnoticedDragsToLog = 20;

    private readonly DispatcherQueue dispatcher;
    private readonly DispatcherQueueTimer lookout;
    private readonly List<EdgeStrip> strips = [];
    private MouseHook? hook;
    private bool isArmed;
    private bool isDraggingFiles;
    /// <summary>What held the mouse during this drag, for the log.</summary>
    private string? captureClass;
    private int unnoticedDragsLogged;

    public DragWatcher(DispatcherQueue dispatcher)
    {
        this.dispatcher = dispatcher;
        lookout = dispatcher.CreateTimer();
        lookout.Interval = TimeSpan.FromMilliseconds(50);
        lookout.Tick += (_, _) => LookForDrag();
    }

    /// <summary>A drag that probably carries files began in another app.</summary>
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
        captureClass = null;
        var scale = Monitors.ScaleAt(Monitors.Pointer());
        var rects = Position.DragStrips(Monitors.All(), Math.Round(StripThickness * scale)).ToList();
        while (strips.Count < rects.Count)
        {
            strips.Add(new EdgeStrip(CanAccept, () => DragNoticed("an edge strip")));
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
        LookForDrag();
        lookout.Start();
    }

    private void Disarm()
    {
        isArmed = false;
        lookout.Stop();
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
        else if (unnoticedDragsLogged < UnnoticedDragsToLog)
        {
            unnoticedDragsLogged++;
            Log.Write($"A drag ended unnoticed; the mouse was held by {captureClass ?? "nothing"}");
        }
    }

    /// <summary>Runs every few milliseconds while the button is down and moving.</summary>
    private void LookForDrag()
    {
        if (isDraggingFiles || ShellFiles.IsDraggingOut)
        {
            lookout.Stop();
            return;
        }
        if (IsDragImageShowing())
        {
            DragNoticed("the drag image");
            return;
        }
        if (ForegroundCaptureClass() is { } capture)
        {
            captureClass = capture;
            if (capture == OleDragClass)
            {
                DragNoticed("OLE holding the mouse");
            }
        }
    }

    private void DragNoticed(string how)
    {
        lookout.Stop();
        if (!isDraggingFiles)
        {
            isDraggingFiles = true;
            Log.Write($"Drag noticed by {how}");
            FileDragBegan?.Invoke();
        }
    }

    private static bool IsDragImageShowing()
    {
        for (var window = FindWindowEx(0, 0, DragImageClass, null); window != 0; window = FindWindowEx(0, window, DragImageClass, null))
        {
            if (IsWindowVisible(window))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The class of the window that holds the mouse in the app being dragged from.</summary>
    private static unsafe string? ForegroundCaptureClass()
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var info = new GUITHREADINFO { Size = (uint)sizeof(GUITHREADINFO) };
        if (thread == 0 || !GetGUIThreadInfo(thread, ref info) || info.Capture == 0)
        {
            return null;
        }
        var name = stackalloc char[64];
        var length = GetClassName(info.Capture, name, 64);
        return length > 0 ? new string(name, 0, length) : null;
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
        dropTarget = new DropTarget { Name = "Edge strip", CanAccept = canAccept, TakesDrops = false };
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
        Log.Write(hook != 0 ? "Mouse hook installed" : $"Mouse hook failed with error {Marshal.GetLastPInvokeError()}");
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
