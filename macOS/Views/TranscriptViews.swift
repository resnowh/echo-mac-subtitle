import SwiftUI

struct SubtitleCorrectionEditor: View {
    @ObservedObject var model: SpeechViewModel
    let entry: SubtitleEntry
    @Environment(\.dismiss) private var dismiss
    @State private var source: String
    @State private var translation: String
    @State private var baselineSource: String
    @State private var baselineTranslation: String
    @State private var term = ""
    @State private var termNotice = ""
    @State private var confirmDiscard = false

    init(model: SpeechViewModel, entry: SubtitleEntry) {
        self.model = model
        self.entry = entry
        _source = State(initialValue: entry.english)
        _translation = State(initialValue: entry.chinese)
        _baselineSource = State(initialValue: entry.english)
        _baselineTranslation = State(initialValue: entry.chinese)
    }

    private var current: SubtitleEntry? { model.entries.first { $0.id == entry.id } }
    private var isDirty: Bool { source != baselineSource || translation != baselineTranslation }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("纠正字幕").font(.headline)
                Spacer()
                Button("关闭") {
                    if isDirty { confirmDiscard = true } else { dismiss() }
                }
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    Text("录音会继续。仅保存改动的字段；未编辑的内容继续接收识别结果。")
                        .font(.caption).foregroundStyle(.secondary)
                    if let current, current.english != baselineSource || current.chinese != baselineTranslation {
                        Text("本条识别稿仍在更新；保存会覆盖你编辑过的字段。")
                            .font(.caption).foregroundStyle(.orange)
                        Button("载入最新识别稿") { reloadDraft() }.disabled(isDirty)
                    }
                    Text("原文").font(.subheadline.bold())
                    TextEditor(text: $source).frame(height: 95)
                        .overlay(RoundedRectangle(cornerRadius: 4).stroke(.secondary.opacity(0.3)))
                    Text("译文").font(.subheadline.bold())
                    TextEditor(text: $translation).frame(height: 95)
                        .overlay(RoundedRectangle(cornerRadius: 4).stroke(.secondary.opacity(0.3)))
                    HStack {
                        Button("AI 校对") { model.requestCorrection(entry.id) }
                            .disabled(isDirty || current == nil)
                        Button("重新翻译") { model.requestCorrection(entry.id, translate: true) }
                            .disabled(isDirty || current == nil || !model.recognitionConfig.translationEnabled)
                        Button("撤销上次纠正") {
                            model.undoCorrection(entry.id)
                            reloadDraft()
                        }
                        .disabled(isDirty || current?.correction?.history.isEmpty != false)
                    }
                    Text(isDirty ? "请先保存修改，再请求 AI 校对或重新翻译。" : "AI 只读取文字；点击请求将本句和相邻上下文发送给 DeepSeek，可能产生费用。")
                        .font(.caption).foregroundStyle(.secondary)
                    if let status = model.correctionStatuses[entry.id] {
                        Text(status).font(.caption).foregroundStyle(.secondary)
                    }
                    if model.archiveStatus.contains("失败") { Text(model.archiveStatus).foregroundStyle(.red) }
                    if model.fileStatus.contains("失败") { Text(model.fileStatus).foregroundStyle(.red) }
                    if let suggestion = model.correctionSuggestions[entry.id] {
                        VStack(alignment: .leading, spacing: 8) {
                            Text(suggestion.uncertain ? "待确认建议（AI 无法确认原音）" : "AI 校对建议").font(.subheadline.bold())
                            Text(suggestion.source)
                            Text(suggestion.translation)
                            Text(suggestion.reason).font(.caption).foregroundStyle(.secondary)
                            Button("采用建议到编辑框") {
                                source = suggestion.source
                                if model.recognitionConfig.translationEnabled { translation = suggestion.translation }
                            }
                            .disabled(isDirty)
                            Text("采用后仍需点击保存；请先核对专业词、数字和否定词。")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        .padding(10).background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
                    }
                    if let correction = current?.correction {
                        DisclosureGroup("识别稿与修改前版本") {
                            VStack(alignment: .leading, spacing: 8) {
                                Text("识别稿（未应用手动纠正）").font(.caption.bold())
                                Text(correction.rawSource)
                                Text(correction.rawTranslation)
                                ForEach(Array(correction.history.enumerated()), id: \.offset) { _, version in
                                    Divider()
                                    Text(version.date, style: .time).font(.caption)
                                    Text(version.source)
                                    Text(version.translation)
                                }
                            }.textSelection(.enabled)
                        }
                    }
                    HStack {
                        TextField("需要记住的专业词（可选）", text: $term)
                        Button("加入术语表") {
                            if model.addCorrectionTerm(term) {
                                term = ""
                                termNotice = "已加入；识别提示在下次建连生效"
                            } else { termNotice = "术语已存在，或已达到 100 个上限" }
                        }
                        .disabled(term.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || term.count > 80)
                    }
                    if !termNotice.isEmpty { Text(termNotice).font(.caption).foregroundStyle(.secondary) }
                    Text("已生成的总结不会自动重写；修正后的文字用于后续导出与总结。")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }
            HStack {
                Spacer()
                Button("保存纠正") {
                    model.saveCorrection(entry.id,
                        source: source != baselineSource ? source : nil,
                        translation: translation != baselineTranslation ? translation : nil)
                    reloadDraft()
                }
                .buttonStyle(.borderedProminent).tint(.mint)
                .disabled(!isDirty || current == nil || source.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(20).frame(width: 560, height: 600)
        .interactiveDismissDisabled(isDirty)
        .confirmationDialog("放弃尚未保存的修改？", isPresented: $confirmDiscard) {
            Button("放弃修改", role: .destructive) { dismiss() }
            Button("继续编辑", role: .cancel) { }
        }
    }

    private func reloadDraft() {
        guard let current else { return }
        source = current.english
        translation = current.chinese
        baselineSource = source
        baselineTranslation = translation
    }
}

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
        VStack(alignment: .leading, spacing: 7) {
            ForEach(parseBlocks()) { block in
                blockView(block)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private func blockView(_ block: SummaryBlock) -> some View {
        switch block.kind {
        case .title:
            Text(block.text)
                .font(.headline.weight(.semibold))
                .padding(.top, 2)
        case .heading:
            Text(block.text)
                .font(.subheadline.weight(.semibold))
                .padding(.top, 10)
        case .bullet:
            HStack(alignment: .top, spacing: 8) {
                Text("•")
                    .font(.body.weight(.semibold))
                Text(block.text)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        case .paragraph:
            Text(block.text)
                .font(.callout)
                .lineSpacing(3)
        }
    }

    private func parseBlocks() -> [SummaryBlock] {
        var blocks: [SummaryBlock] = []
        var paragraphLines: [String] = []

        func flushParagraph() {
            guard !paragraphLines.isEmpty else { return }
            blocks.append(SummaryBlock(id: blocks.count, kind: .paragraph, text: paragraphLines.joined(separator: "\n")))
            paragraphLines.removeAll(keepingCapacity: true)
        }

        for rawLine in markdown.components(separatedBy: .newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.isEmpty {
                flushParagraph()
            } else if line.hasPrefix("### ") {
                flushParagraph()
                blocks.append(SummaryBlock(id: blocks.count, kind: .heading, text: String(line.dropFirst(4))))
            } else if line.hasPrefix("## ") {
                flushParagraph()
                blocks.append(SummaryBlock(id: blocks.count, kind: .title, text: String(line.dropFirst(3))))
            } else if line.hasPrefix("- ") || line.hasPrefix("* ") {
                flushParagraph()
                blocks.append(SummaryBlock(id: blocks.count, kind: .bullet, text: String(line.dropFirst(2))))
            } else {
                paragraphLines.append(line)
            }
        }
        flushParagraph()
        return blocks
    }

    private struct SummaryBlock: Identifiable {
        enum Kind {
            case title
            case heading
            case bullet
            case paragraph
        }

        let id: Int
        let kind: Kind
        let text: String
    }
}

struct SynchronizedTranscriptView: View {
    let entries: [SubtitleEntry]
    let recognitionConfig: RecognitionConfig
    var suggestedIDs: Set<UUID> = []
    var onEdit: (SubtitleEntry) -> Void = { _ in }
    @State private var isAtBottom = true
    @State private var hasNewContent = false
    @State private var isUserScrolling = false

    var body: some View {
        GeometryReader { _ in
            ScrollViewReader { proxy in
                ZStack(alignment: .bottomTrailing) {
                    VStack(spacing: 0) {
                        HStack(alignment: .center, spacing: 14) {
                            Text(sourceHeader)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            if recognitionConfig.translationEnabled {
                                Text(targetHeader)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(.secondary)
                        .padding(.bottom, 8)

                        Divider().opacity(0.55)

                        ScrollView(.vertical) {
                            LazyVStack(spacing: 0) {
                                if entries.isEmpty {
                                    Text("开始录音后，每条识别结果会保留在这里")
                                        .foregroundStyle(.secondary)
                                        .frame(maxWidth: .infinity, alignment: .leading)
                                        .padding(.top, 14)
                                } else {
                                    ForEach(entries.indices, id: \.self) { index in
                                        let entry = entries[index]
                                        VStack(spacing: 0) {
                                            if isNewDay(at: index) {
                                                Text(dayLabel(for: entry))
                                                    .font(.subheadline.weight(.semibold))
                                                    .foregroundStyle(.secondary)
                                                    .frame(maxWidth: .infinity, alignment: .leading)
                                                    .padding(.top, 12)
                                                    .padding(.bottom, 4)
                                            }
                                            VStack(alignment: .leading, spacing: 5) {
                                                HStack {
                                                    Text(metadata(for: entry))
                                                    if entry.correction != nil { Text("已纠正") }
                                                    Spacer()
                                                    Button { onEdit(entry) } label: {
                                                        Label(suggestedIDs.contains(entry.id) ? "查看校对" : "纠正", systemImage: "pencil")
                                                    }
                                                    .buttonStyle(.borderless)
                                                    .help("编辑原文和译文，不中断录音")
                                                }
                                                .font(.caption.monospacedDigit())
                                                .foregroundStyle(.secondary)

                                                HStack(alignment: .top, spacing: 14) {
                                                    Text(entry.english.isEmpty ? "…" : entry.english)
                                                        .frame(maxWidth: .infinity, alignment: .leading)

                                                    if recognitionConfig.translationEnabled {
                                                        Text(entry.chinese.isEmpty ? " " : entry.chinese)
                                                            .frame(maxWidth: .infinity, alignment: .leading)
                                                    }
                                                }
                                                .font(.body)
                                                .lineSpacing(3)
                                            }
                                            .frame(maxWidth: .infinity, alignment: .leading)
                                            .padding(.vertical, 7)
                                            Divider()
                                        }
                                    }
                                }
                                Color.clear.frame(height: 1).id("subtitle-bottom")
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .id("subtitle-content")
                        }
                        .coordinateSpace(name: "transcriptScroll")
                        .onScrollPhaseChange { _, phase in
                            isUserScrolling = phase == .tracking || phase == .interacting || phase == .decelerating
                        }
                        .onScrollGeometryChange(for: Bool.self) { geometry in
                            geometry.contentSize.height - geometry.contentOffset.y - geometry.containerSize.height <= 32
                        } action: { _, nowAtBottom in
                            // Content growth must not be mistaken for the user
                            // scrolling away from the live end.
                            if isUserScrolling || nowAtBottom { isAtBottom = nowAtBottom }
                            if nowAtBottom { hasNewContent = false }
                        }
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
                .onChange(of: entries.last?.english) { _, _ in contentDidChange(proxy) }
                .onChange(of: entries.last?.chinese) { _, _ in contentDidChange(proxy) }
            }
        }
        .frame(minHeight: 220, maxHeight: .infinity)
    }

    private var sourceHeader: String {
        guard recognitionConfig.sourceLanguageMode == .specified else { return "原文" }
        return languageTitle(for: recognitionConfig.specifiedSourceLanguage)
    }

    private var targetHeader: String {
        languageTitle(for: recognitionConfig.targetTranslationLanguage)
    }

    private func languageTitle(for code: String) -> String {
        LanguageOption.supported.first(where: { $0.code == code })?.title ?? code
    }

    private func metadata(for entry: SubtitleEntry) -> String {
        var values: [String] = []
        if recognitionConfig.speakerDiarizationEnabled,
           let speaker = entry.speaker,
           !speaker.isEmpty {
            values.append(speaker)
        }
        values.append(timestamp(for: entry))
        return values.joined(separator: " · ")
    }

    private func timestamp(for entry: SubtitleEntry) -> String {
        (entry.recordedAt ?? Date(timeIntervalSince1970: entry.start))
            .formatted(.dateTime.hour(.twoDigits(amPM: .omitted)).minute(.twoDigits).second(.twoDigits)
                .locale(Locale(identifier: "en_GB")))
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
        proxy.scrollTo("subtitle-bottom", anchor: .bottom)
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
