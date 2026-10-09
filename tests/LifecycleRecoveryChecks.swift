import Foundation

enum LifecycleRecoveryChecks {
    static func run() {
        inactiveWakeIsIgnored()
        recordingResumesAcrossRepeatedSleepCycles()
        userStopCancelsPendingAndActiveRecovery()
        duplicateAndStaleEventsAreIgnored()
        print("PASS: Mac sleep/wake recovery state, repeated cycles, user cancellation, and stale events")
    }

    private static func inactiveWakeIsIgnored() {
        var state = LifecycleRecoveryState()
        precondition(state.handle(.didWake) == .none)
        precondition(state.handle(.willSleep) == .none)
        precondition(state.isSystemSleeping && !state.wasRecordingBeforeSleep)
        precondition(state.handle(.didWake) == .none)
        precondition(!state.isSystemSleeping && !state.pendingWakeRecovery)
    }

    private static func recordingResumesAcrossRepeatedSleepCycles() {
        var state = LifecycleRecoveryState()
        for cycle in 0..<10 {
            precondition(state.handle(.willSleep, recordingIntended: true) == .endRecordingForSleep)
            precondition(state.isSystemSleeping && state.wasRecordingBeforeSleep)
            precondition(state.handle(.didWake) == .scheduleWakeRecovery)
            precondition(state.pendingWakeRecovery && !state.isSystemSleeping)
            precondition(state.handle(.didWake) == .none, "Duplicate wake scheduled twice in cycle \(cycle)")
            precondition(state.handle(.recoveryStarted) == .beginWakeRecovery)
            precondition(state.isRecovering && !state.pendingWakeRecovery)
            precondition(state.handle(.recoverySucceeded) == .none)
            precondition(!state.wasRecordingBeforeSleep && !state.isRecovering)
        }
    }

    private static func userStopCancelsPendingAndActiveRecovery() {
        var pending = LifecycleRecoveryState()
        _ = pending.handle(.willSleep, recordingIntended: true)
        _ = pending.handle(.didWake)
        precondition(pending.pendingWakeRecovery)
        _ = pending.handle(.userStop)
        precondition(!pending.pendingWakeRecovery && !pending.wasRecordingBeforeSleep)
        precondition(pending.handle(.recoveryStarted) == .none)

        var recovering = LifecycleRecoveryState()
        _ = recovering.handle(.willSleep, recordingIntended: true)
        _ = recovering.handle(.didWake)
        _ = recovering.handle(.recoveryStarted)
        precondition(recovering.isRecovering)
        _ = recovering.handle(.userStop)
        precondition(!recovering.isRecovering && !recovering.wasRecordingBeforeSleep)
        _ = recovering.handle(.recoverySucceeded)
        precondition(!recovering.pendingWakeRecovery && !recovering.isRecovering)
    }

    private static func duplicateAndStaleEventsAreIgnored() {
        var state = LifecycleRecoveryState()
        precondition(state.handle(.recoveryStarted) == .none)
        _ = state.handle(.willSleep, recordingIntended: true)
        _ = state.handle(.didWake)
        _ = state.handle(.recoveryStarted)
        precondition(state.handle(.recoveryStarted) == .none)
        _ = state.handle(.recoveryFailed)
        precondition(!state.wasRecordingBeforeSleep && !state.pendingWakeRecovery && !state.isRecovering)
        precondition(state.handle(.recoverySucceeded) == .none)
    }
}
