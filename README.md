# blue_battery

一个常驻系统托盘的 WinUI 3 蓝牙电量面板应用。

## 简单介绍

`blue_battery` 面向 Windows 桌面环境，目标是以尽量轻量、原生、贴近系统风格的方式显示当前已连接蓝牙设备的电量。

首版只支持 Windows 公开标准路径可读取到电量的设备，不尝试兼容私有协议或厂商扩展。

## 技术栈

- C#
- WinUI 3
- Windows App SDK
- MSIX
- Win32 托盘互操作
- Bluetooth LE + GATT Battery Service

## 功能特性

- 常驻系统托盘，左键展开电量面板，右键打开快捷菜单
- 单实例运行，重复启动会重定向到主实例
- 读取已连接 Bluetooth LE 设备的 BAS 电量
- 支持 `DeviceWatcher` 自动刷新
- 支持 GATT 通知订阅与轮询回退
- 支持托盘最低电量聚合图标
- 支持缓存恢复、刷新失败缓存和断开态保留
- 面板为无标题栏的紧凑弹出样式
- 设置界面当前支持“开机自启动”

## 项目结构

```text
blue_battery/
├─ blue_battery/              WinUI 3 应用项目
│  ├─ Diagnostics/            诊断与单实例日志
│  ├─ Interop/                Win32 互操作
│  ├─ Models/                 设备与状态模型
│  ├─ Resources/Strings/      文案资源
│  ├─ Services/               蓝牙、托盘、状态持久化服务
│  ├─ ViewModels/             面板视图模型
│  └─ Views/                  面板窗口
```

## TODO

- 细化断连、失败和异常状态
- 补齐少量状态与交互细节

## 安装

当前发布包采用自签名证书，仅适合你本人和小范围朋友手动安装。

推荐直接在发布目录中运行 `Install.ps1`，它会自动处理证书检查、依赖安装和主包安装。

如果需要手动安装：

1. 先安装发布目录里的证书文件 `blue_battery_0.1.0.0_x64.cer`
2. 导入位置选择“当前用户”
3. 证书存储选择 `TrustedPeople`
4. 证书导入完成后，再运行 `blue_battery_0.1.0.0_x64.msix`

如果系统仍提示发布者不受信任，通常是证书没有导入到正确位置，重新确认导入目标为 `CurrentUser\\TrustedPeople`，或者直接改用发布目录里的 `Install.ps1`。

## 已知限制

- 首版只支持 `Bluetooth LE + GATT Battery Service` 路径可读取到电量的设备
- 不兼容私有协议、厂商扩展或系统可显示但公开 API 不稳定的设备
- 当前设置界面只实现了“开机自启动”

## 发布前检查

- `Debug x64` 构建通过
- `Release x64` 构建通过
- `MSIX` 打包通过
- `AppsFolder` 启动验证通过
- `AppsFolder -> AppsFolder` 单实例验证通过

## 许可证

本项目采用 `GNU General Public License v3.0`。

详见 [LICENSE](LICENSE)。
