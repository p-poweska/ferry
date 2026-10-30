using System.Diagnostics;
using Ferry.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;

namespace Ferry;

/// <summary>
/// Keeps the shelf on the virtual desktop you're looking at, so files can
/// ride along to another desktop. Tool windows like the shelf normally show
/// on every desktop already; this moves it over whenever Windows leaves it
/// behind, using the documented IVirtualDesktopManager.
/// </summary>
internal sealed class VirtualDesktopFollower
{
    private static readonly Guid ManagerClass = new("aa509086-5ca9-4c25-8f95-589d3c07b48a");
    private const string DesktopsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

    private readonly nint window;
    private readonly IVirtualDesktopManager? manager = ComInterop.TryCreate<IVirtualDesktopManager>(ManagerClass);
    private readonly DispatcherQueueTimer timer;

    public VirtualDesktopFollower(nint window, DispatcherQueue dispatcher)
    {
        this.window = window;
        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => Follow();
    }

    public void Start()
    {
        if (manager is not null)
        {
            timer.Start();
        }
    }

    public void Stop() => timer.Stop();

    private void Follow()
    {
        if (manager is null || manager.IsWindowOnCurrentVirtualDesktop(window, out var isOnCurrent) < 0 || isOnCurrent != 0)
        {
            return;
        }
        if (CurrentDesktop() is { } desktop)
        {
            manager.MoveWindowToDesktop(window, desktop);
        }
    }

    /// <summary>The desktop on screen. Windows 11 keeps it in one place, Windows 10 per session.</summary>
    private static Guid? CurrentDesktop()
    {
        var session = Process.GetCurrentProcess().SessionId;
        foreach (var key in new[] { $@"{DesktopsKey}\VirtualDesktops", $@"{DesktopsKey}\SessionInfo\{session}\VirtualDesktops" })
        {
            using var desktops = Registry.CurrentUser.OpenSubKey(key);
            if (desktops?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } id)
            {
                return new Guid(id);
            }
        }
        return null;
    }
}
