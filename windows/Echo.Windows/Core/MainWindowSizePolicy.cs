namespace Echo_Windows.Core;

public readonly record struct MainWindowPixelSize(int Width, int Height);

public static class MainWindowSizePolicy
{
    public const int MinimumWidthDip = 680;
    public const int MinimumHeightDip = 520;
    public const int IdealWidthDip = 820;
    public const int IdealHeightDip = 650;

    public static MainWindowPixelSize Minimum(int workWidth, int workHeight, double dpiScale) =>
        ClampToWorkArea(MinimumWidthDip, MinimumHeightDip, workWidth, workHeight, dpiScale);

    public static MainWindowPixelSize Initial(int workWidth, int workHeight, double dpiScale) =>
        ClampToWorkArea(IdealWidthDip, IdealHeightDip, workWidth, workHeight, dpiScale);

    private static MainWindowPixelSize ClampToWorkArea(int widthDip, int heightDip, int workWidth, int workHeight, double dpiScale)
    {
        if (workWidth <= 0 || workHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(workWidth), "The display work area must have positive dimensions.");

        dpiScale = double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1;
        return new MainWindowPixelSize(
            Math.Min((int)Math.Round(widthDip * dpiScale), workWidth),
            Math.Min((int)Math.Round(heightDip * dpiScale), workHeight));
    }
}
