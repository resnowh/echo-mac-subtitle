# 2026-10-09 origin/main 同步底稿

同步日期：2026-10-09
目标分支：`feature/windows-preview`

## 远端提交快照

本轮开始时，`origin/main` 已从 Windows 分支共同基点 `c7b9e37b475f0eb468f78d37a83c4c6673adf1be` 前进至 `6ed817df8edd31500ef85a9c40250970ca1cda40`；共有 4 个 main-only 提交：

- `9bb090a` Refine live waveform rendering
- `f2156f0` Revert "Refine live waveform rendering"
- `db78517` Add configurable transcript segmentation
- `6ed817d` Add quick subtitle language switching

快照差异只涉及 `macOS/` 下的转写、语言切换与波形代码，以及 Mac 检查和 `tests/` 文档。共 8 个文件、746 行新增、93 行删除；不包含 Windows 源码改动。

## 同步与验证

通过 `git merge origin/main` 将上游提交合并到 Windows 分支，生成合并提交 `d41687bb6e7fab10b57b0e9ca04595dc710ca88c`，没有冲突，并已推送。对应 [GitHub Actions 运行 37899278925](https://github.com/resnowh/echo-mac-subtitle/actions/runs/37899278925) 两个 job 全通过：Windows locked restore、46 项 CoreChecks、Release x64 build；Mac transport checks、Debug 和 Release build。

该底稿记录的是公开 Git 提交元数据和文件差异，没有提取私密仓库资料、凭据或录音。
