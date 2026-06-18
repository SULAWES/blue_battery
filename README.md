# Blue Battery

Blue Battery is a lightweight Windows tray app for checking Bluetooth battery levels.

它只显示 Windows 能读取到的当前连接蓝牙低功耗设备电量。读取路径限定为标准 BLE Battery Service，不支持私有协议，也不负责蓝牙配对、连接管理或厂商驱动能力扩展。

## Status

- Current version: `0.1.0`
- Release type: portable Windows demo zip
- Platform: Windows desktop
- Repository: <https://github.com/SULAWES/blue_battery>

0.1.0 的目标是验证核心体验：托盘图标显示最低电量，展开面板显示 Windows 当前可读的蓝牙电量。安装器、自动更新、复杂设置页和厂商私有协议都不在这个版本范围内。

## Features

- 显示当前连接的 BLE 设备电量。
- 只使用 Windows 暴露的标准 BLE Battery Service。
- 托盘图标显示所有可读设备中的最低电量。
- 托盘 tooltip 显示设备电量摘要和低电量提醒。
- 小面板显示设备名称、电量百分比、读取来源和连接状态。
- 支持手动刷新、后台自动刷新、诊断信息和开机自启动。
- 设备电量低于或等于 20% 时，在托盘 tooltip 和面板底部显示低电量提醒。
- 使用 Microsoft Fluent UI System Icons 的电池图标资源，并在构建期预渲染为运行时 RGBA 数据。

## Scope

Blue Battery 是一个“显示 Windows 已经知道的电量”的工具。它不会尝试绕过 Windows 蓝牙栈，也不会实现厂商私有协议。

支持：

- 当前已连接的 BLE 设备。
- Windows 能通过标准 BLE Battery Service 读取到的电量。
- Windows 托盘、tooltip、小面板和当前用户开机自启动。

不支持：

- 不支持私有协议电量读取。
- 不显示只存在于历史配对记录里的设备。
- 不保证所有蓝牙耳机、鼠标、键盘都能显示电量。
- 不提供蓝牙连接、断开、配对、重命名或驱动修复功能。
- 不提供安装器、自动更新、系统服务或云同步。

如果某个设备在 Windows 设置页中也不显示电量，Blue Battery 通常也无法显示它。

## Download

发布包会放在 GitHub Releases：

<https://github.com/SULAWES/blue_battery/releases>

下载 `BlueBattery-demo-v0.1.0.zip` 后解压，运行其中的 `blue-battery.exe`。0.1.0 是未签名 portable zip，Windows 首次运行时可能显示 SmartScreen 提示。

## Usage

启动 `blue-battery.exe` 后，应用会常驻托盘。

- 左键点击托盘图标：打开或关闭面板。
- 右键点击托盘图标：打开菜单，可刷新或退出。
- 面板右下角齿轮：开机自启动、清理开机自启动项、刷新、诊断信息。
- `Esc`：关闭当前面板或面板内的浮层。

后台刷新默认每 60 秒执行一次。需要立即更新时，可以从托盘菜单或面板设置菜单手动刷新。

## Startup And Registry

开机自启动默认关闭。启用后，Blue Battery 只写入当前用户的 Run 项：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Value name: Blue Battery
```

关闭开机自启动会删除这个 `Blue Battery` 值。portable 版本移动或删除后，如果担心留下旧路径，可以在面板右下角齿轮中选择“清理开机自启动项”。该操作只删除 `Blue Battery` 这个 Run 值，不删除应用文件，也不影响其他应用。

## Privacy

Blue Battery 不包含账号、云同步或遥测逻辑。它读取本机 Windows 蓝牙 API 暴露的设备与电量信息，并把最近刷新状态保留在应用进程内用于诊断面板显示。

## Build From Source

需要准备：

- Windows
- Rust toolchain
- Node.js and npm
- Tauri 2 所需的 Windows WebView2 Runtime

安装依赖：

```powershell
npm install
```

开发运行：

```powershell
npm run tauri:dev
```

生成 release 可执行文件：

```powershell
npm run tauri -- build
```

也可以使用项目脚本生成 demo zip：

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

## Development Checks

```powershell
npm run test:panel
cargo test --manifest-path src-tauri/Cargo.toml
npm run tauri -- build --debug
```

`release/`、`dist/`、`target/` 等生成产物不进入 git。

## Troubleshooting

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

### 启动后出现黑色控制台窗口

请确认运行的是 release 版本，也就是 `src-tauri/target/release/blue-battery.exe` 或 GitHub Release zip 中的 `blue-battery.exe`。debug 版本会显示控制台窗口。

## Roadmap

- 设置持久化：刷新间隔、低电量阈值、启动行为。
- 更完整的设置面板：诊断、启动项清理、关于页面和版本信息。
- 正式安装包：签名、卸载流程和自动启动清理。
- GitHub Actions：自动构建、测试和 release 产物上传。
- 更清晰的设备状态说明：区分不可读、未连接、读取失败和无标准服务。

## Third-Party Assets

托盘电池图标使用 Microsoft Fluent UI System Icons。发布 zip 中会包含 `THIRD_PARTY_NOTICES.txt`，其中记录相关许可文本。

## License

项目代码许可尚未声明。公开协作或正式开源发布前，建议添加明确的 `LICENSE` 文件，例如 MIT 或 Apache-2.0。
