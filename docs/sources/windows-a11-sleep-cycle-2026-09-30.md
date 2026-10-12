# Windows A11 睡眠恢复状态机循环验证底稿

日期：2026-09-30
范围：应用睡眠时录音意图保存、唤醒恢复授权与用户取消。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 运行 `Echo.CoreChecks --audio`；直接驱动生产 `SleepRecoveryState` 状态机，不触发操作系统睡眠，不打开 Echo 窗口。
- 无真实音频或云端请求。

## 结果

1. 建立 10 个独立状态机实例，逐个执行 `BeginSleep(recordingIntended: true)`、`BeginWake()`、`BeginRecovery()`、`FinishRecovery()`；10 次均按预期完成，且结束时没有残留恢复任务。
2. 另一个状态机在唤醒后仍处于待恢复阶段时调用 `CancelByUser()`，随后 `BeginRecovery()` 返回 false，不会自动重启。
3. 最新全套 `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio` 共 51 项检查通过。测试不保存音频、不调用云端。

## 边界

- 这是确定性状态机模拟，不代表设备真的经历睡眠/唤醒；Windows 电源通知、外接设备重新枚举时机、恢复后新建录音段均未在本轮实测。
- A11 的真实设备 10 次睡眠循环仍是未完成的人工验收项。

## 数据留存

只保留状态转移代码、断言和本说明；没有录音或设备标识数据。
