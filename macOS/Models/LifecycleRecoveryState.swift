enum LifecycleEvent {
    case willSleep
    case didWake
    case userStop
    case recoveryStarted
    case recoverySucceeded
    case recoveryFailed
}

enum LifecycleAction: Equatable {
    case none
    case endRecordingForSleep
    case scheduleWakeRecovery
    case beginWakeRecovery
}

/// Small, platform-independent state machine for sleep/wake recovery. The
/// macOS notification adapter only emits events; it does not own recording.
struct LifecycleRecoveryState: Equatable {
    private(set) var isSystemSleeping = false
    private(set) var wasRecordingBeforeSleep = false
    private(set) var pendingWakeRecovery = false
    private(set) var isRecovering = false

    mutating func handle(_ event: LifecycleEvent, recordingIntended: Bool = false) -> LifecycleAction {
        switch event {
        case .willSleep:
            let shouldResume = recordingIntended || wasRecordingBeforeSleep || pendingWakeRecovery || isRecovering
            isSystemSleeping = true
            pendingWakeRecovery = false
            isRecovering = false
            wasRecordingBeforeSleep = shouldResume
            return shouldResume ? .endRecordingForSleep : .none

        case .didWake:
            guard isSystemSleeping else { return .none }
            isSystemSleeping = false
            guard wasRecordingBeforeSleep, !pendingWakeRecovery, !isRecovering else { return .none }
            pendingWakeRecovery = true
            return .scheduleWakeRecovery

        case .userStop:
            pendingWakeRecovery = false
            isRecovering = false
            wasRecordingBeforeSleep = false
            return .none

        case .recoveryStarted:
            guard pendingWakeRecovery, !isSystemSleeping, !isRecovering else { return .none }
            pendingWakeRecovery = false
            isRecovering = true
            return .beginWakeRecovery

        case .recoverySucceeded, .recoveryFailed:
            isRecovering = false
            pendingWakeRecovery = false
            wasRecordingBeforeSleep = false
            return .none
        }
    }
}
