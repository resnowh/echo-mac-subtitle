namespace Echo_Windows.Services;

/// <summary>Adjusts source frames per output frame so independent capture clocks cannot grow their buffers indefinitely.</summary>
public sealed class ClockDriftController
{
    public const double TargetBufferSeconds = 0.06;
    public const int MaximumFrameAdjustment = 2;
    private const double ProportionalGain = 0.05;
    private const double IntegralGain = 0.02;
    private const double MaximumIntegral = 0.0015;
    private double integral, fractionalFrames;
    public double LastRatio { get; private set; } = 1;

    public int InputFramesFor(int outputFrames, double bufferedSeconds)
    {
        if (outputFrames <= 0) throw new ArgumentOutOfRangeException(nameof(outputFrames));
        if (!double.IsFinite(bufferedSeconds) || bufferedSeconds < 0) bufferedSeconds = 0;
        double elapsed = outputFrames / 16000.0;
        double error = Math.Clamp(bufferedSeconds - TargetBufferSeconds, -2, 2);
        integral = Math.Clamp(integral + error * elapsed * IntegralGain, -MaximumIntegral, MaximumIntegral);
        LastRatio = Math.Clamp(1 + error * ProportionalGain + integral, 0.995, 1.005);
        double desiredFrames = outputFrames * LastRatio + fractionalFrames;
        int inputFrames = (int)Math.Round(desiredFrames, MidpointRounding.AwayFromZero);
        inputFrames = Math.Clamp(inputFrames, outputFrames - MaximumFrameAdjustment, outputFrames + MaximumFrameAdjustment);
        fractionalFrames = desiredFrames - inputFrames;
        return inputFrames;
    }
}
