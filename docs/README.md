# 文档导航

## 当前源码与开发入口

Android 和 iOS 共用 **29 个大厅入口**，包括 25 个 Unity 原生玩法和 4 个离线网页玩法。源码构建号为 **0.9.8 / build 17**。下载已发布的 APK 或 IPA 时，请按下方发布记录核对该包包含的玩法。

| 要做的事 | 从哪里开始 |
| --- | --- |
| 接入新玩法或复用已有组件 | [公共组件与接入示例](shared-components.md)、[弹窗与设置规范](dialogs.md) |
| 在编辑器运行项目 | [项目总览](../README.md)、[工具链与启动场景](development.md#打开项目) |
| 查找游戏规则、页面或存档 | [玩法模块与存储](architecture.md#玩法模块与存储)、[Core](../Assets/Scripts/Core/)、[Runtime](../Assets/Scripts/Runtime/) |
| 检查大厅、导航和共享交互 | [大厅入口清单](../Assets/Scripts/Runtime/Lobby/AppController.Lobby.cs)、[大厅设计](design/lobby-v2/README.md)、[弹窗与设置规范](dialogs.md)、[小游戏 UI 开发参考](sokoban-development-guide.md)、[项目整理基线](project-refactor-baseline.md) |
| 执行模块测试 | [按模块选择验证](development.md#按模块选择验证)、[独立测试项目](../Tests/)、[Unity 编辑器检查](../Assets/Editor/) |
| 构建 Android 或导出 iOS | [统一构建命令、平台清单与签名](building.md)、[iOS 自动续签](ios-auto-renew.md) |

## 玩法、数据与资源

| 模块 | 维护入口 |
| --- | --- |
| 成语接龙、飞花令 | [成语数据与导入](idiom-data.md)、[飞花令现行玩法](feihualing-research.md)、[诗库构建与台账](../Data/Feihualing/README.md) |
| 棋类、数独、推箱子 | [四款棋类规则](../Assets/Scripts/Core/BoardGames/)、[五子棋维护记录](gomoku-development-record.md)、[数独题库](sudoku-content.md)、[推箱子数据](../Data/Sokoban/) |
| 抓大鹅、连连看、黄金矿工 | [抓大鹅规则与 3D 原型](bakery-basket-prototype.md)、[连连看设计与分档](link-match-research.md)、[黄金矿工源战役迁移](gold-miner/source-campaign-migration.md) |
| 四个离线网页玩法 | [赵云与阿斗](verification-zhaoyun-adou.md)、[割绳子固定来源包](../ThirdParty/CutTheRopeContent/README.md)、[只有一道门内容构建](../ThirdParty/LevelDevil/README.md)、[泡泡堂宿主与续关](bomb-it.md) |
| 公共图标、字体与原始资料 | [Unity 随包资源](../Assets/Resources/)、[大厅图标](design/lobby-v2/README.md)、[数据输入](../Data/)、[第三方许可](../THIRD_PARTY_NOTICES.md) |

## 历史验证与发布

| 记录 | 适用范围 |
| --- | --- |
| [Android v0.9.8](releases/v0.9.8.md)、[Android v0.9.7](releases/v0.9.7.md)、[Android 与 iOS v0.9.6](releases/v0.9.6.md)、[全部发布](releases/) | 对应版本的附件、变更与验收，最近一次 iOS 公开附件为 v0.9.6 的未签名 IPA |
| [双平台入口整合](verification-platform-integration.md)、[游戏精简与 iOS 网页适配](verification-ios-web-games.md)、[iOS 原生构建与设备记录](verification-ios-native.md) | 各次提交的构建环境、产物、实测设备和仍待验证项目 |
| [体验调整](gameplay-polish.md)、[结算回看](result-review-audit.md)、[首版设备存档工具](device-testing.md) | 阶段性专项验收。首版设备工具依赖可调试包的 `run-as` 权限 |
| [飞花令早期交互稿](feihualing-research-2026-11-22-draft.md)、[研究资料](research/)、[交互原型](prototypes/)、[设计规格](superpowers/specs/)、[实施计划](superpowers/plans/) | 调研和设计过程。飞花令早期稿保留 16 种组合，现行设计含竞速，共 24 种 |

历史报告中的 `artifacts/` 是执行机器生成的目录。可复跑的脚本和固定输入链接到仓库。原设备的截图、日志与旧构建产物保留当时的路径。新增验证请写明提交、执行命令、产物、设备和未覆盖项。
