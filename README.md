# Echo：英文语音转写与中文翻译

## macOS 使用（推荐）

1. 用 Xcode 打开 `macOS/EchoMac.xcodeproj`，运行 `Echo` Scheme；或者双击 `build_mac.command` 自动编译并启动。
2. 在原生 Mac 界面的 `Soniox API Key` 输入框粘贴你的 Key。Key 只保存在本机的 UserDefaults 中。
3. 第一次录音时，在 macOS 弹窗中允许 Echo 使用麦克风。

Soniox Key 可从 [Soniox Console](https://console.soniox.com/) 创建。原生版使用 Soniox `stt-rt-v5` WebSocket，实时返回英文原文和简体中文翻译。Soniox 是云端服务，因此需要网络连接并按 Soniox 计费；音频只通过实时连接发送，不会在本机保存。

如果 macOS 因安全设置不让双击运行：右键 `build_mac.command`，选择“打开”；仍被拦截时，打开终端执行：

```sh
chmod +x build_mac.command
./build_mac.command
```

支持 Apple Silicon 和 Intel Mac。项目原生版本不需要 Python、Node.js 或 Homebrew。

## 文字稿

每次录音结束后，原生版会自动在“下载”文件夹保存 `Echo-日期时间.srt`。文件包含英文原文、中文翻译和时间戳；默认不保存音频。界面的“导出全部字幕”按钮可以把当前窗口中的所有字幕另存为新的 `.srt` 文件。

## 浏览器旧版

仓库中的 `server.py`、`start_mac.command` 和 `index.html` 仍保留原来的 OpenAI 批处理网页版本，需单独配置 `OPENAI_API_KEY`。要使用 Soniox 实时双语字幕，请使用上面的原生 Mac 版。
