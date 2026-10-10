namespace Echo_Windows.Core;

public readonly record struct SummaryPanelLayout(double PanelMaxHeight, double ContentMaxHeight);

public static class SummaryPanelLayoutPolicy
{
    public static SummaryPanelLayout Calculate(
        double flexibleHeight,
        double fixedPanelHeight,
        double minimumTranscriptHeight,
        double preferredContentMaxHeight)
    {
        if (!double.IsFinite(flexibleHeight) || flexibleHeight < 0) flexibleHeight = 0;
        if (!double.IsFinite(fixedPanelHeight) || fixedPanelHeight < 0) fixedPanelHeight = 0;
        if (!double.IsFinite(minimumTranscriptHeight) || minimumTranscriptHeight < 0) minimumTranscriptHeight = 0;
        if (!double.IsFinite(preferredContentMaxHeight) || preferredContentMaxHeight < 0) preferredContentMaxHeight = 0;

        double availablePanelHeight = Math.Max(0, flexibleHeight - minimumTranscriptHeight);
        double panelMaxHeight = Math.Min(availablePanelHeight, fixedPanelHeight + preferredContentMaxHeight);
        double contentMaxHeight = Math.Max(0, panelMaxHeight - fixedPanelHeight);
        return new(panelMaxHeight, contentMaxHeight);
    }
}
