using Ferry.Core;
using Ferry.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>Ties the shelf, the tray icon, the shortcut and the drag watcher together.</summary>
public partial class App : Application
{
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ferry", "settings.json");
    private static readonly string IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Ferry.ico");

    private readonly Settings settings = Settings.Load(SettingsPath);
    private readonly Shelf shelf = new(new ShellFileDescriber());
    private MessageWindow messages = null!;
    private ShelfController panel = null!;
    private TrayIcon tray = null!;
    private HotKey hotKey = null!;
    private DragWatcher dragWatcher = null!;
    private SettingsWindow? settingsWindow;

    public App()
    {
        InitializeComponent();
        // Ferry lives in the tray; closing the settings window mustn't quit it.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Inbox.Temporary.Empty();
        NativeMenu.AllowDarkMode();
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        messages = new MessageWindow();
        panel = new ShelfController(shelf, settings);
        tray = new TrayIcon(messages, IconPath);
        tray.Clicked += panel.Toggle;
        tray.MenuRequested += ShowTrayMenu;

        shelf.Changed += () =>
        {
            RefreshTray();
            panel.ShelfDidChange();
        };

        dragWatcher = new DragWatcher(dispatcher) { Position = settings.Position };
        dragWatcher.FileDragBegan += panel.FileDragBegan;
        dragWatcher.FileDragEnded += panel.FileDragEnded;
        dragWatcher.IsEnabled = settings.ShowWhileDragging;

        hotKey = new HotKey(messages);
        hotKey.Pressed += panel.Toggle;
        hotKey.Register(settings.Shortcut);

        settings.Changed += Apply;
        // Starting Ferry again while it's running brings up the shelf.
        Program.Instance.ShowRequested += () => dispatcher.TryEnqueue(panel.Show);
    }

    private void Apply(Settings.Setting setting)
    {
        switch (setting)
        {
            case Settings.Setting.ShowWhileDragging:
                dragWatcher.IsEnabled = settings.ShowWhileDragging;
                break;
            case Settings.Setting.Position:
                dragWatcher.Position = settings.Position;
                panel.PositionDidChange();
                break;
            case Settings.Setting.Shortcut:
                hotKey.Register(settings.Shortcut);
                break;
        }
    }

    private void RefreshTray()
    {
        var count = shelf.Items.Count;
        tray.ToolTip = count > 0 ? $"Ferry — {ItemText.Count(count)} on the shelf" : "Ferry";
    }

    private void ShowTrayMenu(POINT point)
    {
        shelf.PruneMissing();
        var count = shelf.Items.Count;
        var toggle = panel.IsVisible ? "Hide shelf" : "Show shelf";
        if (settings.Shortcut is { } shortcut)
        {
            // A tab right-aligns the shortcut, like any Windows menu.
            toggle += "\t" + shortcut.ToDisplayString(KeyNames.Name);
        }
        NativeMenu.Show(messages.Handle, point,
        [
            new MenuCommand(count > 0 ? $"{ItemText.Count(count)} on the shelf" : "Shelf is empty"),
            MenuCommand.Separator,
            new MenuCommand(toggle, panel.Toggle),
            new MenuCommand("Clear shelf", shelf.RemoveAll, IsEnabled: count > 0),
            MenuCommand.Separator,
            new MenuCommand("Settings…", ShowSettings),
            new MenuCommand("Quit Ferry", Quit),
        ]);
    }

    private void ShowSettings()
    {
        if (settingsWindow is null)
        {
            settingsWindow = new SettingsWindow(settings, hotKey, IconPath);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }
        settingsWindow.Activate();
    }

    private void Quit()
    {
        settingsWindow?.Close();
        dragWatcher.Dispose();
        hotKey.Dispose();
        tray.Dispose();
        messages.Dispose();
        Exit();
    }
}
