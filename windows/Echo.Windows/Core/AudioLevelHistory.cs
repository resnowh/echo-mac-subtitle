namespace Echo_Windows.Core;

/// <summary>Produces the smoothed RMS samples used by the Mac audio meter.</summary>
public sealed class AudioLevelHistory
{
    public const int Capacity = 48;
    private readonly List<double> samples = Enumerable.Repeat(0d, Capacity).ToList();
    private long lastSampleTimestamp;

    public double Current { get; private set; }
    public IReadOnlyList<double> Samples => samples;

    public bool TryRecord(double level, long timestamp, long timestampFrequency)
    {
        if (timestampFrequency <= 0) throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        if (lastSampleTimestamp != 0 && timestamp - lastSampleTimestamp < Math.Max(1, timestampFrequency / 20)) return false;
        lastSampleTimestamp = timestamp;
        level = double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;
        Current = level >= Current ? Current * .25 + level * .75 : Current * .82 + level * .18;
        samples.Add(Current);
        if (samples.Count > Capacity) samples.RemoveAt(0);
        return true;
    }

    public void Reset()
    {
        Current = 0;
        lastSampleTimestamp = 0;
        samples.Clear();
        samples.AddRange(Enumerable.Repeat(0d, Capacity));
    }

    public static double MeasureRms(ReadOnlySpan<float> samples, int validCount)
    {
        int count = Math.Clamp(validCount, 0, samples.Length);
        if (count == 0) return 0;
        double sum = 0;
        for (int i = 0; i < count; i++) sum += (double)samples[i] * samples[i];
        return Math.Clamp(Math.Sqrt(sum / count) * 7.5, 0, 1);
    }
}
