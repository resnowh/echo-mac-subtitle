# Windows A06 说话人与语言元数据验证底稿

日期：2026-09-30  
范围：识别 token → 字幕模型/界面绑定 → Archive JSON → SRT 导出。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 使用仓库命令行回归和后台 Release x64 构建；未打开 Echo 窗口。
- 输入为测试代码中的合成 Soniox 风格 JSON token，不是真实录音，也未调用云端服务。

## 检查结果

1. 合成的两个最终 token 分别带 `speaker: 1, language: en` 与 `speaker: 2, language: ja`。Speaker 变化仍会拆成两行并分别最终化；每行保留相应语言，界面显示名仍匿名为 Speaker 1 / Speaker 2。
2. 字幕语言属性采用可观察属性；XAML 对语言代码进行单向绑定，仅在存在非空语言值时显示。Release 构建成功，确认 XAML 可编译；未做窗口视觉、缩放或读屏实测。
3. `Archive` JSON 序列化/解析往返保留 Speaker 与语言代码。生成的 SRT 对应文本包含 `[Speaker 2] [fr] bonjour`，并保留原文与译文。
4. `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio`：46 项检查通过，包含音频路径与本项序列化/导出回归。测试使用本机模拟 WebSocket；无 API Key、云端调用或音频文件落盘。
5. Release x64 `BuildAndRun.ps1 Echo.Windows.csproj -SkipRun -ExtraArgs '/p:Configuration=Release'` 构建通过，0 错误、5 条 NAudio 弃用警告；未启动应用。

## 边界

- 未对照真实线上 Soniox 响应验证字段在所有模型/语言下均存在；缺失字段时不会生成语言标签。
- 未做人工界面检查；语言代码与说话人标签并列后的实际布局、字体缩放和窄窗口表现仍需后续验收。
- SRT 的语言与说话人前缀是本版本新增格式，其他播放器会将其作为普通字幕文本显示。

## 数据留存

仅保留合成 JSON 测试输入、断言及本说明。未保存真实用户语音、设备 ID、API Key 或云端响应。
