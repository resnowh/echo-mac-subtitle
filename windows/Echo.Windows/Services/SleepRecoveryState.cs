namespace Echo_Windows.Services;

public sealed class SleepRecoveryState
{
    public bool IsSleeping { get; private set; }
    public bool WasRecordingBeforeSleep { get; private set; }
    public bool HasPendingWakeRecovery { get; private set; }
    public bool IsRecovering { get; private set; }

    public bool BeginSleep(bool recordingIntended)
    {
        if (IsSleeping) return false;
        bool shouldResume = recordingIntended || WasRecordingBeforeSleep || HasPendingWakeRecovery || IsRecovering;
        IsSleeping = true;
        HasPendingWakeRecovery = false;
        IsRecovering = false;
        WasRecordingBeforeSleep = shouldResume;
        return recordingIntended;
    }

    public bool BeginWake()
    {
        if (!IsSleeping) return false;
        IsSleeping = false;
        if (!WasRecordingBeforeSleep || HasPendingWakeRecovery || IsRecovering) return false;
        HasPendingWakeRecovery = true;
        return true;
    }

    public bool BeginRecovery()
    {
        if (!HasPendingWakeRecovery || IsSleeping || IsRecovering) return false;
        HasPendingWakeRecovery = false;
        IsRecovering = true;
        return true;
    }

    public void CancelByUser()
    {
        HasPendingWakeRecovery = false;
        IsRecovering = false;
        WasRecordingBeforeSleep = false;
    }

    public void FinishRecovery()
    {
        IsRecovering = false;
        HasPendingWakeRecovery = false;
        WasRecordingBeforeSleep = false;
    }
}
