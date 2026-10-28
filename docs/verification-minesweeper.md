# 扫雷开发与验证记录

初版日期：2026-10-27；用户反馈修订与合并检查：2026-10-28。实现基于用户确认的[完整玩法与交互设计](superpowers/specs/2026-10-27-minesweeper-design.md)，开发任务见[实施计划](superpowers/plans/2026-10-27-minesweeper-implementation.md)。下文按时间保存各轮证据，最新完整回归与构建见文末；不以源码、独立测试或编辑器检查替代真机验收。

## 工作树与范围

- 工作目录：`/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`。
- 开发分支：`p/duanyao.0516/feature/minesweeper`；创建基线为 `master` 的 `ca50db0f381b96c7a024586792bdc60eb3f0fd3c`。
- 所有源码、测试、Unity Library/Temp 和构建产物都位于该工作树。没有将其他游戏开发分支的未合入内容带入扫雷。
- 原六款游戏顺序保持，扫雷作为第七张大厅卡片。公共修改限于 AppController 路由/共享设置生命周期、GameFeedback 通用触感入口、验证构建入口和大厅卡片断言。
- 初版仍为源码版本 0.4.0 / versionCode 4；当时尚未合并 master、推送、创建 Release 或发布新版本。后续修订与合并检查见文末。

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

## 2026-10-28 第三轮反馈：底部工具栏整合

全屏不再独占顶部一行。底栏依次为「全屏/退出、翻开、插旗、缩放组、总览」，全屏/退出在同一位置交替显示，沿用总览的图标在上、文字在下及主题绿。9×9 仍禁用全屏与总览。

缩放三个按钮共用一个带圆角裁剪的背景，内部没有间隔，细线区分点击区域；三个按钮仍分别执行减小倍率、恢复 100%、放大倍率，禁用时文字与图标变为次要色。暂停背景每边内缩 8 单位，图标从 34 缩为 28，透明命中区保持原尺寸。普通棋盘取消顶部全屏行后上移，底栏和提示再上移 24 设计单位；共享导航位置不变。

布局单行阈值为 `7 × minimumTarget + 32`；不足时上排为全屏/退出、翻开、插旗、总览，下排为居中的连续缩放组。所有操作仍满足按逻辑密度换算的 44 dp 点击目标，无重叠；不以缩小命中区域强行排一行。

- `scripts/test-minesweeper-input.sh`：76 项通过，0 失败；新增分组、七控件排列和边界回归先观察旧布局内部间距失败，原输入/手势用例保留。日志在 `artifacts/logs/minesweeper-toolbar-input-{red,green}.out`。
- `scripts/test-minesweeper-ui.sh`：4166 条断言通过。旧适配层先在第 347 条断言因全屏不在底栏失败，报告保存为 `artifacts/validation/minesweeper-ui/toolbar-red-report.txt`。新回归覆盖真实分段点击、同位置全屏/退出、视觉缩小与命中区保持、普通棋盘/底栏上移、两种屏高和窄屏重排，并继续覆盖先前的初级禁用、旧档恢复、长按、导航、暂停和终局。
- 已查看 `refined-toolbar-normal-720x1582.png`、`refined-toolbar-focus-720x1320.png`、`focus-overview-720x1320.png` 实际渲染图，确认分组、文字、图标和背景表现。截图位于 `artifacts/validation/minesweeper-ui/`。

改动限定在扫雷视图/布局及对应测试、文档，未改规则、存档 schema、共享导航或其他游戏。编辑器几何和点击检查不代替物理设备的实际触控与视觉体验验收。

独立源码审查未发现阻塞问题。`scripts/build-minesweeper-validation.sh` 退出 0，`aapt` 确认独立测试包名、0.4.0/code 4 和 ARM64，`apksigner verify --verbose` 退出 0（v2 签名通过）。ProjectSettings 无最终差异。最新 APK 仍为 `artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`，77,980,241 字节，SHA-256 为 `504bc6939b4fee63201ff7d9f3639db6f761373ea107fea762594ea9305651fe`。

已通过指定 PKM110 的 `adb install -r` 覆盖安装，返回 `Success`，未卸载或清除数据；读取设备实际 APK 哈希与本次构建一致。上一版入口/配色包备份为 `XanaduYonder-0.4.0-minesweeper-validation-20261028-view-colors.apk`。本轮设备证据限于安装及产物一致性，不宣称已完成新布局的真人触控验收。

## 2026-10-28 第四轮反馈：底部说明删除与按钮留白

删除扫雷底部「选择难度 · 每档独立续局与纪录」及初始说明、保存成功常驻文案。进入、返回、开始和续局均清除旧普通提示；真实加载/保存异常保留，重试成功后清空。`Save()` 只有在扫雷页面可见时才发布公共状态，避免后台保存清空其他页面的提示；公共存档异常仍由宿主优先显示。

底栏每行增高 16 设计单位，图标/文字内容组垂直居中，常规 104 高按钮上下各留 16。横向宽度和换行阈值不变。底栏下边距从 128 改为 96，操作提示顶部距底部从 116 改为 84、高度从 40 改为 36；单行按钮上边下移 16、下边下移 32，提示下移 32。错误信息仍在原底部区域，与操作提示相隔 6 单位，不覆盖文字。棋盘顶部保持本轮之前的 294 基准。

- `scripts/test-minesweeper-input.sh`：旧高度先出现 69 通过/7 失败，修改后 76 通过、0 失败，原手势及布局边界继续覆盖。
- `scripts/test-minesweeper-ui.sh`：先在第 351 条断言观察按钮留白不均衡，修改后退出 0，**4338 条断言通过**。新增状态检查覆盖旧提示清除、离开后的 Save 不清宿主提示、宿主实际保存错误优先、错误解除后状态清空；原真实存储失败重试、全屏、小棋盘禁用和点击回归保留。
- 失败报告为 `artifacts/validation/minesweeper-ui/footer-padding-red-report.txt`，最终报告仍为该目录 `report.txt`。已查看 `final-toolbar-normal-720x1582.png`、`final-toolbar-focus-720x1320.png` 和 `save-error-normal-720x1320.png`，确认上下留白、下移和错误信息不重叠。

独立源码审查未发现阻塞问题。修改限定扫雷表现/布局、相关测试和文档，未改共享 AppController 或其他游戏；编辑器验证不替代 Android 真人视觉和触控验收。

本轮构建脚本退出 0，`aapt` 确认仍为独立测试包 `com.duanyao.xanaduyonder.minesweepertest`、0.4.0/code 4、ARM64；`apksigner verify --verbose` 退出 0，v2 签名通过；ProjectSettings 无最终差异。

- 最新 APK：`artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`，77,979,311 字节。
- SHA-256：`a6f954a79c9ad747e6ff1d1e52ed253220d86e1ac1f6e24b1a9da05934bd21d3`。
- 已使用指定物理设备的 `adb install -r` 覆盖安装到 PKM110，退出 0/`Success`；设备已安装 APK 哈希与此构建一致，未卸载或清空数据。设备证据限于安装及产物一致性。
- 前一版紧凑底栏 APK 备份为 `XanaduYonder-0.4.0-minesweeper-validation-20261028-compact-toolbar.apk`。

## 2026-10-28 最终完整回归与 master 合并检查

用户确认上一轮界面调整可以，并要求完成扫雷功能、UI 全面检查后合入 master。本轮以最新已提交的 `origin/master`（`b793fe8b43f3bafe6380a32464dfe7b45993437e`）为目标；先将其同步到扫雷分支，再完成下列回归。该目标提交包含已在 master 中的校园主题调整，不读取或引入其他工作树的未提交改动。

### 审查与修复

三路独立源码审查分别检查规则/会话/实际存档链路、整帧输入/视口/控件布局、页面/公共宿主/UI。规则、输入、数据恢复链路未发现新的阻塞问题。页面审查发现一个 P2：公共存档失败时，进入全屏会隐藏普通状态栏，而紧凑状态只读取扫雷自身错误，导致公共保存错误不可见。

修复由宿主将当前公共错误同步至扫雷紧凑状态区；普通/全屏一致按公共错误优先、扫雷错误其次展示。两种存档均恢复后清空文字，不显示常驻成功说明；不可见的扫雷页仍不覆盖其他页面状态。修改收敛于 `AppController.SetStatus/Save` 与 `MinesweeperPage` 的错误呈现，不变更存档格式或游戏规则。生产源码提交为 `fad41d084c170aa6b5a5d6503088ab767c839a76`。

新增回归通过真实临时目录阻断公共存储，先在第 1722 条断言观察全屏错误不可见，修复后通过。覆盖普通/全屏往返、全屏期间新失败、扫雷保存不清公共失败、两种错误同时存在、各自恢复及恢复后清空。修订经过独立复核，未发现新的 P1/P2。

### 本轮实际执行结果

所有命令均在扫雷独立工作树执行并退出 0；Unity 检查与构建依次运行，使用本树隔离的 Gradle/ADB 环境。

| 检查 | 通过数 | 范围 |
| --- | --- | --- |
| `test-minesweeper.sh` | 11 | 首击安全、展开、旗与数字展开、胜负/快照 |
| `test-minesweeper-session.sh` | 13 | 计时、暂停/恢复、独立槽位、终局幂等 |
| `test-minesweeper-input.sh` | 76 | 长按即时提交、多指取消、50%～200%、平移/命中、工具栏重排 |
| `test-minesweeper-persistence.sh` | 20 | 严格真实 JSON、备份/损坏、实际读写失败与恢复 |
| 已有基础与六款游戏独立回归 | 271 | Core 43、Input 10、Persistence 13、SudokuSession 8、Jump 25、Sheep 40、Idiom 45、Sudoku 27、Watermelon 36、WatermelonPersistence 24 |
| `test-minesweeper-ui.sh` | 4568 条断言 | 两种屏高、真实 UGUI/输入、导航、状态/保存恢复、小棋盘禁用、普通/全屏/总览、按钮几何 |
| `test-minigame-ui.sh` | 209 条断言 | 七张大厅卡片、数独、跳一跳、旸了个旸及公共图标/弹窗 |
| `test-idiom-ui.sh` | 6238 条断言 | 三种尺寸、八种页面状态、统一导航、键盘布局、生词本往返、实际射线点击 |

合计 391 组独立检查和 11015 条 Unity UI 断言。已人工查看本轮渲染的选择、规则、教程、初级禁用态、50% 棋盘、全屏暂停、胜负与全屏公共保存错误截图，未见裁切、重叠或配色偏离。全部 412 个 Assets GUID 无重复，新加入资源均有 `.meta`；`git diff --check` 通过。

本轮日志集中于 `artifacts/validation/minesweeper-final-review/`；最终界面报告/截图仍位于 `artifacts/validation/minesweeper-ui/`、`artifacts/validation/minigame-ui/`，成语报告为 `artifacts/logs/idiom-ui-checks.txt`。

### 最终 Android 产物与验证边界

构建脚本退出 0，`aapt` 核对独立包名、0.4.0/code 4、minSdk 26、targetSdk 36、ARM64，`apksigner verify` 退出 0。ProjectSettings 构建后无差异。

- APK：`artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk`，77,980,178 字节。
- SHA-256：`fefd34d9adc607366ad84dafe7f22ec65da9aefb47e5192f1531e5c31c2de228`。
- 上一版已安装的 APK 另存为 `XanaduYonder-0.4.0-minesweeper-validation-20261028-footer-padding.apk`。

用户对先前设备版的验收结论保持为本轮输入。本轮补充的是源码审查、真实存储回归、Unity 渲染/操作和 Android 构建；没有再次执行整套物理设备触摸、实际 dp、原生键盘或声音/振动体验验收。最终构建尚未替换手机已安装的上一版测试包。

### 合并范围

新增内容为扫雷模块、对应测试、独立构建脚本与玩法/验证文档；公共接入限于宿主路由、设置/生命周期/错误状态、触感入口和大厅卡片。成语仅保留用户此前明确要求的七处页头坐标统一及相应回归。与 `origin/master` 比较，`Assets/Resources` 和 `Runtime/Watermelon` 无增量改动；不夹带其他工作树中的数独/祖玛未提交文件。

交付采用本扫雷工作树内的本地 master 快进合并。主目录原有分支及未提交修改不切换、不暂存、不清理；不推送远程，不创建 Release，不修改正式版本号。精确合并提交及树一致性由交付时 Git 检查确认。
