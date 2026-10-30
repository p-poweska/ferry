using Ferry.Core;
using Ferry.Interop;

namespace Ferry;

/// <summary>Shows, hides and positions the shelf window.</summary>
internal sealed class ShelfController
{
    private readonly Shelf shelf;
    private readonly Settings settings;
    private readonly ShelfWindow window;
    private readonly VirtualDesktopFollower follower;
    /// <summary>Popped up by a file drag elsewhere rather than opened by the user.</summary>
    private bool isShownForDrag;
    /// <summary>Set when the user drags the shelf around, as opposed to Ferry moving it.</summary>
    private bool wasMovedByUser;
    private CancellationTokenSource? hideAfterDrag;

    public ShelfController(Shelf shelf, Settings settings)
    {
        this.shelf = shelf;
        this.settings = settings;
        window = new ShelfWindow(shelf);
        window.CloseClicked += () =>
        {
            shelf.RemoveAll();
            Hide();
        };
        window.MovedByUser += SaveCustomSpot;
        follower = new VirtualDesktopFollower(window.Handle, window.DispatcherQueue);
    }

    public bool IsVisible { get; private set; }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        shelf.PruneMissing();
        if (IsVisible)
        {
            return;
        }
        IsVisible = true;
        wasMovedByUser = false;
        window.SetFrame(TargetFrame());
        window.ShowWithoutActivating();
        follower.Start();
    }

    public void Hide()
    {
        if (!IsVisible)
        {
            return;
        }
        IsVisible = false;
        isShownForDrag = false;
        follower.Stop();
        window.HideWindow();
    }

    public void FileDragBegan()
    {
        // The next drag started before the last one's shelf went away.
        hideAfterDrag?.Cancel();
        if (IsVisible)
        {
            return;
        }
        isShownForDrag = true;
        Show();
    }

    public async void FileDragEnded()
    {
        if (!isShownForDrag)
        {
            return;
        }
        // The drop reaches the shelf a moment after the button goes up, so
        // wait before deciding the shelf wasn't used.
        hideAfterDrag?.Cancel();
        hideAfterDrag = new CancellationTokenSource();
        try
        {
            await Task.Delay(400, hideAfterDrag.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (isShownForDrag)
        {
            isShownForDrag = false;
            HideIfUnused();
        }
    }

    public void ShelfDidChange() => HideIfUnused();

    /// <summary>Moves a visible shelf to its new spot as soon as the setting changes.</summary>
    public void PositionDidChange()
    {
        // Dragging the shelf around picks "Your spot", and it's already there.
        if (!IsVisible || (settings.Position == ShelfPosition.Custom && wasMovedByUser))
        {
            return;
        }
        wasMovedByUser = false;
        window.SetFrame(TargetFrame());
    }

    private void HideIfUnused()
    {
        if (shelf.IsEmpty)
        {
            Hide();
        }
    }

    private ScreenRect TargetFrame()
    {
        var mouse = Monitors.Pointer();
        var scale = Monitors.ScaleAt(mouse);
        var size = new ScreenSize(ShelfWindow.DipSize.Width * scale, ShelfWindow.DipSize.Height * scale);
        return settings.Position.Frame(size, mouse, settings.CustomOrigin, Monitors.All(), scale);
    }

    private void SaveCustomSpot()
    {
        wasMovedByUser = true;
        settings.CustomOrigin = window.Frame.Origin;
        settings.Position = ShelfPosition.Custom;
    }
}
