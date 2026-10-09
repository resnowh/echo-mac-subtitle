# Windows 功能对齐计划

本文的 Mac 源码基线是 `ae0359dc90da0ccb5e526a275da1747954a49a4f`。逐项现状和证据见 [mac-parity-matrix.md](mac-parity-matrix.md)。按优先级推进，每项完成后更新矩阵与测试底稿。

## P0：识别正确性和数据

- 以 Mac 的 `handleSonioxMessage` 和 Soniox 当前事件协议为参考，为 Windows 建立相同输入序列：每条响应都整体替换 provisional 原文/译文快照、final 追加、speaker/语言元数据变化、原文后到达的翻译、统一双语端点及重连边界。provisional 语言只初始化空标签，final token 才能更新既有标签；Mac 将 `<end>`/`<fin>` 作为响应级标记，Windows 在完整处理响应后最多结束当前双语字幕一次，不由可选的 `translation_status` 决定游标。
- WebSocket 鉴权遵守 Soniox 当前官方推荐：Windows 在连接握手发送 `Authorization: Bearer <key>`，起始配置不包含 `api_key`；Mac 当前仍在配置中携带 `api_key`。该旧方式目前仍受支持，但官方文档给出 2027-01-15 的迁移截止日期。按任务约束保留 Mac 源码原样，Windows 不回退；A37 保存官方协议和本地模拟服务检查证据。
- A57 核对并更正 Key 存储基准：Mac 的 `saveAPIKey/saveSummarySettings` 使用 `UserDefaults`，Windows 继续使用当前用户 DPAPI。若 DPAPI 密文不可解密，保存其他设置会保留该密文并提示重填；替换值仍会经 DPAPI 加密，正常解密后的空字段仍可清除。138 项 CoreChecks 覆盖保留、替换和清空；见 [A57](../sources/windows-a57-dpapi-secret-recovery-2026-10-10.md)。
- A59 用合成采集器和本地 WebSocket 验证音源启动失败后恢复旧输入、继续发送且不重建连接；不打开真实音频设备。真实 WASAPI 连续性、权限拒绝及拔插仍未验收，见 [A59](../sources/windows-a59-audio-switch-rollback-2026-10-10.md)。
- A60 在 A59 同一 loopback 会话中注入默认话筒变化，验证 `SpeechSession` 收到端点事件后重启采集、切到新默认端点并继续向同一 WebSocket 发送 PCM；失败回滚与默认端点变化两条路径共用 141 项 CoreChecks。真实设备拔插、权限弹窗和声学间隙仍待验，见 [A60](../sources/windows-a60-default-device-change-2026-10-10.md)。
- A35 已在 macOS CI 直接运行当前生产 `SpeechViewModel.handleSonioxMessage` 与 `TranscriptModels.swift`，Windows CI 将相同输入的 Mac 输出逐响应交给 Windows `TokenAssembler` 比较字幕、speaker、语言、时间和定稿次数。首轮对拍修正了 Windows 同响应 speaker 行为；A42 加入错误类型字段回退；A43 又追加五条生产响应，覆盖强语境经济学纠正、微积分词汇及中文翻译联动、保守负例和 han/hand 上下文纠正。Actions run `37966271007` 的 18 响应 Mac production differential、Windows 116 项 CoreChecks、Release x64 构建及 Mac Archive 回读全部通过。Mac 原始输出与 SHA-256 见 [A43 底稿](../sources/windows-a43-correction-parity-2026-10-10.md)。云端真实负载仍待验。
- A44 对齐传输控制帧：Mac 会处理仅含字符串 `error_message` 的错误，并仅把布尔 `finished: true` 视为结束；Windows 现采用相同的宽容类型判断，同时保留 error-code-only 帧的 Windows 服务诊断。两个边界通过本地 loopback WebSocket 验证，含停止流程；见 [A44 底稿](../sources/windows-a44-soniox-control-frames-2026-10-10.md)。真实云响应仍待验。
- 七响应序列已追加“已有字幕行在下一响应遇到 speaker 变化”场景。Mac production 输出与 Windows `TokenAssembler` 的 95 项 CoreChecks 已在本机和 GitHub Actions 通过；真实云响应仍未覆盖。
- A36 对照 Mac `PCM16TimelineMixer` 与 Windows 双路捕获发现：Windows 的 resampler 将欠载补零后仍固定除以音源总数，短暂缺帧会压低另一条正常输入。Windows 现保留真实读取帧数，逐样本只平均有效来源，双方均无样本时才输出静音；新增确定性检查，97 项 CoreChecks 和 Release x64 构建通过。实现/限制记录见 [A36 audio mix parity](../sources/windows-a36-audio-mix-parity-2026-10-10.md)。
- A39 对齐 Mac 的本地静默后备计时：除服务错误及 `finished` 响应外，每条有效 Soniox 响应都会刷新安静计时，包括空 token 和 endpoint-only 响应；Windows 不再只依赖普通语音 token。固定响应检查、100 项 CoreChecks 与 Release x64 构建通过。Mac 源码依据、哈希和限制见 [A39 Soniox quiet activity](../sources/windows-a39-soniox-quiet-activity-2026-10-10.md)。
- A40 对齐录音 meter：输入来源样本分别计算 Mac 缩放 RMS，按 20Hz 用 Mac 快攻慢放系数平滑，并使用 48 个真实 meter 样本绘制波形；静音时不再显示无关的正弦动画。104 项 CoreChecks 与 Release x64 构建通过。混合模式调度和实机输入强度仍待验，底稿见 [A40 audio meter parity](../sources/windows-a40-audio-meter-parity-2026-10-10.md)。
- A51 对齐 Mac 录音中切换音源模式：三种模式与 Mac 顺序一致，默认话筒并持久化；Windows 菜单在录音时继续可用，通过当前 session 的音频切换事务重启所需 WASAPI 源，不重建 Soniox WebSocket 或字幕段。切换时排空有界采集尾部；失败先回滚原模式，原设备也无法恢复时保存并停止。126 项 CoreChecks 覆盖缺省/兼容设置、模式路由和 XAML 可用性；真实设备、权限拒绝及连续性仍待验收，见 [A51](../sources/windows-a51-live-audio-mode-switch-2026-10-10.md)。
- A54 复核语言配置：Mac automatic 模式清空 hints，Windows 自动请求同样省略 `language_hints`/`language_hints_strict`；specified 模式发送单个语言和 strict 偏好。128 项 CoreChecks 新增两种模式切换请求契约。Soniox 官方页面原始快照与 SHA-256 见 [A54](../sources/windows-a54-soniox-refresh-2026-10-10.md)。真实服务结果未测试。
- Mac 对经济学/微积分识别结果的强语境词汇纠正和 Soniox 请求上下文（领域、主题、背景文本、39 个术语、26 组中英译词及用户自定义词）已移植到 Windows；请求契约、自定义词边界（含组合字符/ZWJ emoji 截断）及纠正规则有自动检查。数据底稿见 [A26 Soniox context baseline](../sources/windows-a26-soniox-context-2026-10-09.md)；纠正前后 production handler 的 Mac/Windows runtime 对拍仍待补充。
- A32 跨平台 CI 现为双段合成 Archive 使用 Mac 生产 encoder/SRTExporter 生成数据，经 Windows 导入回写，再用 Mac 生产 decoder/SRTExporter 检查 ID、日期、metadata、校对历史与 SRT。当前提交的三个相关 Actions job 在 run `37948130637` 均通过；真实用户历史 Archive 仍需广泛互操作验证。来源清单见 [A27 archive order baseline](../sources/windows-a27-archive-order-multisegment-2026-10-09.md) 与 [A32 runtime pipeline](../sources/windows-a32-archive-runtime-pipeline-2026-10-09.md)。
- A45 对齐 Mac `prepareArchiveForRecording()` 的新存档标题：采用“课程 MM-dd HH:mm”和本机当地时间。固定时间标题用例通过；用户现存 Archive 的广泛历史互操作仍未验证，来源见 [A45](../sources/windows-a45-archive-title-parity-2026-10-10.md)。
- 保持存档原子写入、备份和恢复行为；发现损坏文件时明示，不静默覆盖。
- Soniox/API 错误按 HTTP 状态、断线、超时分类；仅对可重试错误有限重试，并显示用户可理解的错误。

## P1：产品主路径

- 已完成：把识别与目标语言改为受支持语言选择菜单；自动识别和不翻译都是显式选项。
- 已完成：字幕滚动跟随、用户查看历史时暂停、到达新内容提示及回到底部。A56 使被动字幕布局变化不再误判为用户滚动；状态迁移和输入事件接线通过 134 项 CoreChecks，触控板/鼠标滚轮实机行为仍待 UI 验收。
- 已完成：Mac 四类设置导航和 Soniox/local segmentation 配置已接入；默认值、范围、说明、下一会话快照和本地兜底策略有自动检查。DeepSeek 自动总结开关（默认关闭）与停止后总结已接入。数据底稿见 [A24 Soniox segmentation source baseline](../sources/windows-a24-soniox-segmentation-2026-10-09.md)。
- 已完成：实现悬浮字幕窗口和单会话字幕投影，设置和 final/迟到译文保留语义有自动检查；A38 对齐翻译关闭时强制显示原文及每路最多两行的尾部省略规则；A50 将浮层切换为 Win32 layered HWND + Win2D/Direct2D 透明合成，并在隔离 GUI 验证点击穿透与调整模式。多屏/DPI、全屏和虚拟桌面仍待验收，不得产生额外音频或网络流。
- 对齐 Mac 的设置保存、授权、设备状态、错误恢复和窗口尺寸等可见行为。

## P2：AI 与易用性

- 总结新增/当前段/完整存档、增量去重、Mac 同款 Markdown 分块、默认展开、收起/展开和复制已接入。A41 补齐 Mac 同款面板显隐条件、面板内状态反馈和录音提示，并用 CoreChecks 覆盖显隐条件；真实 GUI 排版、自动总结生命周期及云端响应仍待验收。呈现底稿见 [A29 summary panel baseline](../sources/windows-a29-summary-panel-2026-10-09.md) 与 [A41 summary panel parity](../sources/windows-a41-summary-panel-parity-2026-10-10.md)。
- 字幕纠正按改动字段提交：未编辑字段读取当前最新识别值，保持继续接收识别更新；编辑器显示新识别提示，可载入最新文本，关闭未保存修改时先确认。原始识别稿和每条修订（时间、原文、译文）可展开查看；长内容在受限滚动视口中呈现。可在编辑器内加术语，立即保存并从下一次 Soniox 建连应用；长度、重复和数量限制对齐 Mac。AI 校对继续采用建议后人工确认；不把模型输出自动写回字幕。云端结果和该编辑器交互仍待 GUI 核对。来源清单见 [A28](../sources/windows-a28-ai-correction-2026-10-09.md)、[A53](../sources/windows-a53-correction-editor-scroll-2026-10-10.md) 与 [A55](../sources/windows-a55-inline-correction-term-2026-10-10.md)。
- Soniox 与 DeepSeek 请求均使用 Mac 固定模型；Windows 不再暴露不同模型输入。继续核对 Key 保护、网络失败、费用提示及云端行为。

## 完成定义

- 矩阵中每项均有具体代码证据和状态，平台限制有解释。
- P0/P1 有稳定自动检查；真实 UI、设备和发布检查在独立底稿注明环境和结果。
- Mac/Windows 合成档案双向兼容检查通过。
- Windows Release x64 构建通过，CI 不启动应用、不请求云服务、不录制用户设备。
- 安装包经真实代码签名，并在干净 Windows 用户环境验证安装、升级、卸载/回滚。
- 不改 `macOS/` 源码或现有 Mac 行为来迁就 Windows。
