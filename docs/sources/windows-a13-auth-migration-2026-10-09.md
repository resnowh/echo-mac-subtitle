# A13 Soniox WebSocket 认证迁移与错误恢复底稿

日期：2026-10-09
分支：`feature/windows-preview`
范围：实时转写 WebSocket 认证和错误处理；仅本机模拟服务，未启动 Echo 或连接 Soniox 云端。

## 官方协议数据底稿

本轮读取并核对 Soniox 官方文档：

- [Speech-to-Text WebSocket API](https://soniox.com/docs/api-reference/stt/websocket-api)：实时 WebSocket 的启动 JSON 中 `api_key` 已弃用；错误帧包含 `error_code`、稳定的 `error_type`、可显示的 `error_message` 和用于支持排查的 `request_id`。
- [WebSocket authentication](https://soniox.com/docs/guides/websocket-authentication)：桌面原生客户端可在连接请求头使用 `Authorization: Bearer <API_KEY>`；启动配置不应再带 `api_key`。服务端可能先完成 WebSocket 握手，再通过错误帧报告无效 Key。
- [API errors](https://soniox.com/docs/api-reference/errors)：401 表示未认证，402 表示账户余额/月度预算耗尽，403 表示权限不足，429 表示使用或并发限额；错误处理应优先按 `error_type` 分类。时长上限 `max_duration_reached` 要求新建 WebSocket 会话。

这些是本次核对时的官方文档状态，实施发布前仍应复查服务协议变化。

## 代码调整

- 在 `ClientWebSocket` 建连前设置 Authorization Bearer 请求头；密钥不再序列化进首条转写 JSON。
- 解析错误帧中的状态码、`error_type`、服务说明和 `request_id`，保留为结构化 `SpeechServiceException` 字段。
- 401 提示检查/更新 Key；402 提示检查余额与预算；403 提示检查实时转写权限；429 提示等待限额恢复或释放并发连接。以上均不自动重试。
- 408、500～599 保持有界重试；`max_duration_reached` 进入新 WebSocket 会话恢复。其他错误保留服务说明与请求编号，便于诊断。

## 验证结果

- 本机 `HttpListener` 模拟服务分别发送 `unauthenticated`、`organization_balance_exhausted`、`permission_denied`、`limit_exceeded`、`service_unavailable`、`max_duration_reached` 六种错误帧。
- 断言每次连接都带 `Authorization: Bearer synthetic`，启动 JSON 不含 `api_key`；分类、用户提示、请求编号和重试策略均与预期一致。另验证握手阶段 HTTP 401/503 分类以及连接取消行为。
- `dotnet run --project windows/Echo.CoreChecks/Echo.CoreChecks.csproj -c Release --no-restore`：49 项通过，无云端调用、无真实 API Key 使用、无音频保存。
- `dotnet build windows/Echo.Windows/Echo.Windows.csproj -c Release --no-restore /p:Platform=x64 /p:RuntimeIdentifier=win-x64`：成功，0 错误，10 条既有 NAudio 弃用警告。
- 提交 `b2c807ef1ddbd5d3064fd9e2cb15754f5c839371` 的 [GitHub Actions 运行 37901351866](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37901351866) 中 Windows 与 Mac jobs 均通过；Windows 锁定还原、49 项 CoreChecks 和 Release x64 构建通过，Mac transport 与 Debug/Release 检查通过。

## 未覆盖

未使用真实 Soniox 凭据验证外部服务接受请求头，也未验证真实账户余额、限额或云端断网恢复。当前 Windows 状态栏和保存后提示仍需人工界面检查；这些不由本机模拟服务替代。
