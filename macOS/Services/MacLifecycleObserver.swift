import AVFoundation
import AppKit

/// Centralized macOS lifecycle notifications. It is deliberately independent
/// of SwiftUI and only reports events to its owner.
final class MacLifecycleObserver {
    var onWillSleep: (() -> Void)?
    var onDidWake: (() -> Void)?
    var onAudioConfigurationChange: (() -> Void)?

    private var observers: [NSObjectProtocol] = []

    func start() {
        guard observers.isEmpty else { return }

        let workspaceCenter = NSWorkspace.shared.notificationCenter
        observers.append(workspaceCenter.addObserver(
            forName: NSWorkspace.willSleepNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.onWillSleep?()
        })
        observers.append(workspaceCenter.addObserver(
            forName: NSWorkspace.didWakeNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.onDidWake?()
        })
        observers.append(NotificationCenter.default.addObserver(
            // AVAudioEngineConfigurationChangeNotification is imported as a
            // C global on some SDK versions and is not exposed as a Swift
            // type member consistently. Keep the canonical notification name
            // here so the app builds across the supported macOS SDKs.
            forName: Notification.Name("AVAudioEngineConfigurationChangeNotification"),
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.onAudioConfigurationChange?()
        })
    }

    func stop() {
        let workspaceCenter = NSWorkspace.shared.notificationCenter
        observers.forEach { observer in
            workspaceCenter.removeObserver(observer)
            NotificationCenter.default.removeObserver(observer)
        }
        observers.removeAll()
    }

    deinit { stop() }
}
