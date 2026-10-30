using Ferry.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Ferry;

/// <summary>The settings window: show while dragging, the shortcut, the position, launch at login.</summary>
internal sealed partial class SettingsWindow : Window
{
    private readonly Settings settings;
    private readonly HotKey hotKey;
    private readonly ShortcutRecorder recorder;
    private readonly TextBlock shortcutDescription;
    private readonly RadioButtons positions;

    public SettingsWindow(Settings settings, HotKey hotKey, string iconPath)
    {
        this.settings = settings;
        this.hotKey = hotKey;
        Title = "Ferry Settings";
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(iconPath);
        var scale = Interop.NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(520 * scale), (int)(600 * scale)));

        var showWhileDragging = new ToggleSwitch { IsOn = settings.ShowWhileDragging, MinWidth = 0 };
        showWhileDragging.Toggled += (_, _) => settings.ShowWhileDragging = showWhileDragging.IsOn;

        recorder = new ShortcutRecorder(settings);
        recorder.RecordingChanged += isRecording => hotKey.IsPaused = isRecording;

        positions = new RadioButtons { MaxColumns = 3, Margin = new Thickness(0, 8, 0, 0) };
        FillPositions();
        positions.SelectionChanged += (_, _) =>
        {
            if (positions.SelectedItem is RadioButton { Tag: ShelfPosition position })
            {
                settings.Position = position;
            }
        };

        var launchAtLogin = new ToggleSwitch { IsOn = LaunchAtLogin.IsEnabled, MinWidth = 0 };
        launchAtLogin.Toggled += (_, _) =>
        {
            LaunchAtLogin.Set(launchAtLogin.IsOn);
            // Read it back: writing to the registry can fail.
            launchAtLogin.IsOn = LaunchAtLogin.IsEnabled;
        };

        shortcutDescription = Description("");
        var content = new StackPanel
        {
            Spacing = 4,
            Padding = new Thickness(24, 20, 24, 24),
            MaxWidth = 640,
            Children =
            {
                Heading("Shelf"),
                Card("Show while dragging files", Description("Slides in when you drag files to the edge of the screen where it lives."), showWhileDragging),
                Card("Keyboard shortcut", shortcutDescription, recorder.View),
                Card(
                    "Position",
                    Description("Where the shelf slides in. Drag it by its edge to put it anywhere else."),
                    null,
                    positions),
                Heading("System"),
                Card("Launch at login", Description("Starts Ferry when you sign in to Windows."), launchAtLogin),
                new TextBlock
                {
                    Text = $"Ferry {typeof(App).Assembly.GetName().Version?.ToString(3)}",
                    Opacity = 0.6,
                    FontSize = 12,
                    Margin = new Thickness(2, 12, 0, 0),
                },
            },
        };
        Content = new ScrollViewer { Content = content };

        UpdateShortcutDescription();
        settings.Changed += OnSettingChanged;
        hotKey.StateChanged += UpdateShortcutDescription;
        Closed += (_, _) =>
        {
            settings.Changed -= OnSettingChanged;
            hotKey.StateChanged -= UpdateShortcutDescription;
            hotKey.IsPaused = false;
        };
    }

    private void OnSettingChanged(Settings.Setting setting)
    {
        switch (setting)
        {
            case Settings.Setting.Shortcut:
                recorder.Update();
                break;
            case Settings.Setting.Position:
                // Dragging the shelf around picks "Your spot" while this is open.
                DispatcherQueue.TryEnqueue(FillPositions);
                break;
        }
    }

    private void FillPositions()
    {
        var options = Enum.GetValues<ShelfPosition>()
            .Where(position => position != ShelfPosition.Custom || settings.CustomOrigin is not null)
            .ToList();
        if (positions.Items.Count != options.Count)
        {
            positions.Items.Clear();
            foreach (var position in options)
            {
                positions.Items.Add(new RadioButton { Content = position.Title(), Tag = position });
            }
        }
        positions.SelectedIndex = options.IndexOf(settings.Position);
    }

    private void UpdateShortcutDescription()
    {
        if (hotKey.IsTaken)
        {
            shortcutDescription.Text = "Another app is already using this shortcut.";
            shortcutDescription.Foreground = Resource<Brush>("SystemFillColorCriticalBrush");
            shortcutDescription.Opacity = 1;
        }
        else
        {
            shortcutDescription.Text = "Shows or hides the shelf from any app. Backspace turns it off.";
            shortcutDescription.ClearValue(TextBlock.ForegroundProperty);
            shortcutDescription.Opacity = 0.7;
        }
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(2, 12, 0, 4),
    };

    private static TextBlock Description(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.7,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A Windows 11 settings card: a title and description, the control on the right, anything else below.</summary>
    private static Border Card(string title, TextBlock description, FrameworkElement? control, UIElement? below = null)
    {
        var labels = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }, description },
        };
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(labels);
        if (control is not null)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(control);
            Grid.SetColumn(control, 1);
        }
        var stack = new StackPanel { Children = { row } };
        if (below is not null)
        {
            stack.Children.Add(below);
        }
        return new Border
        {
            Child = stack,
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = Resource<object>("ControlCornerRadius") is CornerRadius radius ? radius : new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Background = Resource<Brush>("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Resource<Brush>("CardStrokeColorDefaultBrush"),
        };
    }

    private static T? Resource<T>(string key) where T : class =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as T : null;
}
