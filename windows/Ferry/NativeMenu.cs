using Ferry.Interop;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>An item in a <see cref="NativeMenu"/>. One without an action shows as a dimmed label.</summary>
internal sealed record MenuCommand(string Title, Action? Run = null, bool IsEnabled = true)
{
    public static readonly MenuCommand Separator = new("-");
}

/// <summary>
/// Pop-up menus drawn by Windows itself, for the tray icon and the shelf.
/// They look native on Windows 11 and close when you click anywhere else.
/// </summary>
internal static class NativeMenu
{
    /// <summary>Lets menus follow dark mode. Call once, before the first menu.</summary>
    public static void AllowDarkMode()
    {
        try
        {
            SetPreferredAppMode(1);
        }
        catch (EntryPointNotFoundException)
        {
            // Older Windows: menus stay light.
        }
    }

    /// <summary>
    /// Shows the menu at <paramref name="point"/> and runs the chosen command
    /// after it closes, giving the keyboard back to the app that had it.
    /// </summary>
    public static void Show(nint owner, POINT point, IReadOnlyList<MenuCommand> commands)
    {
        var menu = CreatePopupMenu();
        try
        {
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (ReferenceEquals(command, MenuCommand.Separator))
                {
                    AppendMenu(menu, MF_SEPARATOR, 0, null);
                }
                else
                {
                    var flags = command.IsEnabled && command.Run is not null ? MF_STRING : MF_STRING | MF_GRAYED;
                    AppendMenu(menu, flags, (nuint)(index + 1), command.Title);
                }
            }

            var previous = GetForegroundWindow();
            // Without this the menu doesn't close when you click elsewhere.
            SetForegroundWindow(owner);
            var chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON, point.X, point.Y, owner, 0);
            PostMessage(owner, WM_NULL, 0, 0);
            if (previous != 0)
            {
                SetForegroundWindow(previous);
            }
            if (chosen > 0)
            {
                commands[chosen - 1].Run?.Invoke();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }
}
