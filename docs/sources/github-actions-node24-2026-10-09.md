# GitHub Actions Node.js 24 版本基线

核对日期：2026-10-09。来源为 GitHub 官方 Actions 仓库的最新 release API 与对应版本 `action.yml`；工作流使用 GitHub-hosted `macos-latest` / `windows-latest` runner。

## 官方版本与 runtime

| Action | 官方最新标签（核对时） | `runs.using` | 官方记录 |
|---|---:|---|---|
| checkout | `v7.0.1` | `node24` | [release](https://github.com/actions/checkout/releases/tag/v7.0.1) · [action.yml](https://github.com/actions/checkout/blob/v7.0.1/action.yml) |
| upload-artifact | `v7.0.2` | `node24` | [release](https://github.com/actions/upload-artifact/releases/tag/v7.0.2) · [action.yml](https://github.com/actions/upload-artifact/blob/v7.0.2/action.yml) |
| download-artifact | `v8.0.2` | `node24` | [release](https://github.com/actions/download-artifact/releases/tag/v8.0.2) · [action.yml](https://github.com/actions/download-artifact/blob/v8.0.2/action.yml) |
| setup-dotnet | `v6.0.0` | `node24` | [release](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0) · [action.yml](https://github.com/actions/setup-dotnet/blob/v6.0.0/action.yml) |

artifact v7/v8 使用 Node 24，并要求 Actions runner `2.327.1` 或更新版本；本工作流的 GitHub-hosted runner 实际兼容性由下方 CI 运行验证。本工作流使用的 action 输入（checkout 默认仓库、命名 artifact 的上传/下载、`dotnet-version: 10.0.x`）在新主版本仍保留。checkout v7 的不安全 fork 检查仅针对 `pull_request_target` / `workflow_run`；本项目只用 `push` 与 `pull_request`。

## 本轮实施与验证

- 更新 `.github/workflows/ci.yml` 中全部 checkout、artifact upload/download 和 setup-dotnet action 主版本；不改变 job 依赖、命令、artifact 名称或路径。
- 触发后的 GitHub Actions 结果会追加在此处。该工作流会执行真实 Mac Debug/Release 构建、无设备核心检查、Windows Release x64 构建和跨平台 Archive 往返；不会签名、安装或启动 Echo。
