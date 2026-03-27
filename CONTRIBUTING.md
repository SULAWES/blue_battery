# 贡献指南

感谢你关注 `blue_battery`。

## 基本原则

- 保持 Windows 原生体验优先
- 首版范围只做公开标准接口
- 小步提交，优先保证每一步都可构建
- 不把生成物、临时文件和本地 IDE 状态提交进仓库

## 开发环境

- Windows
- Visual Studio 2022 或 2026
- .NET 8 SDK
- Windows App SDK / WinUI 3 开发组件

## 本地开发

1. 使用 Visual Studio 打开 `blue_battery.slnx`
2. 选择 `x64` 配置
3. 构建项目，确认 `Debug` 可通过
4. 涉及打包行为验证时，先重新部署最新 `AppX`
5. 再通过 `shell:AppsFolder\\包家族名!App` 验证真实启动路径

## 提交建议

- 提交信息尽量简洁明确
- 一次提交只解决一类问题
- UI 调整和功能调整尽量分开提交

## Issue / PR 建议

- 说明问题场景、预期行为和实际行为
- 如涉及蓝牙设备，请说明设备类型和是否支持 BAS
- 如涉及托盘、单实例、打包问题，请说明使用的是普通构建验证还是 `AppsFolder` 已部署包验证
