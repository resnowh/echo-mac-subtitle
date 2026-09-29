# Windows A13 会话断线与重试分类验证底稿

日期：2026-09-30  
范围：WebSocket 握手失败分类、用户取消和录音会话建立后的意外断开。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 使用 `Echo.CoreChecks --audio` 与本机回环 `HttpListener` WebSocket 模拟服务；未连接真实 Soniox 服务，也未打开 Echo 窗口。
- 会话关闭用例禁用麦克风采集，客户端发送服务配置及静音音频帧；测试未落盘任何音频。

## 检查结果

1. 握手 HTTP 401 被分类为不可重试的 `SpeechServiceException`。
2. 握手 HTTP 503 被分类为可重试服务异常；策略最多两次重试，间隔为 1 秒、3 秒。
3. 连接等待期间由用户取消会返回取消状态，而不是把取消误判为网络超时。
4. 服务端接受会话配置并收到首个音频帧后，主动发送异常关闭帧且不发送 `finished`。客户端 `Failure` 事件收到 `IOException`，`SpeechRetryPolicy.IsTransient` 判为 true，允许进入有限重试流程。
5. 最新全套 `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio` 共 53 项检查通过；无云端请求、API Key 或音频文件。

## 实现检查与边界

- `MainPageViewModel` 对短暂网络异常先关闭旧会话、排空并保存当前字幕，再为重试建立新 Segment；重试失败会保存 Archive 并提示用户手动重试。该 ViewModel 路径本轮只做静态核查，没有接入可注入的模拟会话端到端运行。
- 未实测真实断网/恢复、HTTP 配额错误在界面的完整呈现、重连期间点击停止后从窗口保存以及真实服务恢复后的两段 Archive 内容。

## 数据留存

仅保留合成 HTTP/WebSocket 消息、断言和本说明。没有真实用户语音、服务凭据或云端响应。
