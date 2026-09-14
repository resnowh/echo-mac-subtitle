import AppKit

/// Centralized macOS lifecycle notifications. It is deliberately independent
/// of SwiftUI and only reports events to its owner.
final class MacLifecycleObserver {
    var onWillSleep: (() -> Void)?
    var onDidWake: (() -> Void)?

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
    }

    func stop() {
        let workspaceCenter = NSWorkspace.shared.notificationCenter
        observers.forEach { observer in
            workspaceCenter.removeObserver(observer)
        }
        observers.removeAll()
    }

    deinit { stop() }
}
