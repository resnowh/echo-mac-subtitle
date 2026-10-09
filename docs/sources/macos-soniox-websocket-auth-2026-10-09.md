# Mac Soniox WebSocket 认证迁移底稿

日期：2026-10-09

## 官方协议数据底稿

本次核对 Soniox 官方页面：

- [Speech-to-Text WebSocket API](https://soniox.com/docs/api-reference/stt/websocket-api)：实时转写启动 JSON 的 `api_key` 已弃用；密钥应随 WebSocket 连接发送。错误帧包含 `error_code`、`error_type`、`error_message` 和可选 `request_id`。
- [WebSocket authentication](https://soniox.com/docs/guides/websocket-authentication)：原生客户端可在升级请求头中发送 `Authorization: Bearer <API_KEY>`，启动消息不再带 `api_key`；无效 Key 可能在 WebSocket 握手成功后以 401 错误帧返回。
- [Migrate WebSocket authentication](https://soniox.com/docs/guides/migrate-websocket-authentication)：给出从启动消息 `api_key` 字段迁移到连接认证头的前后示例。
- [API errors](https://soniox.com/docs/api-reference/errors)：稳定错误类型可用于区分认证、余额/预算、权限、限额及可重试错误。

页面于 2026-10-09 查询。没有下载外部文件；本文记录协议摘要与实现所用的官方链接。

## 实现与测试

- Mac 用 `URLRequest` 在 WebSocket 握手中发送 Bearer Key；首条转写配置 JSON 删除 `api_key` 字段。
- Soniox 错误帧按状态码和 `error_type` 映射为认证、余额/预算、权限、限额及超时的中文处理建议；服务说明与 `request_id` 保留，远端控制字符会被清理。
- `tests/run_stream_checks.py` 的本机 WebSocket fixture 检查真实 transport 握手头为合成 Bearer Key、启动 JSON 不含 Key 字段和值。独立检查覆盖 401、402、403、429、408 和诊断文字清理。
- 测试只连接回环模拟服务，使用合成 Key，不连 Soniox 云端、不采集音频、不启动 Echo。

## CI 验证结果

- GitHub Actions [37916093546](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37916093546)（提交 `2da0002a4add6e41dc8229f65f5ca9cbed01b0f2`）的 Mac、Windows 与 Mac 归档回读三个 job 全部通过。
- Mac job 的 Swift 源码检查通过，包括结构化错误提示和本机 WebSocket 回环测试；回环握手确认 Bearer 认证头存在，启动 JSON 不包含 `api_key`。Mac Debug 与 Release 构建通过。
- Windows CoreChecks 与 Release x64 构建通过（0 警告、0 错误）。Windows 生成的合成归档由 Mac 生产 decoder 回读通过，日期、可选字段、说话人/语言及校对历史均保留。
- 验证没有启动 Echo，也没有使用真实服务账户、Key 或音频。

真实 Soniox 账户与真实 Key 的端到端仍需单独验收。
