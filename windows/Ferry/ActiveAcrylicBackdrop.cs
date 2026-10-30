using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Ferry;

/// <summary>
/// Acrylic that stays see-through when the window isn't active. The built-in
/// backdrops turn solid when their window loses focus, and the shelf never
/// has focus.
/// </summary>
internal sealed partial class ActiveAcrylicBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? controller;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (!DesktopAcrylicController.IsSupported())
        {
            return;
        }
        var configuration = new SystemBackdropConfiguration { IsInputActive = true };
        controller = new DesktopAcrylicController();
        controller.AddSystemBackdropTarget(connectedTarget);
        controller.SetSystemBackdropConfiguration(configuration);
        if (xamlRoot.Content is FrameworkElement root)
        {
            configuration.Theme = ThemeOf(root);
            root.ActualThemeChanged += (element, _) => configuration.Theme = ThemeOf(element);
        }
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        controller?.Dispose();
        controller = null;
    }

    private static SystemBackdropTheme ThemeOf(FrameworkElement element) => element.ActualTheme switch
    {
        ElementTheme.Dark => SystemBackdropTheme.Dark,
        ElementTheme.Light => SystemBackdropTheme.Light,
        _ => SystemBackdropTheme.Default,
    };
}
