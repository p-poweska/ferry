using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class ShelfPositionTests
{
    private static readonly ScreenSize Size = new(200, 240);

    /// <summary>A 1920×1080 screen with a 48 px taskbar along the bottom.</summary>
    private static readonly Screen Main = new(new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1032));

    /// <summary>A second screen to the right of the main one.</summary>
    private static readonly Screen Side = new(new ScreenRect(1920, 0, 1280, 1024), new ScreenRect(1920, 0, 1280, 1024));

    private static readonly Screen[] Screens = [Main, Side];

    [Fact]
    public void SitsInTheMiddleOfTheChosenEdge()
    {
        var mouse = new ScreenPoint(900, 500);

        Assert.Equal(new ScreenRect(1704, 396, 200, 240), ShelfPosition.RightEdge.Frame(Size, mouse, null, Screens));
        Assert.Equal(new ScreenRect(16, 396, 200, 240), ShelfPosition.LeftEdge.Frame(Size, mouse, null, Screens));
        Assert.Equal(new ScreenRect(860, 16, 200, 240), ShelfPosition.TopEdge.Frame(Size, mouse, null, Screens));
        Assert.Equal(new ScreenRect(860, 776, 200, 240), ShelfPosition.BottomEdge.Frame(Size, mouse, null, Screens));
    }

    [Fact]
    public void UsesTheScreenWithThePointer()
    {
        var frame = ShelfPosition.RightEdge.Frame(Size, new ScreenPoint(2500, 100), null, Screens);

        Assert.Equal(new ScreenRect(2984, 392, 200, 240), frame);
    }

    [Fact]
    public void ScalesTheMarginsWithTheScreen()
    {
        var frame = ShelfPosition.LeftEdge.Frame(Size, new ScreenPoint(900, 500), null, Screens, scale: 1.5);

        Assert.Equal(24, frame.X);
    }

    [Fact]
    public void GoesBesideThePointer()
    {
        var right = ShelfPosition.Pointer.Frame(Size, new ScreenPoint(500, 500), null, Screens);
        var nearTheEdge = ShelfPosition.Pointer.Frame(Size, new ScreenPoint(1850, 500), null, Screens);

        Assert.Equal(new ScreenRect(540, 380, 200, 240), right);
        Assert.Equal(new ScreenRect(1610, 380, 200, 240), nearTheEdge);
    }

    [Fact]
    public void NeverOpensUnderThePointer()
    {
        // The pointer is right where the shelf would sit on the right edge.
        var mouse = new ScreenPoint(1800, 450);

        var frame = ShelfPosition.RightEdge.Frame(Size, mouse, null, Screens);

        Assert.False(frame.Inflate(ShelfPlacement.PointerGap - 1).Contains(mouse));
        Assert.Equal(1704, frame.X);
        Assert.Equal(mouse.Y + ShelfPlacement.PointerGap, frame.Y);
    }

    [Fact]
    public void DodgesAlongTheEdgeToTheNearerSide()
    {
        var mouse = new ScreenPoint(960, 900);

        var frame = ShelfPosition.BottomEdge.Frame(Size, mouse, null, Screens);

        Assert.Equal(new ScreenRect(720, 776, 200, 240), frame);
        Assert.False(frame.Inflate(ShelfPlacement.PointerGap - 1).Contains(mouse));
    }

    [Fact]
    public void KeepsACustomSpotThatStillFits()
    {
        var origin = new ScreenPoint(300, 200);

        var frame = ShelfPosition.Custom.Frame(Size, new ScreenPoint(1500, 900), origin, Screens);

        Assert.Equal(new ScreenRect(origin, Size), frame);
    }

    [Fact]
    public void FallsBackToTheRightEdgeWhenTheCustomSpotIsGone()
    {
        var offScreen = new ScreenPoint(5000, 200);

        var frame = ShelfPosition.Custom.Frame(Size, new ScreenPoint(900, 500), offScreen, Screens);

        Assert.Equal(1704, frame.X);
    }

    [Fact]
    public void MovesACustomSpotAwayFromThePointer()
    {
        var origin = new ScreenPoint(300, 200);

        var frame = ShelfPosition.Custom.Frame(Size, new ScreenPoint(350, 250), origin, Screens);

        Assert.False(frame.Inflate(ShelfPlacement.PointerGap - 1).Contains(new ScreenPoint(350, 250)));
    }

    [Fact]
    public void WatchesOnlyTheOuterEdgesForDrags()
    {
        Assert.Equal([new ScreenRect(3198, 0, 2, 1024)], ShelfPosition.RightEdge.DragStrips(Screens, 2));
        Assert.Equal([new ScreenRect(0, 0, 2, 1080)], ShelfPosition.LeftEdge.DragStrips(Screens, 2));
        Assert.Equal(
            [new ScreenRect(0, 1078, 1920, 2), new ScreenRect(1920, 1022, 1280, 2)],
            ShelfPosition.BottomEdge.DragStrips(Screens, 2));
    }

    [Fact]
    public void WatchesBothSidesForAShelfBesideThePointer()
    {
        Assert.Equal(
            [new ScreenRect(0, 0, 2, 1080), new ScreenRect(3198, 0, 2, 1024)],
            ShelfPosition.Pointer.DragStrips(Screens, 2));
    }
}
