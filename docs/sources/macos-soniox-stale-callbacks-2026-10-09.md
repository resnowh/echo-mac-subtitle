# Mac Soniox 旧连接回调隔离验证

日期：2026-10-09

## 问题与实现基线

检查生产实现 `macOS/Services/SonioxWebSocketClient.swift` 时发现：WebSocket 的 ready、message 和 failure 回调会异步投递到独立队列。取消或重连后，已经排队但尚未执行的旧连接回调仍可能到达 ViewModel，造成新会话状态被旧连接结果干扰。

实现为每次连接分配递增代号；取消时使代号失效。ready、message 和 failure 回调在执行前核对代号，旧连接排队回调不再触发。回调队列可注入，生产环境仍使用专用串行队列。

## 回归测试底稿

- 使用本机合成 WebSocket fixture 和测试专用串行队列，不访问 Soniox 云端、不使用凭据或音频设备。
- 先阻塞回调队列，再由 fixture 发送 message 并通过 WebSocket pong 确认客户端已处理到该帧；随后取消连接、释放队列，并断言旧连接的 ready 与 message 回调都未执行。
- fixture 只写入临时测试目录的同步标记；标记随测试临时目录清理，不包含外部或用户数据。

## 验证结果

GitHub Actions [37917332259](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37917332259)，提交 `22f40d42ab1f9ac8b1d3314ca5c7ec8e537ef58f`：

- Mac `Check PCM and loopback WebSocket transport` 通过，日志明确输出 `PASS: queued callbacks from a cancelled WebSocket are discarded`。
- Mac Debug/Release 构建通过；Windows CoreChecks、Release x64 与 Mac 生产解码器读取 Windows 归档的往返检查通过。
- 没有启动 Echo，没有连接真实服务，也没有使用真实 Key 或录音。

本检查证明取消/重连前已排队的回调会被丢弃；无法撤销已经开始执行的回调。ViewModel 仍以会话 UUID 对异步结果做额外校验。
