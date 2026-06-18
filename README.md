# Blue Battery

Blue Battery 是一个轻量 Windows 托盘工具，用来显示当前连接的蓝牙低功耗设备电量。

它只显示 Windows 能读取到标准 BLE Battery Service 电量的设备。不支持私有协议，不负责蓝牙配对、连接管理或厂商驱动能力扩展。

## 当前范围

- 显示当前连接的 BLE 设备电量。
- 只使用 Windows 暴露的标准 BLE Battery Service。
- 托盘图标显示最低设备电量。
- 托盘 tooltip 显示设备电量摘要。
- 小面板显示设备名称、电量百分比、读取来源和状态。
- 支持手动刷新、后台自动刷新、诊断信息和开机自启动。
- 设备电量低于或等于 20% 时，在托盘 tooltip 和面板底部显示低电量提醒。

## 不支持

- 不支持私有协议电量读取。
- 不显示只存在于历史配对记录里的设备。
- 不保证所有蓝牙耳机、鼠标、键盘都能显示电量。
- 不提供蓝牙连接、断开、配对、重命名或驱动修复功能。

## 使用方式

启动 `blue-battery.exe` 后，应用会常驻托盘。

- 左键点击托盘图标：打开或关闭面板。
- 右键点击托盘图标：打开菜单，可刷新或退出。
- 面板右下角齿轮：开机自启动、清理开机自启动项、刷新、诊断信息。
- `Esc`：关闭当前面板或面板内的浮层。

如果设备没有显示，先确认 Windows 设置页里是否能看到该设备电量。Blue Battery 只显示 Windows 能读取到的标准电量。

## 开机自启动和注册表

开机自启动默认关闭。启用后，Blue Battery 只写入当前用户的 Run 项：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Value name: Blue Battery
```

关闭开机自启动会删除这个 `Blue Battery` 值。portable 版本移动或删除后，如果担心留下旧路径，可以在面板右下角齿轮中选择“清理开机自启动项”。该操作只删除 `Blue Battery` 这个 Run 值，不删除应用文件，也不影响其他应用。

## Demo 构建

生成 release 可执行文件：

```powershell
npm run tauri -- build
```

生成 demo zip：

```powershell
npm run demo:package
```

release exe 路径：

```text
src-tauri/target/release/blue-battery.exe
```

不要把 `src-tauri/target/debug/blue-battery.exe` 作为 demo 发给别人。debug 版本会显示控制台窗口，release 版本使用 Windows GUI 子系统。

demo zip 会包含：

- `blue-battery.exe`
- `README.md`
- `CHANGELOG.md`
- `DEMO_NOTES.txt`
- `THIRD_PARTY_NOTICES.txt`

## 故障排查

### 不显示设备

可能原因：

- 设备没有连接。
- 设备不是 BLE 设备。
- 设备没有暴露标准 BLE Battery Service。
- Windows 暂时读取失败。
- 设备电量只通过厂商私有协议提供。

可以打开面板右下角齿轮里的诊断信息查看最近刷新结果。

### 看到历史配对设备

这是不符合预期的行为。Blue Battery 的显示入口应该只基于当前连接的 BLE 设备枚举。

### 托盘图标没有变化

可以手动刷新一次。后台刷新默认每 60 秒执行一次。

## 开发验证

```powershell
npm run test:panel
cargo test --manifest-path src-tauri/Cargo.toml
npm run tauri -- build --debug
```
