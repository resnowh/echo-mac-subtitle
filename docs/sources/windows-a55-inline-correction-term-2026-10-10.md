# A55 字幕纠正面板内添加术语

日期：2026-10-10

## 对照基线与源码哈希

- Mac 权威提交：`origin/main` `ae0359dc90da0ccb5e526a275da1747954a49a4f`。
- Windows 远端起始提交：`a2b25aa3097265788c72168abb18cc97c5185f0b`。
- Mac `macOS/Views/TranscriptViews.swift`：`805C242A93A45830F355AD14A91DF567E166BDF1F36D697FB2A3D9D2538140E4`。
- Mac `macOS/ViewModels/SpeechViewModel.swift`：`BD79AA7588FF43E4EF3DB4A198B35C525C741D03BCF2AC8E3765C9A3DA3A0F8D`。
- Windows `windows/Echo.Windows/Core/CorrectionTermList.cs`：`E4B2A428D56636C70962DAB52345657A9C81C856B4A313EABBC3DF03B702AAB9`。
- Windows `windows/Echo.Windows/ViewModels/MainPageViewModel.cs`：`E245E45F14550DD417CA1022338601E83E4D23C61CBA99CB5FD40E627E8D3EEC`。
- Windows `windows/Echo.Windows/MainPage.xaml.cs`：`1D15CE7A748638099532070ED1EE495320064552A29904E6CC513FA67737B1C8`。
- Windows `windows/Echo.CoreChecks/Program.cs`：`075FAE336C4C83C0CF33139458CFB6A3741B564940600307CE262AE698449983`。

## 差异与实现

Mac `SubtitleCorrectionEditor` 在编辑字幕时提供“加入术语表”字段；添加后立即持久化，并提示 Soniox 识别提示从下次连接生效。Windows 原先只能去设置页编辑术语列表，无法在处理当前字幕时就地添加。

Windows 纠正面板现可输入并添加术语。新增逻辑按 Mac 规则去除首尾空白、忽略空项、拒绝大小写不敏感重复、最多 100 项、每项不超过 80 个 Unicode 文本元素；成功后即时保存设置，同步设置页的文本框，并显示下次建连生效提示。新建 session 时现有请求 builder 会读取这些术语。

## 验证与限制

- 本机 CoreChecks 130 项通过；新检查覆盖规范化、重复、空输入、100 项上限、80 个 Unicode 文本元素边界和面板控件接线。
- Windows Release x64 构建成功，0 警告、0 错误；`git diff --check` 通过。
- 没有进行 DeepSeek/Soniox 云端调用，没有采集音频。
- GUI 未运行：机器上仍有前台隔离 A50 浮层预览实例；本轮没有向它或其他 Echo 窗口发送交互。因此文本输入、状态展示及实时保存后的视觉仍待隔离 UIA 验收。
- 本次仅修改 Windows 源码、测试和文档；未修改 `macOS/`。
