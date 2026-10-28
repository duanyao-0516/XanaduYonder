# 推箱子合入最新 master：验证记录

日期：2026-10-28。交付目标为 `origin/master`；本次以远端 `68a54c2`（欢乐球球集成）为主线基准，合并推箱子 UI 实现 `9c03c6d`、开发经验文档 `fe8e6df` 及后续集成修正。保留主线版本 0.5.0 / Android versionCode 5，不创建正式 Release。

开发知识主文档为[推箱子开发复盘与小游戏 UI 参考规范](sokoban-development-guide.md)，从根 README 与[开发说明](development.md)均可进入。文档、截图、原型和源码链接全部位于仓库；生成日志及 APK 遵循现有 `artifacts/` 忽略规则。

## 合并结果与游戏数量

主线原有 11 款顺序保持不变，推箱子追加第 12 款。数量由生产 `AppController.BuildLobby` 的实际注册与 `MiniGameUiChecks.CheckLobby` 的完整顺序共同核对。

| 顺序 | 小游戏 |
| --- | --- |
| 01 | 2048 |
| 02 | 跳一跳 |
| 03 | 旸了个旸 |
| 04 | 合成大西瓜 |
| 05 | 成语接龙 |
| 06 | 数独 |
| 07 | 扫雷 |
| 08 | Zuma（祖玛） |
| 09 | Dino（小恐龙） |
| 10 | 五子棋 |
| 11 | 欢乐球球 |
| 12 | 推箱子 |

本次完整源码及新构建含 **12 款**。GitHub 当前正式 `v0.5.0` 仍为前 **10 款**；欢乐球球和推箱子是之后合入的内容。正式发布状态已通过 GitHub CLI 回读，不能仅因新开发包沿用 0.5.0 版本号就把它当作已发布的同一产物。

## 集成处理

- 合并大厅注册、页面枚举、构造、路由、保存、暂停、Back、状态提示和前后台回调，其他页面进入时退出推箱子。
- 保留推箱子构造与进入的 `WidthOnly` 一致性，以及入口预览不制造存档的行为。
- 保留主线 Zuma 的异步退出、宿主输入门禁及回竖屏流程，Dino 的方向处理，以及五子棋、欢乐球球的现有生命周期。
- `ProjectBuild` 保留主线全部构建入口和 0.5.0 / code 5；推箱子独立构建脚本同步使用 0.5.0 输出名，避免误检查旧 APK。
- 大厅回归名单改为 12 款，末卡和页脚断言指向推箱子；原有 Zuma 宿主输入检查保留。
- 同步修正旧 README、架构、来源说明和内容文档中关于缩放、仓库展示名、测试数及“尚未安装”的过时陈述。源素材、地图和许可证原文字节未因文档整理改变。

独立源码复核未发现剩余阻塞项。复核与测试各自提供不同证据，不以审查替代运行验证。

## 合并后重新执行的检查

| 检查命令 | 结果 |
| --- | --- |
| `scripts/test-sokoban.sh` | 8 组通过 |
| `scripts/test-sokoban-persistence.sh` | 18 组通过 |
| `scripts/test-sokoban-input.sh` | 593 项断言通过 |
| `scripts/test-sokoban-content.sh` | 20 项导入测试、19 项目录拒绝检查、108 关回放和完整非终局撤回通过；资源目录逐字节可复现 |
| `scripts/test-sokoban-ui.sh` | 4,158 项断言通过，含真实宿主、短屏、窄网格、续局与末关 |
| `scripts/test-minigame-ui.sh` | 243 项断言通过，含 12 款顺序、编号及末卡页脚 |
| `scripts/test-dino-page.sh` | 24 个场景、543 项断言，0 失败 |
| `scripts/test-zuma-navigation.sh` | 9 个场景、39 项断言，0 失败 |
| `scripts/test-gomoku-ui.sh` | 1,062 项断言通过 |
| `scripts/test-happy-ball-ui.sh` | 294 项断言通过 |
| `scripts/build-android.sh` | 完整 Android IL2CPP / ARM64 开发包构建成功 |
| 文档相对链接、冲突标记和 `git diff --check` | 通过 |

UI 检查与构建在本工作树串行运行；普通构建额外继承推箱子环境脚本，隔离 Gradle 与 ADB。原始输出在 `artifacts/validation/sokoban-integration/`，构建日志为 `artifacts/logs/build-android.log`，其中记录 `Build Finished, Result: Success.`。本次覆盖实际合并影响的共享宿主及相关页面，没有把未重跑的其他独立规则测试标成新证据。

## 本次完整开发包

- 文件：`artifacts/builds/XanaduYonder-0.5.0.apk`。
- 包名：`com.duanyao.xanaduyonder`；版本 0.5.0，code 5。
- aapt 核对：minSdk 26、targetSdk 36、`arm64-v8a`。
- 大小：82,010,623 字节。
- SHA-256：`770d56f84fd9231bf3aabb3de6d876b364cdc17b2d343be5b8cfbd97f60ef3cb`。

这是完整的 **12 款游戏开发包**，采用本机调试签名，不是已发布的正式签名包。本轮没有安装它，也没有替换手机上的正式应用或此前推箱子独立验证包。

此前 `0.4.0-sokoban-validation.apk` 已安装 OPPO 并成功启动，详见[原实现验证记录](verification-sokoban.md)。该设备证据不覆盖本次合并包。Unity 中方向就绪使用受控测试状态，不能替代真实 Android 旋转、触摸、安全区、系统 Back、音频与性能验收。
