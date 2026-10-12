namespace Echo_Windows.Core;

public readonly record struct OverlayPlacement(int X, int Y, int Width, int Height);

public static class DesktopSubtitleOverlayPlacement
{
    public static OverlayPlacement Calculate(
        int workX,
        int workY,
        int workWidth,
        int workHeight,
        double dpiScale,
        double widthFraction,
        double normalizedX,
        double normalizedBottom)
    {
        if (workWidth <= 0 || workHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(workWidth), "The display work area must have positive dimensions.");

        dpiScale = double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1;
        widthFraction = double.IsFinite(widthFraction) ? Math.Clamp(widthFraction, .35, .95) : .75;
        normalizedX = double.IsFinite(normalizedX) ? Math.Clamp(normalizedX, 0, 1) : .5;
        normalizedBottom = double.IsFinite(normalizedBottom) ? Math.Clamp(normalizedBottom, 0, 1) : .09;

        int minWidth = Math.Min(workWidth, (int)Math.Ceiling(280 * dpiScale));
        int width = Math.Clamp((int)(workWidth * widthFraction), minWidth, workWidth);
        int height = Math.Min(workHeight, Math.Max(1, (int)Math.Ceiling(150 * dpiScale)));
        int x = workX + (int)(workWidth * normalizedX) - width / 2;
        int y = workY + workHeight - height - (int)(workHeight * normalizedBottom);

        return new OverlayPlacement(
            Math.Clamp(x, workX, workX + workWidth - width),
            Math.Clamp(y, workY, workY + workHeight - height),
            width,
            height);
    }
}
