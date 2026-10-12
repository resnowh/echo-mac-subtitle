namespace Echo_Windows.Core;

public static class ThemePreference
{
    // Keep the segmented control and cycle button in the same order as Mac AppThemeMode.allCases.
    public static int IndexFor(string? theme) => theme switch
    {
        "Light" => 0,
        "Dark" => 1,
        _ => 2
    };

    public static string FromIndex(int index) => index switch
    {
        0 => "Light",
        1 => "Dark",
        _ => "Default"
    };

    public static string Next(string? theme) => FromIndex((IndexFor(theme) + 1) % 3);
}
