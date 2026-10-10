namespace Echo_Windows.Core;

/// <summary>Pure pixel-space placement for a small overlay popover.</summary>
public readonly record struct OverlayPixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class OverlayPopoverLayout
{
    public static OverlayPixelRect Place(
        OverlayPixelRect anchor,
        OverlayPixelRect workArea,
        int desiredWidth,
        int desiredHeight,
        int gap)
    {
        int workWidth = Math.Max(1, workArea.Width);
        int workHeight = Math.Max(1, workArea.Height);
        int width = Math.Clamp(desiredWidth, 1, workWidth);
        int height = Math.Clamp(desiredHeight, 1, workHeight);
        int above = Math.Max(0, anchor.Y - gap - workArea.Y);
        int below = Math.Max(0, workArea.Bottom - (anchor.Bottom + gap));
        int left = Math.Max(0, anchor.X - gap - workArea.X);
        int right = Math.Max(0, workArea.Right - (anchor.Right + gap));

        if (Math.Max(above, below) >= height)
        {
            bool placeAbove = above >= below;
            int x = Math.Clamp(anchor.Right - width, workArea.X, Math.Max(workArea.X, workArea.Right - width));
            int y = placeAbove ? anchor.Y - gap - height : anchor.Bottom + gap;
            return new OverlayPixelRect(x, y, width, height);
        }

        if (workHeight >= height && Math.Max(left, right) >= width)
        {
            bool placeRight = right >= left;
            int sideX = placeRight ? anchor.Right + gap : anchor.X - gap - width;
            int sideY = Math.Clamp(anchor.Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height));
            return new OverlayPixelRect(sideX, sideY, width, height);
        }

        int aboveHeight = Math.Min(height, Math.Max(1, above));
        int belowHeight = Math.Min(height, Math.Max(1, below));
        int leftWidth = Math.Min(width, Math.Max(1, left));
        int rightWidth = Math.Min(width, Math.Max(1, right));
        var candidates = new[]
        {
            new OverlayPixelRect(Math.Clamp(anchor.Right - width, workArea.X, Math.Max(workArea.X, workArea.Right - width)),
                Math.Clamp(anchor.Y - gap - aboveHeight, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - aboveHeight)), width, aboveHeight),
            new OverlayPixelRect(Math.Clamp(anchor.Right - width, workArea.X, Math.Max(workArea.X, workArea.Right - width)),
                Math.Clamp(anchor.Bottom + gap, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - belowHeight)), width, belowHeight),
            new OverlayPixelRect(Math.Clamp(anchor.X - gap - leftWidth, workArea.X, Math.Max(workArea.X, workArea.Right - leftWidth)),
                Math.Clamp(anchor.Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height)), leftWidth, height),
            new OverlayPixelRect(Math.Clamp(anchor.Right + gap, workArea.X, Math.Max(workArea.X, workArea.Right - rightWidth)),
                Math.Clamp(anchor.Y, workArea.Y, Math.Max(workArea.Y, workArea.Bottom - height)), rightWidth, height)
        };
        return candidates.OrderByDescending(candidate => candidate.Width * (long)candidate.Height).First();
    }
}
