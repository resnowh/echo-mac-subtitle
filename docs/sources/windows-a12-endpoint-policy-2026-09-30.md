# Windows A12 音频端点通知策略验证底稿

日期：2026-09-30
范围：默认端点跟随、固定设备丢失处理及无关系统通知过滤。

## 环境与方法

- Windows 11 build 10.0.26200；.NET SDK 10.0.401。
- 在 `Echo.CoreChecks --audio` 中构造合成设备路线和 `DeviceState`/flow/role 值，调用生产 `AudioEndpointChangePolicy`；未打开 Echo 窗口。
- 没有更改系统默认设备，没有拔插真实硬件，也没有改变蓝牙模式。

## 策略检查

| 输入事件 | 预期行为 | 结果 |
|---|---|---|
| 跟随默认的多媒体设备更换为不同 ID | 请求同一转写会话内切换采集端点 | 通过 |
| 固定选择的 USB Speaker 变为 Unplugged | 报设备丢失并停止采集，不静默换到系统默认 | 通过 |
| 活动状态通知或未使用设备 ID | 不作为设备丢失处理 | 通过 |
| 通知来自不匹配 flow 或 Communications/Console 角色 | 不触发默认多媒体路由切换 | 通过 |
| 系统重复通知当前仍为活动设备的默认 ID | 不重建采集 | 通过 |

最新 `dotnet run --project windows/Echo.CoreChecks -c Release -- --audio` 共 52 项通过；无云端调用、未保存音频。

## 边界

策略函数回归不能代替 Windows Core Audio 实际设备通知。本轮未验证耳机拔插、多个播放端点切换、系统无默认设备、蓝牙 A2DP/HFP profile 变化或驱动延迟；这些仍需硬件验收。

## 数据留存

只保留合成设备 ID、事件状态、断言和本说明；不保留本机真实设备 ID。
