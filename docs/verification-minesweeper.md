# 扫雷开发与验证记录

初版日期：2026-10-27；用户反馈修订：2026-10-28。实现基于用户确认的[完整玩法与交互设计](superpowers/specs/2026-10-27-minesweeper-design.md)，开发任务见[实施计划](superpowers/plans/2026-10-27-minesweeper-implementation.md)。下文保存初版证据，最新反馈修订与构建见文末；不以源码、独立测试或编辑器检查替代真机验收。

## 工作树与范围

- 工作目录：`/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`。
- 开发分支：`p/duanyao.0516/feature/minesweeper`；创建基线为 `master` 的 `ca50db0f381b96c7a024586792bdc60eb3f0fd3c`。
- 所有源码、测试、Unity Library/Temp 和构建产物都位于该工作树。没有将其他游戏开发分支的未合入内容带入扫雷。
- 原六款游戏顺序保持，扫雷作为第七张大厅卡片。公共修改限于 AppController 路由/共享设置生命周期、GameFeedback 通用触感入口、验证构建入口和大厅卡片断言。
- 仍为源码版本 0.4.0 / versionCode 4；未合并 master、推送、创建 Release 或发布新版本。

## 已实现功能

三档经典棋盘（9×9/10、16×16/40、30×16/99），首格及其邻域安全、延迟布雷、空区展开、旗数匹配快速展开、确定性的触雷优先结算。首次有效翻开计时，胜负一次入账；每档独立续局、胜负和最佳毫秒数。

普通与全屏共用同一对局；单指拖动、双指同时缩放和平移，50%～200% 连续缩放，±25 个百分点与 100% 复位。50% 小格仍可操作；只读总览独立适应整盘并支持点位返回。总览图标为四个向内的小箭头。手势整帧仲裁，第二指取消尚未提交的单击/长按资格，长按现按 2026-10-28 反馈改为达到 400 ms 即提交，取消或越界等待全指释放。

标题、导航、字体、颜色和图标沿用 UiFactory / UiIconGraphic；棋盘图标及短音效为本项目生成，不新增外部游戏素材或 SDK。100% 格子按 Android 逻辑密度适配至少 32 dp；主要工具目标至少 44 dp，不够一行时重排两行。设备实际尺寸另行验证。

进行局的暂停、离页、弹窗、后台恢复均遮盘停时并要求明确继续；准备局和终局按各自状态恢复。设置弹窗由宿主关闭，页面负责遮盘。独立 `minesweeper-v1.json` 使用严格 schema 校验、临时写入、原子替换和有效备份；保存错误保留内存对局，重试成功清除错误。

## 独立生产源码测试

以下脚本均在本工作树运行，退出码为 0。测试直接编译生产 C#；存档测试使用真实 Newtonsoft.Json 和临时目录。

| 命令 | 结果 | 主要边界 |
| --- | --- | --- |
| `scripts/test-minesweeper.sh` | 11 组通过 | 300 组种子/位置安全布雷、旗阻止展开、错误等量旗触雷优先、快照与终局 |
| `scripts/test-minesweeper-session.sh` | 13 组通过 | 假时钟前后台、重复回调、总览计时、三槽隔离、终局幂等、毫秒与溢出 |
| `scripts/test-minesweeper-persistence.sh` | 20 组通过 | 全字段严格类型、重复键/深度/大小、损坏恢复、权限失败、重试和终局原子记录 |
| `scripts/test-minesweeper-input.sh` | 63 组通过 | 缩放锚点/实际命中、多指顺序/释放/取消、400 ms 与 12 单位边界、总览定位、432 组控件尺寸 |

合计 107 组通过。开发期间先观察了缺实现/行为不满足的失败，再补实现；后续发现的重复 Resume 丢失计时片段、存档权限错误被误判为缺失均加入回归并修正。

现有能力回归：`test-core.sh` 43、`test-input.sh` 10、`test-persistence.sh` 13、`test-sudoku-session.sh` 8、`test-watermelon-persistence.sh` 24、`test-jump.sh` 25、`test-sheep.sh` 40、`test-idiom.sh` 45、`test-sudoku.sh` 27、`test-watermelon.sh` 36，全部退出 0。这是受影响基础能力和各游戏核心回归，不声称所有历史测试都已重跑。

## Unity 实际界面

Unity 6000.3.27f1，Android 编译目标，真实 UGUI + Camera.Render。扫雷夹具注入内存档案或临时目录，不读写普通玩家存档；实际棋盘输入经生产 Pointer 入口，鼠标分发使用真实 EventSystem 射线。

- `scripts/test-minesweeper-ui.sh`：3144 条断言通过，720×1320 与 720×1582；包含选择/教程/规则、正常/全屏/总览/暂停、50% 命中、终局、真实宿主共享设置、保存错误恢复和按钮几何。
- `scripts/test-minigame-ui.sh`：209 条断言通过；覆盖原大厅顺序加第七卡片、数独计数/弹窗/输入/计时、跳一跳回调、图标和旸了个旸渲染。
- 99 与 117 设计单位的触控目标模拟直接进入生产 Layout 重排，检验两种根宽下不重叠、不覆盖视口。它们是几何测试，不能当成物理设备 dp 测量。
- 保存重试曾真实失败于旧错误文案残留，修复后整套通过；原始失败保存在 `artifacts/validation/minesweeper-ui/save-retry-red-report.txt`。最初未实现页面的失败为同目录 `initial-red-report.txt`。
- 已人工查看实际渲染的普通/50%/全屏总览、选择、教程、胜负、暂停和保存重试截图；检查中文字形、边界、内向箭头与遮盘。

报告为 `artifacts/validation/minesweeper-ui/report.txt` 和 `artifacts/validation/minigame-ui/report.txt`，截图位于对应目录。Unity 启动日志出现编辑器 QuickSearch 索引异常与许可刷新提示，但两套运行均完成、退出 0；不把这些编辑器日志描述为游戏运行故障或无告警日志。

## Android 构建与设备

`scripts/build-minesweeper-validation.sh` 最终退出 0，已生成 ARM64 开发 APK：

- 文件：`artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`，62,962,304 字节。
- SHA-256：`1fb5ff2ebbe025461a1765adf0b1074afd641d03c30b58c23208c7768b362ce3`。
- `aapt dump badging` 确认独立包 `com.duanyao.xanaduyonder.minesweepertest`，0.4.0 / code 4，minSdk 26、targetSdk 36、arm64-v8a。
- 构建前后 `ProjectSettings/ProjectSettings.asset` 无最终差异，临时产品名/包名恢复成功。

首次构建完成了 C#/IL2CPP，但 Gradle 明确返回 `Gradle build daemon has been stopped: stop command received`，未生成 APK。没有查询其他工作树定位发起者。新增 `scripts/minesweeper-unity-environment.sh`，让扫雷 Unity 检查及构建使用本树 `Library/MinesweeperGradle` 和本次进程独立的 ADB socket/port，避免退出时停止共享工具服务；重跑成功。首次日志保存在 `artifacts/logs/build-minesweeper-validation-gradle-stopped.log`，最终日志为 `artifacts/logs/build-minesweeper-validation.log`。

2026-10-27 工具读取连接物理设备为 PKM110、Android 16、1080×2373；逻辑密度为覆盖后的 480 dpi。当时独立包安装等待期间手机锁屏，`adb install` 最终退出 1，未取得成功证据。2026-10-28 用户确认已自行安装并完成测试，反馈长按反馈时机及返回导航一致性；随后只读核对设备已安装包 SHA-256 与初版 `1fb5ff2e…8b362ce3` 一致。用户反馈属于实际设备使用证据，但未逐项列出的验收矩阵不能据此全部标记通过。

已在忽略目录 `artifacts/validation/minesweeper-device/` 编译并验证独立输入辅助 APK，源码及说明同目录。它以 self-target instrumentation 运行，不重启 Unity；只在扫雷测试包获焦时注入系统 MotionEvent，保留既有辅助功能服务，截图限制为目标窗口。该工具尚未在设备执行，工具编译通过不代表手势已通过验收。

未验证的设备项目包括：Android 实际点击/长按/多指取消与切换、50% 实际命中、32/44 dp 实测、全屏与总览往返、Home/锁屏/重启续局，以及真人手感、声音和触感。后续使用此独立测试包继续验收，不操作正式应用的数据。

## 交叉审查与交付检查

规则/Session/存档、纯输入和页面/宿主交叉审查已执行。发现并修复存档读取权限误判、保存重试提示和小屏固定控件尺寸问题；最终源码审查未发现可确认的 P1/P2。审查不替代设备行为验证。

新增 Assets 文件及目录 `.meta` 齐全，扫描未发现重复 GUID；Core 保持 `noEngineReferences: true`。Library/Temp/artifacts 及独立 Gradle 缓存不使用其他工作树的软链接。`git diff --check` 通过，构建设置无最终差异；当前分支仍为指定扫雷分支，初版实现提交为 `503815e`，后续设备状态记录为 `0481a27`。不把代码交付表述成设备验收完成。

## 2026-10-28 用户反馈修订

### 长按即时标记

用户在实际安装测试后要求到达长按时间就震动并插旗。现改为首个合法单指帧达到 400 ms 时立即 ToggleFlag，消费本轮格子操作资格；399 ms 尚不提交，继续按住和 UP 不重复。若第一次观察到阈值已是正常 UP，仍只处理一次。阈值同帧的第二指、取消或越界优先阻止尚未提交的长按；已完成的旗不回滚，之后可继续平移或双指变换。右键保持松手直接标记。

标记反馈在同步存盘之前触发，避免 `Flush(true)` 的等待延迟触感；声音与触感继续遵从现有开关。原来的翻开/终局反馈顺序保留，避免结果弹窗把胜负音停止。游戏提示、教程、规则、README 与开发文档同步更新。

输入测试先出现 67 通过 / 8 失败，修复后 `test-minesweeper-input.sh` 75/75。页面排序回归在真实存档目录中观察反馈参数求值时的旧磁盘旗值：修复前在第 174 条断言失败，反馈提前后整套 `test-minesweeper-ui.sh` 3618 条断言通过；同时证明 400 ms 帧已经写盘、继续持有和 UP 不再触发反馈。规则 11、Session 13、存档 20 组复跑均通过，总计 119 组独立扫雷测试。

### 普通导航统一与影响范围

核对当前工作树七款游戏：2048、跳一跳、旸了个旸、西瓜、数独和扫雷的普通返回图标都是居中 720 列中的 `(56,34,58,58)`。扫雷与真实数独页面还经过 720/844.8 两种根宽、1320/1582 两种高度、选择→游戏→全屏返回→选择的屏幕四边比较，全部相同，无需对扫雷添加偏移补偿。

唯一坐标例外为成语接龙，本次统一其顶部导航：

| 控件 | 修订前 | 修订后 |
| --- | --- | --- |
| 大厅图标 | `(32,26)` | `(56,34)` |
| 生词本图标 | `(552,26)` | `(536,34)` |
| 设置图标 | `(630,26)` | `(606,34)` |
| 生词本文字返回 | `(32,26)`，130×58 | `(56,34)`，保留 130×58 |
| 同排标题 | y22，35 字号 | y30，字号不变 |

只有成语页头的七处坐标参数变化，未改全局 SafeDesignFrame、WidthOnly、键盘适配、聊天区、输入区、对局与存档。标题仍居中，下一行模式切换仍位于 y108。不同游戏的主体标题字号和棋盘版式保留；统一导航位置不要求把所有游戏改成同一种主体布局。

成语真实 UGUI 回归已先观察失败：`home actual screen left (actual 32, expected 56)`，第 94 条断言中止。修订后 `test-idiom-ui.sh` 退出 0，6238 条断言通过：720×1280、720×1582、900×1582 三种尺寸，每种覆盖八种对战状态；导航及标题实际屏幕边界不重叠，模式行保持 y108；注入 360 设计单位键盘占用后输入区上移、关闭后恢复；真实 raycast 点击生词本→返回→大厅且对局/历史保留。

界限：成语夹具在 WorldSpace Canvas 中运行，未直接运行宿主 WidthOnly 缩放或原生输入法的 DOWN 收键盘→UP 路径，也未直接覆盖输入期间冻结分支。这些生产逻辑未改，已作源码复核，不能把几何与点击回归表述成全部 Android 键盘场景实测。独立代码审查未发现修订后阻塞问题。

本轮红灯报告保存在 `artifacts/validation/minesweeper-refinement/`；扫雷最新截图与报告仍在 `artifacts/validation/minesweeper-ui/`。编辑器输入与反馈顺序检查不能代替 Android 实际振动体感，本轮也未宣称完成新的真人手势或原生输入法验收。

### 修订 APK

`scripts/build-minesweeper-validation.sh` 本轮退出 0，`aapt` 验证仍为 `com.duanyao.xanaduyonder.minesweepertest`、0.4.0 / code 4、arm64-v8a，`apksigner verify --verbose` 退出 0（v2 签名通过）。构建结束后 ProjectSettings 无差异。

- 最新路径：`artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`。
- 大小：77,977,824 字节。
- SHA-256：`2f91c03a3d91b51b267fdaca509322e84c10cad3d71d1d1153a70608d501b118`。
- 首版 APK 已另存同目录 `XanaduYonder-0.4.0-minesweeper-validation-20261027.apk`；本文前面的首版哈希对应此备份。两版 ZIP 条目清单无增删，本轮没有导入额外游戏资源。

修订包保留原数据协议与包标识，可以覆盖此前的独立测试包。本轮没有自动替换用户手机上的安装，也没有修改正式包。新包的真人触感与原生键盘体验待设备复测。

## 2026-10-28 第二轮设备反馈：小棋盘入口与配色

此前长按/导航修订包已按用户要求覆盖安装到 PKM110，设备 APK 的 SHA-256 核对为 `2f91c03a…501b118`。本节对应用户随后提出的新两项反馈。

- 9×9 的总览、全屏按钮保留位置，按钮禁用，图标与文字采用公共次要色；页面操作回调同样拒绝进入。正常 50%～200% 缩放仍可用，不因放大解锁这两个入口。
- 16×16、30×16 继续支持总览与全屏，暂停遮盘与返回层级沿用原逻辑。旧 9×9 全屏存档恢复为普通布局，保留局 ID、棋盘、计时、模式、倍率与期望中心，并在保存时写回普通布局。
- 可用总览图标/文字、横纵范围条统一使用 `UiFactory.Accent`（`#276653`）；棋盘视口背景改为 `#F0F3EC`。未开格、已开格和其他游戏的公共色系不变。游戏规则及规范文档同步。

修改仅位于扫雷 Page/Layout/Dialogs/BoardView、对应 UI 检查及文档，无 Core、Session、存档 schema、共享主题或其他游戏改动。

验证：新增用例在修改前于第 243 条断言失败，原因是初级扩展视图入口仍可用；报告保存为 `artifacts/validation/minesweeper-ui/small-board-actions-red-report.txt`。修订后 `scripts/test-minesweeper-ui.sh` 退出 0，共 **3910 条断言通过**，覆盖两种屏高、真实 raycast 点击与直接回调、同一页面 0→1→2→0 难度切换、旧准备/进行局全屏存档经真实 Codec 往返与恢复，以及原有长按、生命周期、终局、导航和控件布局回归。

已查看实际渲染的初级禁用态、全屏局部与全屏总览截图，确认控件配色、浅背景和禁用态。独立源码交叉审查未发现阻塞问题。上述界面验证运行于 Unity 编辑器，不能替代 Android 真人触摸及视觉体验验收。

本轮 `scripts/build-minesweeper-validation.sh` 退出 0；`aapt` 确认包名仍为 `com.duanyao.xanaduyonder.minesweepertest`、版本 0.4.0/code 4、ARM64；`apksigner verify --verbose` 退出 0，v2 签名通过。ProjectSettings 无最终差异。

- APK：`artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`，77,980,894 字节。
- SHA-256：`e002a3f80862ae980329c63f7cc968309ba9f6b97910262e6e48b413bcc4bfe3`。
- 已通过指定物理设备的 `adb install -r` 覆盖安装到 PKM110，退出 0 并返回 `Success`，未卸载或清空数据。随后从设备读取已安装 APK 的 SHA-256，与上述构建一致。
- 上一版长按/导航修订包备份为 `artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation-20261028-hold-navigation.apk`。

本轮设备验证限于安装成功和产物一致性；实际触感及用户对新配色的验收仍需真人体验。
