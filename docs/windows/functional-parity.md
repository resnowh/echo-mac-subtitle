# Windows 功能对齐计划

本文的 Mac 源码基线是 `ae0359dc90da0ccb5e526a275da1747954a49a4f`。逐项现状和证据见 [mac-parity-matrix.md](mac-parity-matrix.md)。按优先级推进，每项完成后更新矩阵与测试底稿。

## P0：识别正确性和数据

- 以 Mac 的 `handleSonioxMessage` 和 Soniox 当前事件协议为参考，为 Windows 建立相同输入序列：每条响应都整体替换 provisional 原文/译文快照、final 追加、speaker/语言元数据变化、原文后到达的翻译、统一双语端点及重连边界。provisional 语言只初始化空标签，final token 才能更新既有标签；Mac 将 `<end>`/`<fin>` 作为响应级标记，Windows 在完整处理响应后最多结束当前双语字幕一次，不由可选的 `translation_status` 决定游标。
- A35 已在 macOS CI 直接运行当前生产 `SpeechViewModel.handleSonioxMessage` 与 `TranscriptModels.swift`，Windows CI 将相同输入的 Mac 输出逐响应交给 Windows `TokenAssembler` 比较字幕、speaker、语言、时间和定稿次数。首轮对拍修正了 Windows 同响应 speaker 行为；五响应基线与哈希见 [A35 runtime fixture](../sources/windows-a35-soniox-runtime-fixture-2026-10-09/mac-soniox-runtime.json) 和 [A35 record](../sources/windows-a35-soniox-production-handler-parity-2026-10-09.md)。
- 七响应序列已追加“已有字幕行在下一响应遇到 speaker 变化”场景。Mac production 输出与 Windows `TokenAssembler` 的 95 项 CoreChecks 已在本机和 GitHub Actions 通过；真实云响应仍未覆盖。
- Mac 对经济学/微积分识别结果的强语境词汇纠正和 Soniox 请求上下文（领域、主题、背景文本、39 个术语、26 组中英译词及用户自定义词）已移植到 Windows；请求契约、自定义词边界（含组合字符/ZWJ emoji 截断）及纠正规则有自动检查。数据底稿见 [A26 Soniox context baseline](../sources/windows-a26-soniox-context-2026-10-09.md)；纠正前后 production handler 的 Mac/Windows runtime 对拍仍待补充。
- A32 跨平台 CI 现为双段合成 Archive 使用 Mac 生产 encoder/SRTExporter 生成数据，经 Windows 导入回写，再用 Mac 生产 decoder/SRTExporter 检查 ID、日期、metadata、校对历史与 SRT。当前提交的三个相关 Actions job 在 run `37948130637` 均通过；真实用户历史 Archive 仍需广泛互操作验证。来源清单见 [A27 archive order baseline](../sources/windows-a27-archive-order-multisegment-2026-10-09.md) 与 [A32 runtime pipeline](../sources/windows-a32-archive-runtime-pipeline-2026-10-09.md)。
- 保持存档原子写入、备份和恢复行为；发现损坏文件时明示，不静默覆盖。
- Soniox/API 错误按 HTTP 状态、断线、超时分类；仅对可重试错误有限重试，并显示用户可理解的错误。

## P1：产品主路径

- 已完成：把识别与目标语言改为受支持语言选择菜单；自动识别和不翻译都是显式选项。
- 已完成：字幕滚动跟随、用户查看历史时暂停、到达新内容提示及回到底部（仍待 UI 实机验收）。
- 已完成：Mac 四类设置导航和 Soniox/local segmentation 配置已接入；默认值、范围、说明、下一会话快照和本地兜底策略有自动检查。DeepSeek 自动总结开关（默认关闭）与停止后总结已接入。数据底稿见 [A24 Soniox segmentation source baseline](../sources/windows-a24-soniox-segmentation-2026-10-09.md)。
- 已完成：实现悬浮字幕窗口和单会话字幕投影，设置和 final/迟到译文保留语义有自动检查；桌面透明/点击穿透/多屏/DPI 仍待 GUI 验收，不得产生额外音频或网络流。
- 对齐 Mac 的设置保存、授权、设备状态、错误恢复和窗口尺寸等可见行为。

## P2：AI 与易用性

- 总结新增/当前段/完整存档、增量去重、Mac 同款 Markdown 分块、默认展开、收起/展开和复制已接入；summaryStatus 与面板可见条件仍待对齐/GUI 验收。呈现底稿见 [A29 summary panel baseline](../sources/windows-a29-summary-panel-2026-10-09.md)。
- 字幕纠正按改动字段提交：未编辑字段读取当前最新识别值，保持继续接收识别更新；编辑器显示新识别提示，可载入最新文本，关闭未保存修改时先确认。AI 校对继续采用建议后人工确认；不把模型输出自动写回字幕。校对/重新翻译提示、Mac 固定模型、thinking 开关、必需响应字段及手动任务优先级有合成契约检查；云端结果和 UI 交互仍待核对。来源清单见 [A28 AI correction request baseline](../sources/windows-a28-ai-correction-2026-10-09.md)。
- Soniox 与 DeepSeek 请求均使用 Mac 固定模型；Windows 不再暴露不同模型输入。继续核对 Key 保护、网络失败、费用提示及云端行为。

## 完成定义

- 矩阵中每项均有具体代码证据和状态，平台限制有解释。
- P0/P1 有稳定自动检查；真实 UI、设备和发布检查在独立底稿注明环境和结果。
- Mac/Windows 合成档案双向兼容检查通过。
- Windows Release x64 构建通过，CI 不启动应用、不请求云服务、不录制用户设备。
- 安装包经真实代码签名，并在干净 Windows 用户环境验证安装、升级、卸载/回滚。
- 不改 `macOS/` 源码或现有 Mac 行为来迁就 Windows。
