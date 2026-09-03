# Echo：英文语音转写与中文翻译

Echo 是一个仅支持 macOS 的原生实时字幕应用：从话筒采集英文语音，同时显示英文原文和简体中文翻译。

主界面可以选择三种输入源：

- `电脑音频`：识别 Mac 正在播放的声音。
- `话筒`：识别麦克风输入。
- `电脑音频和话筒`：同时识别电脑播放声音和麦克风输入。

主界面右上角可以循环切换浅色、深色和跟随系统，主题设置会保存在本机；设置菜单中也可以直接选择主题。

捕获电脑音频使用 macOS ScreenCaptureKit。第一次使用 `电脑音频` 或 `电脑音频和话筒` 时，需要在“系统设置 → 隐私与安全性 → 屏幕与系统音频录制”中允许 Echo；使用话筒时仍需要允许麦克风。录音过程中可以直接热切换输入源，Soniox 会话和已识别文字不会清空。

## 使用

1. 用 Xcode 打开 `macOS/EchoMac.xcodeproj`，运行 `Echo` Scheme；或者双击 `build_mac.command` 自动编译并启动。
2. 打开主界面的设置，在 `Soniox API Key` 输入框粘贴你的 Key。Key 只保存在本机的 UserDefaults 中。
3. 第一次录音时，在 macOS 弹窗中允许 Echo 使用麦克风。

Soniox Key 可从 [Soniox Console](https://console.soniox.com/) 创建。应用使用 Soniox `stt-rt-v5` WebSocket，实时返回英文原文和简体中文翻译。Soniox 是云端服务，因此需要网络连接并按 Soniox 计费；音频只通过实时连接发送，默认不会在本机保存。

设置中可以选择开启 AI 自动总结。开启后，应用会在每次停止录音后将本次带时间戳的英文文字稿发送给 DeepSeek，并在主界面生成中文的主题、要点和待办；该功能默认关闭，需要单独填写 DeepSeek API Key。录音过程中也可以点击“生成总结”，按当前已识别内容生成总结，不会停止录音。

支持 Apple Silicon 和 Intel Mac。项目不需要 Python、Node.js 或 Homebrew。

如果 macOS 因安全设置不让双击运行：右键 `build_mac.command`，选择“打开”；仍被拦截时，打开终端执行：

```sh
chmod +x build_mac.command
./build_mac.command
```

## 文字稿

每次录音结束后，应用会自动在“下载”文件夹保存 `Echo-日期时间.srt`。文件包含英文原文、中文翻译和时间戳；默认不保存音频。设置中可以修改文字稿保存目录，界面的“导出全部字幕”按钮可以把当前窗口中的所有字幕另存为新的 `.srt` 文件。

## 构建

```sh
xcodebuild -project macOS/EchoMac.xcodeproj -scheme Echo -configuration Debug -sdk macosx build
```
