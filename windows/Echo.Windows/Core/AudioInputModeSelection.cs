namespace Echo_Windows.Core;

public readonly record struct AudioInputModeSelection(int Mode, string? OutputId, string? InputId)
{
    public static AudioInputModeSelection ForSwitch(int mode, string? activeOutputId, string? activeInputId)
    {
        if (mode is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(mode));
        return new(mode,
            mode is 0 or 2 ? activeOutputId : null,
            mode is 1 or 2 ? activeInputId : null);
    }
}
