using Ferry.Core;
using Ferry.Interop;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using static Ferry.Interop.NativeMethods;

namespace Ferry;

/// <summary>
/// The shelf: a small floating window with a drop zone that keeps files until
/// they're dragged out. It never activates, so the app you're working in keeps
/// the keyboard, and as a tool window it shows on every virtual desktop.
/// </summary>
internal sealed partial class ShelfWindow : Window
{
    /// <summary>The window's size in DIPs.</summary>
    public static readonly Size DipSize = new(208, 232);

    private const string DropGlyph = "";
    private const string ListGlyph = "";
    private const string PileGlyph = "";
    private const string CloseGlyph = "";
    private const double SecondaryOpacity = 0.62;
    private static readonly double[] CardAngles = [0, -9, 8];
    private static readonly double[] CardOffsets = [0, -18, 18];

    private readonly Shelf shelf;
    private readonly Thumbnails thumbnails = new();
    private readonly DropTarget dropTarget;

    private readonly Grid root;
    private readonly Grid zone;
    private readonly Rectangle outline;
    private readonly StackPanel prompt;
    private readonly FontIcon promptIcon;
    private readonly TextBlock promptText;
    private readonly Grid pile;
    private readonly ListView list;
    private readonly Border badge;
    private readonly TextBlock badgeText;
    private readonly TextBlock title;
    private readonly TextBlock subtitle;
    private readonly Button listButton;
    private readonly FontIcon listIcon;
    private readonly Button closeButton;

    private bool isTargeted;
    private bool isExpanded;
    /// <summary>Where a press that may turn into dragging files out began, in DIPs.</summary>
    private Point? pressedAt;
    /// <summary>The list row that press was on, or none for the pile.</summary>
    private ListViewItem? pressedRow;
    /// <summary>A drag out ends with a release that XAML may still read as a tap.</summary>
    private bool skipsNextTap;
    private IReadOnlyCollection<string> draggingOut = [];

    public ShelfWindow(Shelf shelf)
    {
        this.shelf = shelf;

        outline = new Rectangle { RadiusX = 12, RadiusY = 12, StrokeThickness = 1.5 };
        promptIcon = new FontIcon { Glyph = DropGlyph, FontSize = 30 };
        promptText = new TextBlock { FontSize = 12, FontWeight = FontWeights.Medium, Opacity = SecondaryOpacity, HorizontalAlignment = HorizontalAlignment.Center };
        prompt = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { promptIcon, promptText },
        };
        pile = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Extended,
            CanReorderItems = false,
            Padding = new Thickness(0, 4, 0, 4),
        };
        badgeText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge = new Border
        {
            Child = badgeText,
            MinWidth = 20,
            Height = 20,
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(AccentColor),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8),
            IsHitTestVisible = false,
        };
        zone = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Children = { outline, prompt, pile, list, badge },
        };

        title = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        subtitle = new TextBlock { FontSize = 11, Opacity = SecondaryOpacity, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        listIcon = new FontIcon { Glyph = ListGlyph, FontSize = 10 };
        listButton = FooterButton(listIcon);
        closeButton = FooterButton(new FontIcon { Glyph = CloseGlyph, FontSize = 9 });
        var labels = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { title, subtitle } };
        var footer = new Grid { ColumnSpacing = 6, Padding = new Thickness(4, 0, 2, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(labels);
        footer.Children.Add(listButton);
        footer.Children.Add(closeButton);
        Grid.SetColumn(listButton, 1);
        Grid.SetColumn(closeButton, 2);

        root = new Grid
        {
            Padding = new Thickness(10),
            RowSpacing = 10,
            // Transparent rather than empty, so the edge can be grabbed to move the shelf.
            Background = new SolidColorBrush(Colors.Transparent),
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(zone);
        root.Children.Add(footer);
        Grid.SetRow(footer, 1);

        Content = root;
        SystemBackdrop = new ActiveAcrylicBackdrop();
        Title = "Ferry";
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetUpWindow();

        root.PointerPressed += OnRootPointerPressed;
        root.ActualThemeChanged += (_, _) => Render();
        zone.Tapped += OnZoneTapped;
        zone.DoubleTapped += OnZoneDoubleTapped;
        zone.RightTapped += OnZoneRightTapped;
        // Rows handle their own presses, so listen to handled ones too.
        zone.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnZonePointerPressed), handledEventsToo: true);
        zone.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnZonePointerMoved), handledEventsToo: true);
        zone.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => pressedAt = null), handledEventsToo: true);
        list.SelectionChanged += (_, _) => RenderFooter();
        list.DoubleTapped += OnListDoubleTapped;
        list.RightTapped += OnListRightTapped;
        listButton.Click += (_, _) => SetExpanded(!ShowsList);
        closeButton.Click += (_, _) => CloseClicked?.Invoke();

        dropTarget = new DropTarget { Name = "Shelf", CanAccept = data => !ShellFiles.IsDraggingOut && DroppedFiles.HasFiles(data) };
        dropTarget.TargetingChanged += isOn =>
        {
            isTargeted = isOn;
            Render();
        };
        dropTarget.Dropped += data =>
        {
            var paths = DroppedFiles.Read(data);
            Log.Write($"Shelf: {paths.Count} file(s) dropped");
            shelf.Add(paths);
        };
        dropTarget.Register(Handle);

        shelf.Changed += ItemsChanged;
        ItemsChanged();
    }

    public nint Handle { get; }

    /// <summary>The close button: clears the shelf, or just closes an empty one.</summary>
    public event Action? CloseClicked;

    /// <summary>The user dragged the shelf somewhere else by its edge.</summary>
    public event Action? MovedByUser;

    /// <summary>The window's frame on screen, in pixels.</summary>
    public ScreenRect Frame
    {
        get
        {
            GetWindowRect(Handle, out var rect);
            return Monitors.ToScreenRect(rect);
        }
    }

    private bool ShowsList => isExpanded && shelf.Items.Count > 1;

    private static Color AccentColor =>
        Application.Current.Resources.TryGetValue("SystemAccentColor", out var color) && color is Color accent
            ? accent
            : ColorHelper.FromArgb(255, 0, 120, 212);

    /// <summary>
    /// Moves the window to <paramref name="frame"/>. Crossing to a screen with
    /// a different scale makes Windows resize it on the way, so the size is
    /// set again for the scale it ends up at.
    /// </summary>
    public void SetFrame(ScreenRect frame)
    {
        SetWindowPos(Handle, 0, (int)frame.X, (int)frame.Y, (int)frame.Width, (int)frame.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        var scale = GetDpiForWindow(Handle) / 96.0;
        var width = (int)Math.Round(DipSize.Width * scale);
        var height = (int)Math.Round(DipSize.Height * scale);
        var actual = Frame;
        if ((int)actual.Width != width || (int)actual.Height != height || (int)actual.X != (int)frame.X || (int)actual.Y != (int)frame.Y)
        {
            SetWindowPos(Handle, 0, (int)frame.X, (int)frame.Y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    public void ShowWithoutActivating()
    {
        AppWindow.Show(activateWindow: false);
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        // WinUI may have added child windows since last time; they need the drop target too.
        dropTarget.Register(Handle);
        FadeIn();
    }

    public void HideWindow() => AppWindow.Hide();

    private void SetUpWindow()
    {
        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(true, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        // Alt+F4 would destroy the window for good; the shelf only ever hides.
        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            HideWindow();
        };
        var style = GetWindowLongPtr(Handle, GWL_EXSTYLE);
        SetWindowLongPtr(Handle, GWL_EXSTYLE, style | (nint)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, sizeof(int));
    }

    private void FadeIn()
    {
        var visual = ElementCompositionPreview.GetElementVisual(root);
        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(180);
        visual.StartAnimation("Opacity", fade);
    }

    // MARK: - Rendering

    private void ItemsChanged()
    {
        if (shelf.Items.Count < 2)
        {
            isExpanded = false;
        }
        BuildPile();
        BuildList();
        Render();
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;
        list.SelectedItems.Clear();
        Render();
    }

    private void Render()
    {
        var count = shelf.Items.Count;
        var showsList = ShowsList;
        var accent = AccentColor;

        outline.Fill = new SolidColorBrush(isTargeted ? WithAlpha(accent, 0.16) : Neutral(0.06));
        outline.Stroke = new SolidColorBrush(isTargeted ? accent : count == 0 ? Neutral(0.35) : Colors.Transparent);
        outline.StrokeDashArray = count == 0 && !isTargeted ? new DoubleCollection { 4, 3 } : null;
        zone.RenderTransform = new ScaleTransform { ScaleX = isTargeted ? 1.03 : 1, ScaleY = isTargeted ? 1.03 : 1 };

        prompt.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        promptText.Text = isTargeted ? "Release to add" : "Drop files here";
        if (isTargeted)
        {
            promptIcon.Foreground = new SolidColorBrush(accent);
        }
        else
        {
            promptIcon.ClearValue(IconElement.ForegroundProperty);
        }

        pile.Visibility = count > 0 && !showsList ? Visibility.Visible : Visibility.Collapsed;
        pile.Opacity = draggingOut.Count > 0 ? 0.35 : 1;
        list.Visibility = showsList ? Visibility.Visible : Visibility.Collapsed;
        badge.Visibility = count > 1 && !showsList ? Visibility.Visible : Visibility.Collapsed;
        badgeText.Text = count.ToString();

        listButton.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        listIcon.Glyph = showsList ? PileGlyph : ListGlyph;
        ToolTipService.SetToolTip(listButton, showsList ? "Show as pile" : "Show files");
        ToolTipService.SetToolTip(closeButton, count == 0 ? "Close" : "Clear shelf");
        RenderFooter();
    }

    private void RenderFooter()
    {
        var items = shelf.Items;
        var selected = ShowsList ? SelectedItems() : [];
        title.Text = selected.Count > 0
            ? $"{selected.Count} of {items.Count} selected"
            : items.Count switch
            {
                0 => "Ferry",
                1 => items[0].Name,
                _ => ItemText.Count(items.Count),
            };
        subtitle.Text = items.Count switch
        {
            0 => "Drop, switch, drag out",
            1 => ItemText.Detail(items[0]),
            _ when !ShowsList => ItemText.Summary(items),
            _ => selected.Count == 0 ? "Select files to drag" : ItemText.Summary(selected),
        };
    }

    /// <summary>Up to three cards fanned out like a small pile, newest on top.</summary>
    private void BuildPile()
    {
        pile.Children.Clear();
        var visible = shelf.Items.TakeLast(3).ToList();
        for (var index = 0; index < visible.Count; index++)
        {
            var depth = visible.Count - 1 - index;
            var card = Card(visible[index].Path, 100, 6, 2);
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = new CompositeTransform
            {
                Rotation = CardAngles[depth],
                TranslateX = CardOffsets[depth],
                TranslateY = depth == 0 ? 0 : 3,
            };
            pile.Children.Add(card);
        }
    }

    /// <summary>One row per file, newest first, keeping the selection.</summary>
    private void BuildList()
    {
        var selected = SelectedPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        list.Items.Clear();
        foreach (var item in shelf.Items.Reverse())
        {
            var row = Row(item);
            list.Items.Add(row);
            row.IsSelected = selected.Contains(item.Path);
        }
    }

    private ListViewItem Row(ShelfItem item)
    {
        var name = new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        var detail = new TextBlock { Text = ItemText.Detail(item), FontSize = 10, Opacity = SecondaryOpacity, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, detail } };
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(Card(item.Path, 26, 3, 1));
        content.Children.Add(labels);
        Grid.SetColumn(labels, 1);
        ToolTipService.SetToolTip(content, item.Path);
        return new ListViewItem
        {
            Content = content,
            Tag = item.Path,
            MinHeight = 36,
            Padding = new Thickness(6, 2, 8, 2),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
    }

    /// <summary>
    /// A file's thumbnail: a preview gets rounded corners and a white rim like
    /// a photo, an icon stays as it is.
    /// </summary>
    private FrameworkElement Card(string path, double side, double radius, double rim)
    {
        var icon = new Image { Width = side, Height = side, Stretch = Stretch.Uniform };
        var card = new Border { Width = side, Height = side, CornerRadius = new CornerRadius(radius), Child = icon };

        void Show(Thumbnail thumbnail)
        {
            if (thumbnail.IsPreview)
            {
                card.Child = null;
                card.Background = new ImageBrush { ImageSource = thumbnail.Image, Stretch = Stretch.UniformToFill };
                card.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(230, 255, 255, 255));
                card.BorderThickness = new Thickness(rim);
            }
            else
            {
                icon.Source = thumbnail.Image;
            }
        }

        async void Load()
        {
            if (await thumbnails.LoadAsync(path) is { } thumbnail)
            {
                Show(thumbnail);
            }
        }

        if (thumbnails.Cached(path) is { } cached)
        {
            Show(cached);
        }
        else
        {
            Load();
        }
        return card;
    }

    // MARK: - Pile

    private void OnZoneTapped(object sender, TappedRoutedEventArgs e)
    {
        if (skipsNextTap)
        {
            skipsNextTap = false;
            return;
        }
        // Clicking the pile lists the files.
        if (!ShowsList && shelf.Items.Count > 1)
        {
            SetExpanded(true);
        }
    }

    private void OnZoneDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!ShowsList && shelf.Items.Count == 1)
        {
            ShellFiles.Open(shelf.Paths);
        }
    }

    private void OnZoneRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var paths = shelf.Paths.ToList();
        if (ShowsList || paths.Count == 0)
        {
            return;
        }
        e.Handled = true;
        var commands = FileCommands(paths);
        if (paths.Count > 1)
        {
            commands.Add(new MenuCommand("Show files", () => SetExpanded(true)));
        }
        commands.Add(new MenuCommand("Clear shelf", shelf.RemoveAll));
        ShowMenu(commands);
    }

    // MARK: - Dragging out

    private void OnZonePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        pressedAt = null;
        var point = e.GetCurrentPoint(root);
        if (!point.Properties.IsLeftButtonPressed || shelf.IsEmpty)
        {
            return;
        }
        // In the list, only a row starts a drag, not the scroll bar or the gaps.
        pressedRow = ShowsList ? RowAt(e.OriginalSource) : null;
        if (!ShowsList || pressedRow is not null)
        {
            pressedAt = point.Position;
            skipsNextTap = false;
        }
    }

    /// <summary>
    /// The pile drags everything at once. Dragging a selected row takes the
    /// whole selection along, in list order; any other row goes on its own.
    /// </summary>
    private void OnZonePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (pressedAt is not { } start)
        {
            return;
        }
        var point = e.GetCurrentPoint(root);
        if (!point.Properties.IsLeftButtonPressed)
        {
            pressedAt = null;
            return;
        }
        var scale = GetDpiForWindow(Handle) / 96.0;
        var threshold = GetSystemMetrics(SM_CXDRAG) / scale;
        if (Math.Abs(point.Position.X - start.X) <= threshold && Math.Abs(point.Position.Y - start.Y) <= threshold)
        {
            return;
        }
        pressedAt = null;
        skipsNextTap = true;
        var paths = pressedRow switch
        {
            null => shelf.Paths.ToList(),
            { IsSelected: true } => SelectedPaths().ToList(),
            var row => [PathOf(row)],
        };
        DragOut(paths);
    }

    // MARK: - File list

    private void OnListDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (RowAt(e.OriginalSource) is { } row)
        {
            ShellFiles.Open([PathOf(row)]);
        }
    }

    private void OnListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (RowAt(e.OriginalSource) is not { } row)
        {
            return;
        }
        e.Handled = true;
        var paths = row.IsSelected ? SelectedPaths().ToList() : [PathOf(row)];
        var commands = FileCommands(paths);
        commands.Add(new MenuCommand("Select all", list.SelectAll));
        commands.Add(new MenuCommand("Remove from shelf", () => shelf.Remove(paths)));
        ShowMenu(commands);
    }

    private static ListViewItem? RowAt(object source)
    {
        for (var element = source as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ListViewItem row)
            {
                return row;
            }
        }
        return null;
    }

    private static string PathOf(ListViewItem row) => (string)row.Tag;

    /// <summary>Selected rows, in list order.</summary>
    private IEnumerable<string> SelectedPaths() =>
        list.Items.OfType<ListViewItem>().Where(row => row.IsSelected).Select(PathOf);

    private List<ShelfItem> SelectedItems()
    {
        var selected = SelectedPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return shelf.Items.Where(item => selected.Contains(item.Path)).ToList();
    }

    // MARK: - Shared

    /// <summary>Open and Show in File Explorer, followed by a separator for extra items.</summary>
    private static List<MenuCommand> FileCommands(IReadOnlyList<string> paths) =>
    [
        new MenuCommand("Open", () => ShellFiles.Open(paths)),
        new MenuCommand("Show in File Explorer", () => ShellFiles.ShowInExplorer(paths)),
        MenuCommand.Separator,
    ];

    private void ShowMenu(IReadOnlyList<MenuCommand> commands)
    {
        GetCursorPos(out var point);
        NativeMenu.Show(Handle, point, commands);
    }

    /// <summary>
    /// Drags files out with File Explorer's own drag and drop, which moves or
    /// copies them like a drag out of a folder window.
    /// </summary>
    private void DragOut(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }
        // Start it once XAML is done with the pointer event that asked for it.
        DispatcherQueue.TryEnqueue(() =>
        {
            draggingOut = paths;
            Render();
            var effect = ShellFiles.DragOut(Handle, paths);
            draggingOut = [];
            // Files leave the shelf as soon as they land somewhere else.
            if (effect != DROPEFFECT_NONE)
            {
                shelf.Remove(paths);
            }
            // Explorer reports some moves as no effect at all; those files are gone now.
            shelf.PruneMissing();
            Render();
        });
    }

    /// <summary>Lets the user reposition the shelf by dragging its edge.</summary>
    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource;
        var isEdge = ReferenceEquals(source, root) || ReferenceEquals(source, title) || ReferenceEquals(source, subtitle);
        if (!isEdge || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        var before = Frame;
        ReleaseCapture();
        // Runs Windows' own move loop and returns when the button goes up.
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        if (Frame.Origin != before.Origin)
        {
            MovedByUser?.Invoke();
        }
    }

    private static Button FooterButton(FontIcon icon) => new()
    {
        Content = icon,
        Width = 24,
        Height = 24,
        Padding = new Thickness(0),
        CornerRadius = new CornerRadius(12),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A grey that reads on both light and dark acrylic.</summary>
    private static Color Neutral(double opacity) => ColorHelper.FromArgb((byte)(opacity * 255), 128, 128, 128);

    private static Color WithAlpha(Color color, double opacity) => ColorHelper.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);
}
