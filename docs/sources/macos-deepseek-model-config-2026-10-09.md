# Mac DeepSeek 模型 ID 配置基线

日期：2026-10-09

## 官方协议数据底稿

- [DeepSeek 更新日志，2026-09-10](https://api-docs.deepseek.com/updates/)：DeepSeek-V4.1-Flash 的新 API 名为 `deepseek-flash`；旧 `deepseek-v4-flash` 暂时路由到该版本，官方称这是临时兼容。
- [Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion/)：`model` 是必填项；当前文档列出的有效 ID 为 `deepseek-flash` 和 `deepseek-v4-pro`。
- [模型列表 API](https://api-docs.deepseek.com/api/list-models/)：可查询当前模型 ID 和元数据，说明模型目录会变化，不应将单个模型 ID 固定为永久客户端协议。

页面于 2026-10-09 查询。没有调用模型 API、提供或读取用户凭据，也没有下载外部文件；本文保存协议摘要和官方来源链接。

## 实现范围

- Mac 设置新增 DeepSeek 模型 ID，默认使用公告中的当前新 ID `deepseek-flash`；总结和校对请求共用设置。
- 模型 ID 仅允许 1～128 个 ASCII 字母、数字、点、下划线或连字符；使用当前用户 `UserDefaults` 持久化。输入格式验证不能证明模型在服务端仍存在。
- 回归检查覆盖默认值、自定义 `deepseek-v4-pro` 请求、总结与校对两种请求、非法 ID 拒绝及隔离 UserDefaults 保存/加载。
- 真实 API Key 和模型响应未测试，Echo 未启动。

## 验证结果

GitHub Actions [37918780584](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37918780584)，提交 `f68af20dcfdde03649be359df743a70f2c51d972`：

- Mac `Check PCM and loopback WebSocket transport` 通过，日志明确输出 `PASS: configurable DeepSeek model ID, safe validation, and isolated persistence`；Mac Debug/Release 构建通过。
- Windows CoreChecks、Release x64 和 Mac 生产 decoder 对 Windows 归档的往返检查通过。
- 没有启动 Echo，没有调用 DeepSeek API，没有使用真实 Key。模型 ID 是否仍被服务端接受，仍需用户填入自己的 Key 后实际验证。
