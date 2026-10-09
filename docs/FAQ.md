# Echo 常见问题（FAQ）

[返回 README](../README.md) · [文档中心](README.md)

### 可以直接下载 Echo 吗？

目前**没有公开、经 Apple Developer ID 签名并公证的 macOS DMG**。macOS 开发版本可以从源码在 Xcode 构建。不要将普通 CI 的 unsigned build 视为已经验证可公开安装的版本。发布准备参见 [release.md](release.md)。

### 支持哪些系统？

当前主要版本为 **macOS 15+**，支持 Apple Silicon 和 Intel 的 Xcode 构建。Windows 原生预览工程位于 [独立分支](https://github.com/resnowh/echo-mac-subtitle/tree/feature/windows-preview)，尚无公开正式安装包。移动客户端尚未实现。

### 是否免费？需要账号吗？

目前没有 Echo 自己的付费订阅和账户系统。需要用户自行申请并填写 **Soniox API Key**（BYOK）；Soniox 可能按量收费。可选 DeepSeek AI 功能还需要对应 API Key，亦可能产生服务费用。Echo 不提供免费 API 额度，也不承诺第三方服务免费。

### 可以完全离线使用吗？

不可以。语音识别／翻译依赖 Soniox 云端网络服务；AI 校对与总结依赖 DeepSeek。历史 Archive 与已经保存的 SRT 属于本地数据，具体操作仍受当前应用功能限制。

### Echo 会保存音频吗？会把文字发送出去吗？

Echo 默认**不保存原始音频**，但实时音频会通过网络发往 Soniox 用于识别与翻译。启用／请求 DeepSeek 功能时，相关**文字**会发往 DeepSeek；不发送音频。请遵守课堂、会议及所在地录音相关的告知和授权规则。

### API Key 安全吗？

当前版本仍将 Key 保存在本机 **UserDefaults**，尚未迁移到 Keychain，故还不满足正式发行前的凭据加固目标。不要把 API Key 粘贴在 Issues、日志、截图或公开仓库中。

### 为什么无法识别麦克风或电脑声音？

先确认输入源、macOS 麦克风授权与“屏幕与系统音频录制”授权。电脑音频经 ScreenCaptureKit 采集，不是所有应用和受保护内容都保证可捕获。还应检查网络、Soniox Key、额度和服务状态。权限或系统音频故障不能只凭 CI 构建通过就断言已解决。

### 为什么切换语言后当前录音没变？

识别和翻译配置在**下一个 Soniox 会话**生效，以免修改正在录音的连接及 token 状态。输入源切换与语言切换是不同操作；不要为了更新语言设置而中断重要录音。

### 字幕会自动保存在哪里？

Archive JSON 默认在 `~/Library/Application Support/Echo/Archives/`；录音段 SRT 默认在用户 `Downloads` 文件夹。默认不保存原始音频；已有旧档的恢复和导出规则见 [数据模型](data-model.md)。

### 可以像视频字幕一样悬浮在其他应用之上吗？

**已经有独立透明双语悬浮字幕窗的源码实现和隔离自动测试**，可通过主窗口「悬浮字幕」入口打开，并在专用设置中调整字号、透明度、位置和点击穿透。但尚未完成真实 Mac GUI、多显示器与全屏应用的验收；目前不保证所有桌面/Space 都能可靠覆盖。详见 [功能说明](features/desktop-subtitle-overlay.md)。

### 怎么反馈问题？

到 [Issues](https://github.com/resnowh/echo-mac-subtitle/issues) 提交复现步骤、系统版本、输入模式及预期/实际行为。不要包含密钥或私人录音；如果属于高风险数据问题，先移除敏感信息再反馈。

### 是开源软件吗？允许自行分发吗？

目前仓库公开可读，但尚无 `LICENSE`。在确定许可证之前，不应将公开访问理解为已经取得复制、修改、再分发等授权。商业模式和许可方式仍在考虑中。
