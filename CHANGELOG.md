# Blue Battery Changelog

## 0.2.0

Configurable tray demo release for Windows.

### Added

- Adds configurable background refresh intervals: 30 seconds, 60 seconds, and 120 seconds.
- Adds configurable low battery threshold choices: 10%, 15%, 20%, and 25%.
- Adds a quiet low-battery status toggle for tray tooltip and panel footer warnings.
- Adds a stored system-notification preference for future Windows Toast support without enabling intrusive notifications by default.
- Adds structured diagnostics for disconnected devices, unreadable devices, devices without standard Battery Service, and read failures.
- Adds diagnostic copy actions for the full diagnostics report and a compact device summary.
- Adds GitHub Actions workflows for Windows CI and tag-based portable zip releases.

### Changed

- Avoids duplicate refresh scans when a manual refresh overlaps with a background refresh.
- Applies short failure backoff after unsuccessful refresh attempts.
- Keeps the frontend panel passive after startup and lets the backend own background refresh scheduling.
- Continues visual polish toward a lightweight Windows-native tray panel with Mica material and Fluent battery icons.

### Not Included

- 不支持私有协议电量读取。
- 不负责蓝牙配对、连接、断开、重命名或驱动修复。
- 不提供安装器、自动更新、系统服务或云同步。
- 不发送 Windows Toast 通知；当前只保存相关偏好，为后续版本预留。

### Known Limits

- Some Bluetooth devices do not expose a standard Battery Service to Windows, even if vendor apps can show battery level.
- The portable zip is unsigned, so Windows may show a SmartScreen warning on first run.
- Windows notification support is planned but not implemented in this release.

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
