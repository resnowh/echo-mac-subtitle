using CommunityToolkit.Mvvm.ComponentModel;

namespace Echo_Windows.Core;

public sealed record DesktopSubtitleOverlayState(
    Guid EntryId,
    string Original,
    string Translation,
    bool TranslationEnabled,
    bool IsFinal,
    Guid Revision,
    DateTimeOffset? FinalizedAt)
{
    public bool IsVisible => Original.Length > 0 || (TranslationEnabled && Translation.Length > 0);

    public bool RemainsVisible(DateTimeOffset now, double retentionSeconds) =>
        !IsFinal || FinalizedAt is null || (now - FinalizedAt.Value).TotalSeconds < retentionSeconds;
}

public static class DesktopSubtitleOverlayPresentation
{
    public static bool ShouldShowOriginal(DesktopSubtitleOverlayState state, DesktopSubtitleOverlaySettings settings) =>
        !string.IsNullOrWhiteSpace(state.Original)
        && (settings.ShowOriginal || !state.TranslationEnabled || !settings.ShowTranslation);

    public static bool ShouldShowTranslation(DesktopSubtitleOverlayState state, DesktopSubtitleOverlaySettings settings) =>
        state.TranslationEnabled && settings.ShowTranslation && !string.IsNullOrWhiteSpace(state.Translation);
}

/// <summary>A read-only projection of the latest live subtitle; it owns no audio or network resources.</summary>
public partial class DesktopSubtitleOverlayFeed : ObservableObject
{
    [ObservableProperty] public partial DesktopSubtitleOverlayState? Current { get; private set; }

    public void Update(Subtitle entry, bool translationEnabled, DateTimeOffset? now = null)
    {
        string original = entry.English.Trim();
        string translation = entry.Chinese.Trim();
        if (original.Length == 0 && (!translationEnabled || translation.Length == 0)) return;

        DateTimeOffset at = now ?? DateTimeOffset.UtcNow;
        bool sameEntry = Current?.EntryId == entry.Id;
        bool textChanged = sameEntry && (Current!.Original != original || Current.Translation != translation || Current.TranslationEnabled != translationEnabled);
        bool wasFinal = sameEntry && Current!.IsFinal;
        Current = new DesktopSubtitleOverlayState(entry.Id, original, translation, translationEnabled,
            wasFinal, Guid.NewGuid(), wasFinal && textChanged ? at : sameEntry ? Current!.FinalizedAt : null);
    }

    public void Finalize(Subtitle entry, bool translationEnabled, DateTimeOffset? now = null)
    {
        Update(entry, translationEnabled, now);
        if (Current?.EntryId != entry.Id) return;
        Current = Current with { IsFinal = true, FinalizedAt = now ?? DateTimeOffset.UtcNow, Revision = Guid.NewGuid() };
    }

    public void Clear() => Current = null;
}
