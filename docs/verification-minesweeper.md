# 扫雷开发与验证记录

日期：2026-10-27。实现基于用户确认的[完整玩法与交互设计](superpowers/specs/2026-10-27-minesweeper-design.md)，开发任务见[实施计划](superpowers/plans/2026-10-27-minesweeper-implementation.md)。本文分别记录源码、独立测试、Unity 界面、Android 构建和设备证据，不用某一层代替另一层。

## 工作树与范围

- 工作目录：`/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`。
- 开发分支：`p/duanyao.0516/feature/minesweeper`；创建基线为 `master` 的 `ca50db0f381b96c7a024586792bdc60eb3f0fd3c`。
- 所有源码、测试、Unity Library/Temp 和构建产物都位于该工作树。没有将其他游戏开发分支的未合入内容带入扫雷。
- 原六款游戏顺序保持，扫雷作为第七张大厅卡片。公共修改限于 AppController 路由/共享设置生命周期、GameFeedback 通用触感入口、验证构建入口和大厅卡片断言。
- 仍为源码版本 0.4.0 / versionCode 4；未合并 master、推送、创建 Release 或发布新版本。

## 已实现功能

三档经典棋盘（9×9/10、16×16/40、30×16/99），首格及其邻域安全、延迟布雷、空区展开、旗数匹配快速展开、确定性的触雷优先结算。首次有效翻开计时，胜负一次入账；每档独立续局、胜负和最佳毫秒数。

普通与全屏共用同一对局；单指拖动、双指同时缩放和平移，50%～200% 连续缩放，±25 个百分点与 100% 复位。50% 小格仍可操作；只读总览独立适应整盘并支持点位返回。总览图标为四个向内的小箭头。手势整帧仲裁，第二指取消单击/长按资格，长按在抬手时提交，取消或越界等待全指释放。

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

已读到连接物理设备为 PKM110、Android 16、1080×2373；逻辑密度为覆盖后的 480 dpi。安装只针对独立包，安装前确认此包不存在。等待期间设备锁屏（系统报告 `showing=true`），系统安装解析页位于锁屏后；`adb install` 最终退出 1，仅返回 `failed to install` 而没有更具体错误。已请用户解锁并完成测试包安装确认后重试。不能据此宣称已安装或完成设备运行。

已在忽略目录 `artifacts/validation/minesweeper-device/` 编译并验证独立输入辅助 APK，源码及说明同目录。它以 self-target instrumentation 运行，不重启 Unity；只在扫雷测试包获焦时注入系统 MotionEvent，保留既有辅助功能服务，截图限制为目标窗口。该工具尚未在设备执行，工具编译通过不代表手势已通过验收。

未验证的设备项目包括：Android 实际点击/长按/多指取消与切换、50% 实际命中、32/44 dp 实测、全屏与总览往返、Home/锁屏/重启续局，以及真人手感、声音和触感。后续使用此独立测试包继续验收，不操作正式应用的数据。

## 交叉审查与交付检查

规则/Session/存档、纯输入和页面/宿主交叉审查已执行。发现并修复存档读取权限误判、保存重试提示和小屏固定控件尺寸问题；最终源码审查未发现可确认的 P1/P2。审查不替代设备行为验证。

新增 Assets 文件及目录 `.meta` 齐全，扫描未发现重复 GUID；Core 保持 `noEngineReferences: true`。Library/Temp/artifacts 及独立 Gradle 缓存不使用其他工作树的软链接。`git diff --check` 通过，构建设置无最终差异；当前分支仍为指定扫雷分支，基线后的既有提交仅为本任务设计文档。设备阻塞与未验证面如上，不把代码交付表述成设备验收完成。
