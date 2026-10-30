using Ferry.Interop;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>
/// The icon in the notification area: shows that Ferry is running and how
/// many files are waiting. A click toggles the shelf; a right-click asks for
/// the menu.
/// </summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const uint Id = 1;
    private const uint CallbackMessage = WM_APP + 1;
    private static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    private readonly MessageWindow window;
    private readonly nint icon;

    public TrayIcon(MessageWindow window, string iconPath)
    {
        this.window = window;
        icon = LoadImage(0, iconPath, IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);
        window.MessageReceived += OnMessage;
        Add();
    }

    public event Action? Clicked;

    /// <summary>The menu should open at this point on the screen.</summary>
    public event Action<POINT>? MenuRequested;

    public string ToolTip
    {
        get;
        set
        {
            field = value;
            Shell_NotifyIcon(NIM_MODIFY, Data(NIF_TIP | NIF_SHOWTIP));
        }
    } = "Ferry";

    public void Dispose()
    {
        window.MessageReceived -= OnMessage;
        Shell_NotifyIcon(NIM_DELETE, Data(0));
        DestroyIcon(icon);
    }

    private void Add()
    {
        var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_ADD, data);
        data.Version = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, data);
    }

    private NOTIFYICONDATAW Data(uint flags)
    {
        var data = new NOTIFYICONDATAW
        {
            Size = (uint)sizeof(NOTIFYICONDATAW),
            Window = window.Handle,
            Id = Id,
            Flags = flags,
            CallbackMessage = CallbackMessage,
            Icon = icon,
        };
        var tip = ToolTip.AsSpan(0, Math.Min(ToolTip.Length, 127));
        tip.CopyTo(new Span<char>(data.Tip, 128));
        return data;
    }

    private void OnMessage(uint message, nint wParam, nint lParam)
    {
        if (message == TaskbarCreated)
        {
            // Explorer restarted and forgot every icon.
            Add();
        }
        else if (message == CallbackMessage)
        {
            switch ((uint)(lParam & 0xFFFF))
            {
                case NIN_SELECT:
                case NIN_KEYSELECT:
                    Clicked?.Invoke();
                    break;
                case WM_CONTEXTMENU:
                    MenuRequested?.Invoke(new POINT { X = LowWord(wParam), Y = HighWord(wParam) });
                    break;
            }
        }
    }
}
