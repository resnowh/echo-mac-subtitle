namespace Echo_Windows.Core;

public static class SummaryPanelPresentation
{
    public static bool ShouldShow(bool autoSummaryEnabled, bool hasSummaryText, bool hasStatus)
        => autoSummaryEnabled || hasSummaryText || hasStatus;
}
