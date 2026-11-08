# 文档导航

## 当前开发入口

- [项目总览](../README.md)：当前源码版本、Android／iOS 范围与最近正式发布的区别。
- [双平台整合验证](verification-platform-integration.md)：统一入口构建、Android 已完成的真机范围及明确未覆盖项。
- [统一构建与平台范围](building.md)：总脚本、产物、平台资源选择与签名边界。
- [本地开发与双平台构建](development.md)：固定环境、编辑器与各玩法专用验证命令。
- [架构与存档契约](architecture.md)：目录责任、共用逻辑、平台桥接及数据协议。
- [iOS 构建和验证](verification-ios-native.md)：iPhone 实证及仍未验证的 iPad／系统版本等范围。
- [iOS 自动续签](ios-auto-renew.md)：macOS LaunchAgent、固定快照、有效期与本机日志。
- [小游戏 UI 与开发参考](sokoban-development-guide.md)：可复用规则和有明确场景限制的取舍。
- [第三方许可](../THIRD_PARTY_NOTICES.md)：资源与运行库来源和授权边界。

- [操作与体验整理](gameplay-polish.md)：本轮八项改动、存档兼容与验证边界。
- [终局回看审查](result-review-audit.md)：数独遮挡根因、九款终局截图矩阵及本轮验证证据。

## 按阶段保留的证据

`releases/` 记录实际发布版本；`verification-*.md` 记录特定设备、包和阶段的验证结果。标题或正文中的旧版本范围应按当时条件理解，不能据此认定当前分支已经通过同样验证。

- [首版 Android 固定存档回归工具](device-testing.md)：要求可调试主包及 `run-as`，不适用于当前 Debug／Release 构建。
- [Android／iOS 发布 v0.9.6](releases/v0.9.6.md)
- [历史 Android 发布 v0.9.4](releases/v0.9.4.md)
- [iOS 端接入及后续声音、触感、续签记录](verification-ios-native.md)
- [大厅设计与实现归档](design/lobby-v2/README.md)
- [方案与实施计划目录](superpowers/)
- [研究记录目录](research/)

历史设计、研究、原型和复盘继续保留原路径，避免破坏已有引用。新增当前使用说明放在上述入口并建立链接；新的设备结果使用独立验证记录，写清源码版本、构建产物、实际通过项和未验证项。
