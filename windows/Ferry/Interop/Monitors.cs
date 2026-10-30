using System.Runtime.InteropServices;
using Ferry.Core;
using static Ferry.Interop.NativeMethods;

namespace Ferry.Interop;

/// <summary>The screens, in physical pixels, and where the pointer is on them.</summary>
internal static unsafe class Monitors
{
    public static IReadOnlyList<Screen> All()
    {
        var screens = new List<Screen>();
        var handle = GCHandle.Alloc(screens);
        try
        {
            EnumDisplayMonitors(0, 0, &Collect, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return screens;
    }

    public static ScreenPoint Pointer()
    {
        GetCursorPos(out var point);
        return new ScreenPoint(point.X, point.Y);
    }

    /// <summary>How many pixels a DIP takes on the screen under <paramref name="point"/>.</summary>
    public static double ScaleAt(ScreenPoint point)
    {
        var monitor = MonitorFromPoint(new POINT { X = (int)point.X, Y = (int)point.Y }, MONITOR_DEFAULTTONEAREST);
        return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
    }

    public static ScreenRect ToScreenRect(RECT rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);

    [UnmanagedCallersOnly]
    private static int Collect(nint monitor, nint deviceContext, RECT* clip, nint parameter)
    {
        var screens = (List<Screen>)GCHandle.FromIntPtr(parameter).Target!;
        var info = new MONITORINFO { Size = (uint)sizeof(MONITORINFO) };
        if (GetMonitorInfo(monitor, ref info))
        {
            screens.Add(new Screen(ToScreenRect(info.Monitor), ToScreenRect(info.Work)));
        }
        return 1;
    }
}
