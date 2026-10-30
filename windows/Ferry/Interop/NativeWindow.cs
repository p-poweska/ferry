using System.ComponentModel;
using System.Runtime.InteropServices;
using static Ferry.Interop.NativeMethods;

namespace Ferry.Interop;

/// <summary>A plain Win32 window, for the jobs that don't need any XAML.</summary>
internal abstract unsafe class NativeWindow : IDisposable
{
    private static readonly Dictionary<nint, NativeWindow> Instances = [];
    private static readonly HashSet<string> RegisteredClasses = [];

    protected NativeWindow(string className, uint style, uint extendedStyle)
    {
        var module = GetModuleHandle(null);
        if (RegisteredClasses.Add(className))
        {
            fixed (char* name = className)
            {
                var windowClass = new WNDCLASSEXW
                {
                    Size = (uint)sizeof(WNDCLASSEXW),
                    WindowProcedure = &Procedure,
                    Instance = module,
                    ClassName = name,
                };
                if (RegisterClassEx(windowClass) == 0)
                {
                    throw new Win32Exception();
                }
            }
        }
        Handle = CreateWindowEx(extendedStyle, className, null, style, 0, 0, 0, 0, 0, 0, module, 0);
        if (Handle == 0)
        {
            throw new Win32Exception();
        }
        Instances[Handle] = this;
    }

    public nint Handle { get; }

    public virtual void Dispose()
    {
        Instances.Remove(Handle);
        DestroyWindow(Handle);
    }

    protected virtual nint WindowProcedure(uint message, nint wParam, nint lParam) =>
        DefWindowProc(Handle, message, wParam, lParam);

    [UnmanagedCallersOnly]
    private static nint Procedure(nint window, uint message, nint wParam, nint lParam) =>
        Instances.TryGetValue(window, out var instance)
            ? instance.WindowProcedure(message, wParam, lParam)
            : DefWindowProc(window, message, wParam, lParam);
}

/// <summary>
/// A hidden window that receives the tray icon's clicks, the global
/// shortcut and news about screens changing. It's a real top-level window
/// rather than a message-only one so broadcasts like "TaskbarCreated" reach it.
/// </summary>
internal sealed class MessageWindow() : NativeWindow("FerryMessages", WS_POPUP, WS_EX_TOOLWINDOW)
{
    public event Action<uint, nint, nint>? MessageReceived;

    protected override nint WindowProcedure(uint message, nint wParam, nint lParam)
    {
        MessageReceived?.Invoke(message, wParam, lParam);
        return base.WindowProcedure(message, wParam, lParam);
    }
}
