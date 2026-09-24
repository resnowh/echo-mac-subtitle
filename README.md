# Echo：macOS 实时语音转写与翻译

Echo 是一个原生 macOS 实时字幕应用。它从话筒或 Mac 正在播放的声音采集音频，通过 Soniox 实时识别，并在界面中保留英文原文、翻译和时间戳。默认行为是英语识别、简体中文翻译；音频默认不保存。

## 当前功能

- **录音中纠正文字**：每行可编辑原文/译文、查看修改前版本并撤销。保存后的改动字段不会被后续识别覆盖，存档及后续导出/总结使用修正版。
- **可选 AI 校对**：默认关闭，在设置开启或逐句请求。使用 DeepSeek Key，将文字和上下文发送到云端，可能计费，不上传音频。只生成需确认的建议，不自动改稿。原文修改保存后可请求“重新翻译”，已有总结和导出文件不自动重写。
- **课程术语表**：用于 AI 校对和下次 Soniox 建连的识别提示，不作全局替换，也不保证每次识别正确。

- 输入源可在录音中热切换：
  - **电脑音频**：识别 Mac 正在播放的声音。
  - **话筒**：识别麦克风输入。
  - **电脑音频和话筒**：两路先在本地按时间轴混音，再发送给 Soniox。
- 识别到的字幕逐条保留，支持现实本地时间、跨天日期行和滚动时的新内容提醒。
- Soniox speaker diarization 可显示并保存匿名编号，如 `Speaker 1`、`Speaker 2`。
- 设置中可选择：
  - 自动识别源语言，或指定语言（指定语言会作为 Soniox 语言提示）；
  - 指定语言的严格限制；
  - 是否翻译，以及翻译目标语言；
  - 是否启用 Speaker 编号。
- 可选择新建存档或接续已有存档；一次课程可以由多个录音段组成，也可以把刚完成的录音段拆出。
- 每段录音自动保存带时间戳的 `.srt` 文字稿；可从主界面导出当前存档的全部字幕。
- 可选 DeepSeek AI 总结：
  - 总结新增内容；
  - 重新总结当前录音段；
  - 总结整个存档。
  AI 总结默认关闭，录音不会因为手动生成总结而停止。
- 支持浅色、深色和跟随系统主题，以及窗口置顶开关。

## 系统要求

- macOS 15.0 或更高版本。
- Apple Silicon 或 Intel Mac。
- Soniox API Key 和网络连接。
- 使用话筒需要允许 Echo 访问麦克风。
- 使用电脑音频需要允许 Echo 访问“屏幕与系统音频录制”。
- AI 总结还需要单独填写 DeepSeek API Key。

Echo 当前只支持 macOS。项目没有 Windows 版本，也没有现成 GitHub Release 安装包或 Apple notarization。

## 使用方法

1. 用 Xcode 打开 `macOS/EchoMac.xcodeproj`，运行 `Echo` Scheme。
2. 打开设置，在 `Soniox API Key` 中填写 Key。Key 只保存在本机 UserDefaults，不写入源码。
3. 按需在“识别与翻译”中选择源语言和翻译目标；默认是 English → 简体中文。
4. 在主界面选择输入源，点击“开始录音”。
5. 第一次使用话筒或电脑音频时，根据 macOS 提示授予权限。

Soniox Key 可从 [Soniox Console](https://console.soniox.com/) 创建。Echo 使用 Soniox `stt-rt-v5` 实时 WebSocket；音频会发送到 Soniox 云端，应用默认不保存音频。

## 文字稿与存档位置

- 每次录音结束后，SRT 文字稿自动保存到用户的“下载”文件夹，文件名类似 `Echo-20260910-143000.srt`。
- Archive JSON 保存在 `~/Library/Application Support/Echo/Archives/`，用于多段录音接续、恢复 Speaker/语言信息和全量 SRT 导出。
- 设置页面会显示文字稿保存位置；当前版本没有修改自动保存目录的功能。
- “导出全部字幕”会打开保存面板，可把当前存档另存到自选位置。
- 默认不保存音频。

## 从源码构建

在项目根目录执行：

```sh
xcodebuild -project macOS/EchoMac.xcodeproj -scheme Echo -configuration Debug -sdk macosx build
```

也可以执行：

```sh
chmod +x build_mac.command
./build_mac.command
```

脚本使用 Release 配置构建未签名的本地 app，并尝试打开构建结果。正式 Developer ID 签名、notarization、DMG 和 GitHub Release 流程见 [docs/release.md](docs/release.md)。

## 相关文档

- [产品路线图：Mac 体验优先，Windows 随后](docs/roadmap/README.md)（设计计划，尚未实现 Windows/移动端）
- [当前架构](docs/architecture.md)
- [数据模型与持久化](docs/data-model.md)
- [变更记录](CHANGELOG.md)
- [发布准备](docs/release.md)
