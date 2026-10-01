# Focus Star

Windows 与 macOS 本地专注时间记录器。界面包含今日进度、软件统计和历史图表，两端的重叠专注时间只计一次。

## 使用

从 [Releases](https://github.com/Sang-Hyorin/focus-star/releases) 下载系统对应 ZIP，解压到可写目录。

- Windows 10/11：需要 .NET Framework 4.8，打开 `LocalActivityRecorder.exe`。关闭窗口仍在托盘记录，选择 **Exit Focus Star** 才退出。
- macOS 13+：Apple Silicon / Intel 通用构建，打开 `Focus Star Mac.app`。应用与配置、记录目录应放在一起。关闭窗口仍会记录，菜单栏 **Quit** 才退出。应用未经过 Apple 公证，可能需要在系统“隐私与安全性”中明确允许。

公开版本不自动安装登录启动项。软件通过白名单、前台应用和键鼠空闲时间估算专注，不记录输入文字、窗口标题、文档名或网址；它并不能证明实际工作或生产力。

## 跨设备与设置

在界面修改规则，或参考 `settings.example.json`。Windows 使用 `settings.json`，Mac 单独使用 `settings-mac.json`。将两端应用与记录目录放入同一共享目录，使用自己选择的同步软件同步；每台设备独立写入 UUID 子目录。一般每十分钟提交共享记录，退出、暂停等生命周期事件会触发提交。同步完成时间由同步软件决定。

Windows Transfer 支持 `.focusstar` 备份导入、导出；Mac Transfer 当前仅打开报告与目录。手动完整备份应复制整个记录目录。统计日界固定为 UTC+08:00，旧记录没有时区，导入时请保持设备时间和时区一致。已结算总量采用高水位保留，因此缩小白名单之后总量可能高于当前可读取的明细。

## 隐私与构建

时间戳、应用名、空闲时间、判定原因和设备 UUID 都属于活动信息，请保护记录目录。公开仓库和安装包不包含真实活动记录、个人设置、设备记录或统计网页。不要在公开 issue 上传这些文件。

Windows 构建：`./scripts/build-windows.ps1 -Test`，需要 .NET SDK 与 .NET Framework 4.8。Mac 构建：`bash scripts/build-macos.sh --test`，需要兼容的 Xcode Command Line Tools。测试使用合成记录，真实锁屏、睡眠和云同步仍应在设备上验证。

MIT 许可证。详细说明见 [英文 README](README.md)。
