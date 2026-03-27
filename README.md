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
├─ dev_docs/                  过程文档与开发记录
└─ .github/workflows/         基础 CI
```

## 开发计划

当前阶段已经完成：

- 项目骨架与基础构建链路
- 托盘壳层与单实例
- 蓝牙最小闭环
- 面板主要状态表达与 UI 收敛
- 基础打包与正式回归验证

当前仍待完成：

- 蓝牙编排边界继续收敛
- 少量状态与交互细节补齐
- 开源元数据和发布准备继续完善

详细过程可见 `dev_docs/`。

## 许可证

本项目采用 `GNU General Public License v3.0`。

详见 [LICENSE](LICENSE)。
