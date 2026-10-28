# 俄罗斯方块实现与验证

2026-10-28。工作树 `tetris-research/XanaduYonder`，分支 `p/duanyao.0516/codex/tetris-research`。按用户要求 fetch 远端 master，并将 `9262b30` 合入本工作树（合并提交 `17c447a`）。新版主线的 UiFactory 和推箱子 UI 规范未改变，沿用已确认的独立竖屏方案；在黄金矿工后动态追加第 14 张卡。正式 v0.6.0 发布仍为十三款，本分支没有发布新版本。

## 实际交付

- 纯 C# 规则：10×20 可见区、顶部两隐藏行、七袋、双向 SRS、Next3、Hold、Ghost；基础消行分与软/硬降分，无进阶奖励分。
- 60 Hz 固定步，10 行升一级，重力间隔 `max(0.2, 0.85^(level-1))` 秒并量化到规则步。首次接地保留30步调整，最多15次有效重置；悬空到期不空中锁定，也不能复活计时，重新接地立即锁定。
- 独立页面：准备、运行、暂停、3秒继续、结束、重开确认；声音/触感使用公共设置，消行闪光120ms仅影响视觉。Home/Book/Settings在暂停层上仍可使用，说明分为两页。
- 输入：按下即动、180ms后每60ms重复；两指组合、同帧左右冲突不动、第三触点取消；旋转/直落/Hold绑定活动块序号，换块后软降须先松开。
- `tetris-v1.json` 严格校验、原子写入、合法备份、完整规则快照。没有开始时 Game 为 null；未来版本/读取失败不覆盖，双份损坏须用户明确归档重置。生命周期及锁定/Hold/终局立即保存，运动中每秒检查点，dirty/revision避免无变化重复写盘。
- 实际 Unity 图集：[UI 示例](prototypes/tetris/unity.html)。原研究原型仍保留历史来源，不伪装成生产游戏。

## 已执行验证

| 检查 | 结果 | 范围 |
| --- | --- | --- |
| `scripts/test-tetris.sh` | 27 个场景通过 | 七袋、SRS墙/地面、O/I、消行/跨级、锁定、顶部失败、计数边界、恢复与随机动作 |
| `scripts/test-tetris-input.sh` | 17 个场景通过 | 暂停/固定步/倒计时、长帧、DAS、双指/第三指、同帧冲突、丢失UP、滑入/滑出、换块清理 |
| `scripts/test-tetris-persistence.sh` | 19 组通过 | 严格JSON、64位值、主备保护、实际文件失败、动态快照及相同未来续演、其他游戏隔离 |
| `scripts/test-tetris-layout.sh` | 324 个断言通过 | 40组响应尺寸与两组原型尺寸、方格和操作区布局 |
| `scripts/test-tetris-ui.sh` | 149 个断言通过 | 实际UGUI/生产Page、射线命中、Hold禁用、长数字、错误状态、消行、生命周期、宿主卡片和真实帮助文字尺寸 |
| 原有 `scripts/test-core.sh` | 43 项通过 | 2048 核心 |
| 原有 `scripts/test-input.sh` | 10 项通过 | 公共滑动输入 |
| 原有 `scripts/test-persistence.sh` | 13 项通过 | 2048与公共设置持久化 |

核心、输入和存档均保留先失败后通过的开发记录。原有 `Tests/Core` 的递归源码引用误包含 Sokoban JSON 解析器却没有 Newtonsoft 依赖，基线失败为 CS0246；本次将其编译范围收回实际测试的 Core 根目录，其他游戏保留自己的独立工程。

独立规格审查发现并复验关闭了帮助正文溢出、同帧左右操作依赖枚举顺序两项问题；随后独立代码质量审查没有发现其他可证实的阻塞缺陷。最终视觉检查额外去掉了错误提示重复，并补充不可读纪录状态：异常存档不再显示零纪录或冒充新玩家；新增失败用例已验证修正后通过。SRS完整表人工核对过，但测试没有逐一执行每个kick候选，不将其描述为穷尽证明。

## 渲染与构建环境

Unity **6000.3.27f1**。生产UGUI在实际Canvas/Camera上渲染到388×784与388×688图像，棋盘为237×474与194×388。操作区与Hold的命中框至少48个对应设计像素，彼此无重叠；这不是Android物理48dp的设备测量。

UI检查与Android构建在同一工作树串行运行，使用本树Library、`Library/TetrisGradle`及独立ADB端口；未复用其他工作树的Library/Temp，未终止共享ADB服务。构建范围包含当前全部十四款游戏，以独立包名隔离数据。

首次UI检查已生成页面截图，但测试夹具误在Play模式内调用已有GoldMiner构建设置验证，触发QualitySettings恢复断言并中止。已移除此跨阶段检查；后续UI检查单独完成，真实Android构建验证临时设置恢复，Git中没有ProjectSettings变化。另有编辑器已有的QuickSearch初始化异常和HappyBall.audio隐藏成员警告；它们不属于新游戏源码编译错误，日志保留在 `artifacts/logs/`。

最终UI报告已随 [Unity图集](prototypes/tetris/unity-screenshots/report.txt)保存，23张PNG涵盖12种状态。浏览器已实际打开图集并验证状态切换。

`scripts/build-tetris-validation.sh` 最终返回成功，APK读取校验：

| 字段 | 实际值 |
| --- | --- |
| 文件 | `artifacts/builds/XanaduYonder-0.6.0-tetris-validation.apk` |
| 包名 | `com.duanyao.xanaduyonder.tetristest` |
| 版本 | 0.6.0 / versionCode 6 |
| ABI | arm64-v8a（IL2CPP） |
| Android | minSdk 26 / targetSdk 36 |
| 类型 | Development 验证包，可调试；正式APP签名/版本不变 |
| 大小 | 139,793,904 bytes |
| 构建完成 | 2026-10-28T22:54:32+08:00 |
| SHA-256 | `e61c91bccfcd85d616e9b2658806382048ef9d4bf314817ebf1b7c22f46575d9` |

Manifest含Development构建的INTERNET权限；俄罗斯方块实现没有联网逻辑。该包用于本地验收，不作为正式发布包。产物旁有 `.sha256` 校验文件；构建日志为 `artifacts/logs/build-tetris-validation.log`。

## 验证边界

- 已检查ADB设备列表，当前没有连接设备。因此没有安装或实测物理触摸、安全区、持续帧率、声音、触感和设备后台恢复。
- Unity宿主检查实际创建大厅卡和公共帮助弹窗；没有自动点击大厅入口跑完整AppController端到端流程。页面本身使用生产类及隔离临时存档测试，路由/离页/生命周期接线经过独立源码审查。
- Unity图集是固定示例夹具的真实渲染，不是用户纪录或真机截图。没有宣称全合集全量回归，也没有执行正式签名发布。
- 检查点保存无法保证系统强杀时零进度损失；最近一次成功保存才是跨进程恢复边界。
