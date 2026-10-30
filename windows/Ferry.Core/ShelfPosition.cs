namespace Ferry.Core;

/// <summary>Where the shelf slides in, on the screen with the pointer.</summary>
public enum ShelfPosition
{
    RightEdge,
    LeftEdge,
    TopEdge,
    BottomEdge,
    Pointer,
    /// <summary>Wherever the user last dragged the shelf to.</summary>
    Custom,
}

public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ScreenSize(double Width, double Height);

/// <summary>A rectangle in screen pixels; y grows downwards, as on Windows.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public ScreenRect(ScreenPoint origin, ScreenSize size) : this(origin.X, origin.Y, size.Width, size.Height) { }

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public ScreenPoint Origin => new(X, Y);

    public bool Contains(ScreenPoint point) => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Contains(ScreenRect rect) =>
        rect.Left >= Left && rect.Right <= Right && rect.Top >= Top && rect.Bottom <= Bottom;

    public bool Intersects(ScreenRect rect) =>
        rect.Left < Right && rect.Right > Left && rect.Top < Bottom && rect.Bottom > Top;

    public ScreenRect Inflate(double amount) => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);
}

/// <summary>A monitor: its whole area and the part outside the taskbar.</summary>
public readonly record struct Screen(ScreenRect Bounds, ScreenRect WorkArea);

public static class ShelfPlacement
{
    /// <summary>Room between the shelf and the edge of the screen, in DIPs.</summary>
    public const double Margin = 16;

    /// <summary>
    /// Room between the pointer and the shelf, in DIPs, so a drag that's just
    /// passing by doesn't land on it.
    /// </summary>
    public const double PointerGap = 40;

    public static string Title(this ShelfPosition position) => position switch
    {
        ShelfPosition.RightEdge => "Right",
        ShelfPosition.LeftEdge => "Left",
        ShelfPosition.TopEdge => "Top",
        ShelfPosition.BottomEdge => "Bottom",
        ShelfPosition.Pointer => "Next to the pointer",
        _ => "Your spot",
    };

    /// <summary>The screen under <paramref name="point"/>, or the first one.</summary>
    public static Screen? ScreenAt(ScreenPoint point, IReadOnlyList<Screen> screens) =>
        screens.Where(screen => screen.Bounds.Contains(point)).Cast<Screen?>().FirstOrDefault()
            ?? screens.Cast<Screen?>().FirstOrDefault();

    /// <summary>
    /// The shelf's frame on the screen under <paramref name="mouse"/>. A custom
    /// spot is used as long as it still fits on one of the screens; otherwise
    /// the shelf goes to the right edge. Either way it stays clear of the
    /// pointer. <paramref name="scale"/> turns DIPs into pixels.
    /// </summary>
    public static ScreenRect Frame(
        this ShelfPosition position,
        ScreenSize size,
        ScreenPoint mouse,
        ScreenPoint? customOrigin,
        IReadOnlyList<Screen> screens,
        double scale = 1)
    {
        var area = ScreenAt(mouse, screens)?.WorkArea ?? default;
        var margin = Margin * scale;
        var gap = PointerGap * scale;
        var frame = ChosenFrame(position, size, mouse, customOrigin, screens, area, margin, gap);

        // The image being dragged covers everything around the pointer, so a
        // shelf there would pop up out of sight.
        if (!frame.Inflate(gap).Contains(mouse))
        {
            return frame;
        }
        return position switch
        {
            ShelfPosition.RightEdge or ShelfPosition.LeftEdge =>
                frame with { Y = Dodge(frame.Y, size.Height, mouse.Y, area.Top, area.Bottom, margin, gap) },
            ShelfPosition.TopEdge or ShelfPosition.BottomEdge =>
                frame with { X = Dodge(frame.X, size.Width, mouse.X, area.Left, area.Right, margin, gap) },
            ShelfPosition.Custom => new ScreenRect(BesidePointer(size, mouse, area, margin, gap), size),
            _ => frame,
        };
    }

    /// <summary>
    /// Thin strips along the screen edges that notice a file drag reaching
    /// them: the shelf's own edge of every screen, or both sides for a shelf
    /// that follows the pointer or has a spot of its own. Edges another screen
    /// carries on past are left out, since the pointer only crosses them.
    /// </summary>
    public static IEnumerable<ScreenRect> DragStrips(this ShelfPosition position, IReadOnlyList<Screen> screens, double thickness)
    {
        ShelfPosition[] edges = position switch
        {
            ShelfPosition.RightEdge or ShelfPosition.LeftEdge or ShelfPosition.TopEdge or ShelfPosition.BottomEdge => [position],
            _ => [ShelfPosition.LeftEdge, ShelfPosition.RightEdge],
        };
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            foreach (var edge in edges)
            {
                var (strip, beyond) = edge switch
                {
                    ShelfPosition.LeftEdge => (bounds with { Width = thickness }, bounds with { X = bounds.X - thickness, Width = thickness }),
                    ShelfPosition.TopEdge => (bounds with { Height = thickness }, bounds with { Y = bounds.Y - thickness, Height = thickness }),
                    ShelfPosition.BottomEdge => (bounds with { Y = bounds.Bottom - thickness, Height = thickness }, bounds with { Y = bounds.Bottom, Height = thickness }),
                    _ => (bounds with { X = bounds.Right - thickness, Width = thickness }, bounds with { X = bounds.Right, Width = thickness }),
                };
                if (!screens.Any(other => other != screen && other.Bounds.Intersects(beyond)))
                {
                    yield return strip;
                }
            }
        }
    }

    private static ScreenRect ChosenFrame(
        ShelfPosition position,
        ScreenSize size,
        ScreenPoint mouse,
        ScreenPoint? customOrigin,
        IReadOnlyList<Screen> screens,
        ScreenRect area,
        double margin,
        double gap)
    {
        if (position == ShelfPosition.Custom && customOrigin is { } origin)
        {
            var custom = new ScreenRect(origin, size);
            if (screens.Any(screen => screen.WorkArea.Contains(custom)))
            {
                return custom;
            }
        }

        var centerX = area.Left + (area.Width - size.Width) / 2;
        var centerY = area.Top + (area.Height - size.Height) / 2;
        var chosen = position switch
        {
            ShelfPosition.LeftEdge => new ScreenPoint(area.Left + margin, centerY),
            ShelfPosition.TopEdge => new ScreenPoint(centerX, area.Top + margin),
            ShelfPosition.BottomEdge => new ScreenPoint(centerX, area.Bottom - size.Height - margin),
            ShelfPosition.Pointer => BesidePointer(size, mouse, area, margin, gap),
            _ => new ScreenPoint(area.Right - size.Width - margin, centerY),
        };
        return new ScreenRect(chosen, size);
    }

    /// <summary>To the right of the pointer, or to its left near the right edge.</summary>
    private static ScreenPoint BesidePointer(ScreenSize size, ScreenPoint mouse, ScreenRect area, double margin, double gap)
    {
        var x = mouse.X + gap;
        if (x + size.Width > area.Right - margin)
        {
            x = mouse.X - gap - size.Width;
        }
        x = Clamp(x, area.Left + margin, area.Right - size.Width - margin);
        var y = Clamp(mouse.Y - size.Height / 2, area.Top + margin, area.Bottom - size.Height - margin);
        return new ScreenPoint(x, y);
    }

    /// <summary>
    /// Moves the shelf along an edge to just past the pointer: to whichever
    /// side is the shorter move, or the other one if that doesn't fit.
    /// </summary>
    private static double Dodge(double start, double length, double pointer, double lower, double upper, double margin, double gap)
    {
        var before = pointer - gap - length;
        var after = pointer + gap;
        double[] sides = pointer >= start + length / 2 ? [before, after] : [after, before];
        foreach (var side in sides)
        {
            if (side >= lower + margin && side + length <= upper - margin)
            {
                return side;
            }
        }
        return start;
    }

    private static double Clamp(double value, double lower, double upper) => Math.Min(Math.Max(value, lower), upper);
}
