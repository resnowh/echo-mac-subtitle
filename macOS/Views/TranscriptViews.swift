import SwiftUI

struct AudioInputModeLabel: View {
    let mode: AudioInputMode

    var body: some View {
        HStack(spacing: 6) {
            if mode == .computerAndMicrophone {
                Image(systemName: "speaker.wave.2.fill")
                Image(systemName: "mic.fill")
            } else {
                Image(systemName: mode.icon)
            }
            Text(mode.title)
        }
    }
}

struct MarkdownSummaryView: View {
    let markdown: String

    var body: some View {
        if let rendered = try? AttributedString(markdown: markdown) {
            Text(rendered)
                .frame(maxWidth: .infinity, alignment: .leading)
        } else {
            Text(markdown)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct TranscriptBottomPreferenceKey: PreferenceKey {
    static let defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = nextValue()
    }
}

struct SynchronizedTranscriptView: View {
    let entries: [SubtitleEntry]
    @State private var isAtBottom = true
    @State private var hasNewContent = false

    var body: some View {
        GeometryReader { container in
            ScrollViewReader { proxy in
                ZStack(alignment: .bottomTrailing) {
                    ScrollView(.vertical) {
                        VStack(spacing: 0) {
                            HStack(alignment: .top, spacing: 14) {
                                Text("英文原文").font(.headline).frame(maxWidth: .infinity, alignment: .leading)
                                Text("中文翻译").font(.headline).frame(maxWidth: .infinity, alignment: .leading)
                            }
                            .padding(.bottom, 10)
                            if entries.isEmpty {
                                HStack(alignment: .top, spacing: 14) {
                                    Text("开始说英文后，每条识别结果会保留在这里").foregroundStyle(.secondary)
                                    Text("Soniox 翻译会逐条追加到这里").foregroundStyle(.secondary)
                                }
                                .frame(maxWidth: .infinity, alignment: .leading)
                            } else {
                                ForEach(Array(entries.enumerated()), id: \.element.id) { index, entry in
                                    if isNewDay(at: index) {
                                        Text(dayLabel(for: entry))
                                            .font(.subheadline.weight(.semibold))
                                            .foregroundStyle(.secondary)
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                            .padding(.top, 12)
                                            .padding(.bottom, 4)
                                    }
                                    HStack(alignment: .top, spacing: 14) {
                                        VStack(alignment: .leading, spacing: 4) {
                                            if let speaker = entry.speaker, !speaker.isEmpty {
                                                Text(speaker)
                                                    .font(.caption.weight(.semibold))
                                                    .foregroundStyle(.mint)
                                            }
                                            HStack(alignment: .top, spacing: 8) {
                                                Text(timestamp(for: entry, at: index))
                                                    .font(.caption.monospacedDigit())
                                                    .foregroundStyle(.secondary)
                                                    .frame(width: 78, alignment: .leading)
                                                Text(entry.english.isEmpty ? "…" : entry.english)
                                                    .frame(maxWidth: .infinity, alignment: .leading)
                                            }
                                        }
                                        .frame(maxWidth: .infinity, alignment: .leading)
                                        Text(entry.chinese.isEmpty ? "翻译中…" : entry.chinese)
                                            .foregroundStyle(entry.chinese.isEmpty ? .secondary : .primary)
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                    }
                                    .padding(.vertical, 8)
                                    Divider()
                                }
                            }
                            GeometryReader { bottom in
                                Color.clear
                                    .preference(key: TranscriptBottomPreferenceKey.self,
                                                value: bottom.frame(in: .named("transcriptScroll")).maxY)
                            }
                            .frame(height: 1)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .id("subtitle-content")
                    }
                    .coordinateSpace(name: "transcriptScroll")
                    .onPreferenceChange(TranscriptBottomPreferenceKey.self) { bottomMaxY in
                        let nowAtBottom = bottomMaxY <= container.size.height + 24
                        isAtBottom = nowAtBottom
                        if nowAtBottom { hasNewContent = false }
                    }

                    if hasNewContent {
                        Button {
                            scrollToBottom(proxy)
                            isAtBottom = true
                            hasNewContent = false
                        } label: {
                            Label("有新内容", systemImage: "arrow.down")
                                .font(.caption.weight(.semibold))
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(.mint)
                        .padding(10)
                    }
                }
                .onChange(of: entries.count) { _, _ in contentDidChange(proxy) }
                .onChange(of: entries.map(\.english).joined()) { _, _ in contentDidChange(proxy) }
                .onChange(of: entries.map(\.chinese).joined()) { _, _ in contentDidChange(proxy) }
            }
        }
        .frame(minHeight: 220, maxHeight: .infinity)
    }

    private func timestamp(for entry: SubtitleEntry, at index: Int) -> String {
        let date = entry.recordedAt
        let timeFormatter = DateFormatter()
        timeFormatter.locale = Locale(identifier: "en_US_POSIX")
        timeFormatter.timeZone = .current
        timeFormatter.dateFormat = "HH:mm:ss"
        let dateTimeFormatter = DateFormatter()
        dateTimeFormatter.locale = Locale(identifier: "en_US_POSIX")
        dateTimeFormatter.timeZone = .current
        dateTimeFormatter.dateFormat = "MM-dd HH:mm:ss"

        guard let date else {
            return timeFormatter.string(from: Date(timeIntervalSince1970: entry.start))
        }
        guard index > 0, entries[index - 1].recordedAt != nil else {
            return dateTimeFormatter.string(from: date)
        }
        return timeFormatter.string(from: date)
    }

    private func isNewDay(at index: Int) -> Bool {
        guard index > 0,
              let previousDate = entries[index - 1].recordedAt,
              let date = entries[index].recordedAt else { return false }
        return !Calendar.current.isDate(previousDate, inSameDayAs: date)
    }

    private func dayLabel(for entry: SubtitleEntry) -> String {
        guard let date = entry.recordedAt else { return "日期" }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "zh_CN")
        formatter.timeZone = .current
        formatter.dateFormat = "yyyy年M月d日"
        return formatter.string(from: date)
    }

    private func contentDidChange(_ proxy: ScrollViewProxy) {
        guard isAtBottom else {
            hasNewContent = true
            return
        }
        hasNewContent = false
        DispatchQueue.main.async {
            scrollToBottom(proxy)
        }
    }

    private func scrollToBottom(_ proxy: ScrollViewProxy) {
        withAnimation(.easeOut(duration: 0.18)) {
            proxy.scrollTo("subtitle-content", anchor: .bottom)
        }
    }
}

struct WaveformView: View {
    let samples: [Double]
    let active: Bool

    var body: some View {
        HStack(alignment: .center, spacing: 3) {
            ForEach(Array(samples.enumerated()), id: \.offset) { _, sample in
                RoundedRectangle(cornerRadius: 2)
                    .fill(active ? Color.mint.opacity(0.85) : Color.secondary.opacity(0.35))
                    .frame(width: 3, height: active ? max(2, min(46, 3 + sample * 44)) : 2)
            }
        }
        .frame(maxWidth: .infinity)
        .frame(height: 52, alignment: .center)
        .padding(.horizontal, 12)
        .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 10))
        .animation(.easeOut(duration: 0.06), value: samples)
    }
}
