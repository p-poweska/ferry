using Ferry.Core;
using Ferry.Interop;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>The global shortcut that shows and hides the shelf from any app.</summary>
internal sealed class HotKey : IDisposable
{
    private const int Id = 1;
    private readonly MessageWindow window;
    private KeyShortcut? shortcut;

    public HotKey(MessageWindow window)
    {
        this.window = window;
        window.MessageReceived += OnMessage;
    }

    public event Action? Pressed;

    /// <summary>Raised when <see cref="IsTaken"/> changes.</summary>
    public event Action? StateChanged;

    /// <summary>Another app registered the same shortcut first.</summary>
    public bool IsTaken { get; private set; }

    /// <summary>Lets the shortcut through to the settings window while a new one is being typed.</summary>
    public bool IsPaused
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Apply();
            }
        }
    }

    /// <summary>Registers <paramref name="shortcut"/>, or turns the shortcut off with <c>null</c>.</summary>
    public void Register(KeyShortcut? shortcut)
    {
        this.shortcut = shortcut;
        Apply();
    }

    public void Dispose()
    {
        window.MessageReceived -= OnMessage;
        UnregisterHotKey(window.Handle, Id);
    }

    private void Apply()
    {
        UnregisterHotKey(window.Handle, Id);
        if (IsPaused)
        {
            return;
        }
        var isTaken = shortcut is { } keys
            && !RegisterHotKey(window.Handle, Id, (uint)keys.Modifiers | MOD_NOREPEAT, (uint)keys.Key);
        if (isTaken != IsTaken)
        {
            IsTaken = isTaken;
            StateChanged?.Invoke();
        }
    }

    private void OnMessage(uint message, nint wParam, nint lParam)
    {
        if (message == WM_HOTKEY && wParam == Id)
        {
            Pressed?.Invoke();
        }
    }
}
