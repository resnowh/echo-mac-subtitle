import Combine
import Foundation

/// A low-frequency, read-only projection of the live subtitle entry.
/// Audio meters and other high-frequency SpeechViewModel state are not observed.
final class DesktopSubtitleOverlayFeed: ObservableObject {
    @Published private(set) var current: DesktopSubtitleOverlayState?
    private var reducer = DesktopSubtitleOverlayReducer()
    private let minimumPublishInterval: TimeInterval = 0.05
    private var lastPublishUptime: TimeInterval = 0
    private var pendingEntry: SubtitleEntry?
    private var pendingTranslationEnabled = false
    private var pendingWorkItem: DispatchWorkItem?
    private var generation = UUID()
    private let generationLock = NSLock()

    func update(_ entry: SubtitleEntry, translationEnabled: Bool) {
        guard Thread.isMainThread else {
            generationLock.lock()
            let expectedGeneration = generation
            generationLock.unlock()
            DispatchQueue.main.async { [weak self] in
                guard let self, self.isCurrentGeneration(expectedGeneration) else { return }
                self.update(entry, translationEnabled: translationEnabled)
            }
            return
        }
        let newEntry = (pendingEntry?.id ?? current?.entryID) != entry.id
        let elapsed = ProcessInfo.processInfo.systemUptime - lastPublishUptime
        guard !newEntry, elapsed < minimumPublishInterval else {
            pendingWorkItem?.cancel()
            pendingWorkItem = nil
            pendingEntry = nil
            publish(entry, translationEnabled: translationEnabled)
            return
        }
        pendingEntry = entry
        pendingTranslationEnabled = translationEnabled
        pendingWorkItem?.cancel()
        let expectedGeneration = generation
        let delay = minimumPublishInterval - elapsed
        let work = DispatchWorkItem { [weak self] in
            guard let self, self.generation == expectedGeneration,
                  let latest = self.pendingEntry else { return }
            let translationEnabled = self.pendingTranslationEnabled
            self.pendingEntry = nil
            self.pendingWorkItem = nil
            self.publish(latest, translationEnabled: translationEnabled)
        }
        pendingWorkItem = work
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: work)
    }

    private func publish(_ entry: SubtitleEntry, translationEnabled: Bool) {
        reducer.update(entry, translationEnabled: translationEnabled)
        publishReducerState()
        lastPublishUptime = ProcessInfo.processInfo.systemUptime
    }

    func finalize(_ entry: SubtitleEntry, translationEnabled: Bool) {
        pendingWorkItem?.cancel()
        pendingWorkItem = nil
        pendingEntry = nil
        reducer.finalize(entry, translationEnabled: translationEnabled)
        publishReducerState()
        lastPublishUptime = ProcessInfo.processInfo.systemUptime
    }

    func clear() {
        generationLock.lock()
        generation = UUID()
        generationLock.unlock()
        pendingWorkItem?.cancel()
        pendingWorkItem = nil
        pendingEntry = nil
        reducer.clear()
        publishReducerState()
    }

    private func publishReducerState() {
        guard current != reducer.current else { return }
        current = reducer.current
    }

    private func isCurrentGeneration(_ value: UUID) -> Bool {
        generationLock.lock()
        defer { generationLock.unlock() }
        return generation == value
    }
}
