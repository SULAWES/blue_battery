# Blue Battery Changelog

## 0.1.0

Public portable zip release for Windows.

### Included

- Shows currently connected Bluetooth LE devices whose battery level is readable through Windows standard BLE Battery Service APIs（标准 BLE Battery Service）.
- Displays the lowest readable battery level in the Windows notification area tray icon.
- Uses Microsoft Fluent UI System Icons battery assets, pre-rendered at build time into runtime RGBA data.
- Provides a compact tray panel with device name, battery percentage, read source, status, diagnostics, manual refresh, background refresh, and low battery status.
- Supports current-user startup through `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Provides a settings-menu action to clean the `Blue Battery` startup registry value for portable release users.
- Packages Fluent UI System Icons license text in `THIRD_PARTY_NOTICES.txt`.

### Not Included

- 不支持私有协议电量读取。
- 不负责蓝牙配对、连接、断开、重命名或驱动修复。
- 不显示只存在于历史配对记录里的设备。
- 不提供安装器、自动更新、系统服务或云同步。

### Known Limits

- Some Bluetooth devices do not expose a standard Battery Service to Windows, even if vendor apps can show battery level.
- Background refresh is fixed at 60 seconds in this release.
- Low battery threshold is fixed at 20% in this release.
- The portable zip is unsigned, so Windows may show a SmartScreen warning on first run.
