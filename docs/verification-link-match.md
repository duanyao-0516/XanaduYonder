# 连连看开发与验证记录

日期：2026-10-28。实现位于独立工作树 `link-match-research/XanaduYonder`，分支 `p/duanyao.0516/codex/link-match-research`，基于已拉取的 `origin/master` 提交 `0f6e4fd691e405020da49128c3034cb808797738`。原 checkout 的未提交文档未改动；没有合并主线、推送或发布。

## 实现范围

已接入大厅第 13 张游戏卡，提供选模式 / 选关 → 独立竖屏对局 → 继续 / 重开 / 结算的真实流程。入口预览不创建活动局，所有时间、剩余水果、配额、完成进度及续局都来自生产模型。

| 约定 | 实现与验证 |
| --- | --- |
| 两种模式、三档棋盘 | 轻松不限时；挑战 90 / 120 / 150 秒，3 次提示、2 次主动洗牌。24 / 36 / 48 张，每种水果 4 张，开局前选择 |
| 连线与可解性 | 不同位置、相同水果、水平 / 垂直最多两次转弯；允许外圈；消除后不落子。生成与洗牌携带完整消除证明并经生产 matcher 回放 |
| 无路可连 | 自动免费洗牌，保持空位和剩余水果多重集；重排计算不计入有效时间。主动提示、洗牌和消除反馈正常计时 |
| 生命周期 | 暂停、帮助、共享设置、后台均遮住整个棋盘并停表；回前台保持暂停，明确继续后才露牌、计时 |
| 输入 | 单一 pointer 持有按下，原格释放才提交；移出、第二触点、弹层、禁用、尺寸变化均取消；没有另一路 Button 点击重复提交 |
| 六槽与终局 | 模式 × 关卡六个独立槽；最近未完成局优先续玩；浏览不造进度；重开只覆盖对应槽；下一关已有活动局则续玩；末关无“下一关” |
| 截止与保存 | 动作前结算有效时间；到点不能再配对；到点前最后一对立即提交胜利，先保存终局再播放反馈，重复保存不重复记成绩 |
| 存储与失败 | 独立 `link-match-v1.json`；严格 Newtonsoft token、字段及跨字段校验；有界读取、临时文件 flush、原子替换、有效备份和损坏隔离。保存失败持续可见，可在内存继续；重试分别交给游戏或公共设置的真实所有者 |

责任边界：`Core/LinkMatch` 只做规则与生成；`Runtime/LinkMatch` 的 Session / Profile / Codec / Store 不依赖 Unity；UGUI 页面处理布局、有效帧时间、输入和反馈。AppController 提供大厅、共享设置及生命周期，保留其他页面的布局策略。没有修改其他游戏的规则或存档协议。

## UI 与素材一致性

保留 [已批准原型](prototypes/link-match/README.md) 的纸色 / 深绿、合集导航、Noto 字体、卡片层次、三档棋盘和底部工具区。生产资源使用同一批 12 张 Fluent 3D 原图，逐字节与原型一致：256 × 256，共 375,824 bytes。固定源版本、原始 SHA-256 及 MIT 完整版权在 [生产 manifest](../Assets/Resources/LinkMatch/manifest.json) 和 [许可证](../Assets/Resources/LinkMatch/Licenses/Fluent-Emoji-MIT.txt)。实际 Unity importer 已验证：Bilinear、Clamp、无 mipmap、未压缩、最大 256。

使用生产 UGUI、WorldSpace Canvas、实际 GraphicRaycaster 和 Camera.Render，在 **388 × 784、360 × 640、720 × 1455** 安全内容区运行验证。原型排列只进入 Editor 测试夹具，生产使用可解生成器。短屏 48 张棋盘的格子为 **51.328 px**，保存错误占位后仍有 **48.25 px**；外圈路径留在棋盘裁剪范围内，工具区不重叠。

- [原型 / Unity 对照页](prototypes/link-match/unity-comparison.html)：同尺寸选择新玩家、48 张棋盘、暂停和玩法，或查看 Unity 页面概览。
- [Unity 实际 UI 概览](screenshots/link-match-unity-overview.png)、[选关对照截图](screenshots/link-match-comparison-entry.png)、[短屏棋盘对照截图](screenshots/link-match-comparison-compact.png)、[暂停对照截图](screenshots/link-match-comparison-pause.png)。
- [Unity 新玩家](screenshots/link-match-entry-new-388x784.png)、[续局入口](screenshots/link-match-entry-resume-388x784.png)、[外圈连线](screenshots/link-match-prototype-outer-route-360x640.png)、[保存错误占位](screenshots/link-match-prototype-warning-48-360x640.png)。
- [暂停](screenshots/link-match-pause-388x784.png)、[帮助](screenshots/link-match-help-388x784.png)、[超时](screenshots/link-match-timeout-388x784.png)、[末关完成](screenshots/link-match-last-level-completed-388x784.png)、[真实共享设置](screenshots/link-match-host-settings-720x1150.png)。

对照逐项核查导航、标题、内容边距、卡片、棋盘 bounds、水果尺寸、工具和遮罩。浏览器与 Unity 字体栅格化存在差异，未声称截图逐像素相同。参考图默认显示提示路径与演示时间，真实截图按对应选中 / 消除 / 计时状态展示，不能用演示数字替代运行数据。

开发阶段补齐了原型最初漏掉的重开入口与确认，并保留原入口、棋盘和素材布局。另补齐 Unity 的弹层关闭按钮：暂停关闭保存进度回选关；帮助关闭回暂停；只有明确继续才恢复对局。原型演示原先通用关闭会直接露盘，已同步修正这一与方案不符的关闭路由。玩法卡增加免费自动重排说明，游戏内主按钮写“继续游戏”；共享设置使用 APP 的真实弹层。以上变更在原型 README 中留有记录。

## 验证证据

工具链：Unity **6000.3.27f1**（实际 Android build target）、.NET **10.0.403**。Unity 检查串行运行，使用本工作树的 Library / Gradle 缓存和独立 ADB server 端口，未链接或清理其他工作树缓存。存档测试只读写独立临时目录，不使用玩家真实存档。

| 验证层 | 结果 | 覆盖与证据 |
| --- | --- | --- |
| Core | 20 组通过，0 失败 | `scripts/test-link-match.sh`；400 个独立搜索对照、300 个种子完整证明、240 个稀疏局面重排及静态空位 |
| 会话 / 档案 / 本地存储 | 32 组通过 | `scripts/test-link-match-persistence.sh`；时钟、截止、配额、六槽、成绩幂等、实际 JSON / 文件 / 备份 / 损坏恢复 / 写失败 |
| 输入 / 布局纯模型 | 238 条断言通过 | `scripts/test-link-match-input.sh`；单指、移出、多点、取消和两尺寸最大棋盘 |
| 既有非 Unity 回归 | **49 个脚本全部通过** | 包含上述 3 个新脚本；结果 `artifacts/validation/link-match-regression/results.json`，逐脚本日志同目录 |
| 生产 UGUI / 实际宿主 | **3,847 条断言通过** | `scripts/test-link-match-ui.sh`；实际点击、所有六槽续局、遮盘停表、真实最后一对 / 超时先落盘、保存失败和重试、共享设置、尺寸变化；报告 `artifacts/validation/link-match-ui/report.txt` |
| 既有大厅 / UI 集成 | **261 条断言通过** | `scripts/test-minigame-ui.sh`；720 × 1320 / 1582 的 13 卡布局和原有 Jump / Sudoku / Sheep / icon 检查；报告 `artifacts/validation/minigame-ui/report.txt` |
| 素材 / 元数据 / 原型 | 通过 | 12 张 PNG 与许可 SHA-256；生产 / 原型逐字节一致；新文件 Unity meta 齐全，全 Assets 无重复 GUID；JS 语法、Git 空白检查 |
| Android 编译与打包 | **Success** | `scripts/build-link-match-validation.sh`；最终日志 `artifacts/logs/build-link-match-validation.log` 含 `Build Finished, Result: Success.` |
| Android 设备 | **未执行安装或真机回归** | 最后执行 `adb devices -l` 为空；不能由 Editor 渲染和构建替代物理多点触摸、后台恢复、重启续局及设备性能验收 |

### 复现后修正的事项

1. 已有 `Tests/Core` 缺 Newtonsoft 引用导致 CS0246；仅补与既有测试相同的 13.0.2 引用，原有 43 个 Core 测试和完整回归随后通过。
2. 首轮 Unity 编译发现局部变量名冲突；修复后，实际 importer 检查发现最简 meta 未应用 Clamp / filter 等参数，改为完整 TextureImporter 配置并验证全部 12 张。
3. 独立规格审查发现公共设置重试没有执行宿主保存，以及最后一对路径被即时结果弹窗遮蔽。前者接回宿主真实 Save；后者保持终局先落盘、输入冻结，等 0.22 秒路径反馈后展示结果。
4. 独立质量审查提出释放可能早于页面 Update 的 resize 窗口。实际 UGUI 用例“选首张 → 按第二张 → 改根尺寸 → 不等帧直接释放”在第 379 条断言明确失败。现在尺寸回调同步取消手势，布局重建延后至 Update；该用例在三种尺寸均通过。红报告保存在 `artifacts/validation/link-match-ui/resize-red-report.txt`。
5. 同尺寸视觉检查补正玩法摘要卡、暂停弹层高度和关闭入口。最终完整 UGUI 重跑与 APK 已包含这些修正；规格、质量复核已处理所有确认缺陷。

## 安装包与后续设备验收

- 文件：`artifacts/builds/XanaduYonder-0.5.0-link-match-validation.apk`
- 独立包：`com.duanyao.xanaduyonder.linkmatchtest`，显示名“Xanadu 连连看测试”，0.5.0 / versionCode 5。
- ARM64，min SDK 26，target SDK 36；这是可调试验证包，不是正式发布包，不覆盖原应用。
- `aapt dump badging` 已核对包信息；`apksigner verify --verbose` 验证通过（APK Signature Scheme v2）。
- 大小：**82,884,967 bytes**。
- SHA-256：`3abbb8e0e04f76a49d9e791a1ea3bef9e46d133ef41c74924f2c35b062cb75fa`。

有 Android 设备后，安装该独立包，检查两模式三关、触点拖出和多点取消、后台 / 锁屏后暂停遮盘、强制关闭后六槽续局、横屏游戏返回后的安全区和最大棋盘、实际音效 / 震动与帧率。当前交付已完成源码、自动化运行、真实 Unity UI 和 Android APK；设备层仍是明确的验证缺口。
