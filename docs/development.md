# 本地开发与 Android 构建

新小游戏的设计和接入先参考[推箱子开发复盘与小游戏 UI 参考规范](sokoban-development-guide.md)。其中将共享风格、实际缺陷、内容/存档契约和验证层次分开说明；推箱子的紧凑选关、取消缩放等取舍不应直接套用到所有玩法。

## 固定环境

- Unity Editor **6000.3.27f1**，changeset `672d562a42a7`，Mac 使用 Apple Silicon 版本。
- Unity Hub 安装同一编辑器的 **Android Build Support**、**Android SDK & NDK Tools**、**OpenJDK**；保持 Unity External Tools 默认使用随该版本安装的依赖。
- VS Code 安装 Microsoft 的 `visualstudiotoolsforunity.vstuc` 扩展，它同时安装 C# 与 C# Dev Kit。Unity 项目使用 `com.unity.ide.visualstudio`，不使用已废弃的 Visual Studio Code Editor 包。
- .NET SDK **10.0.403** 用于独立测试，`global.json` 允许同 feature band 的后续补丁。它不替代 Unity 的脚本编译器与 Android 构建工具。
- Android 8.0/API 26 或以上的 **ARM64 真机**，开启开发者选项与 USB 调试，并授权当前电脑。首版固定 target SDK 36，避免跟随机器中额外安装的 SDK 自动变化。

Unity Hub 的账号登录、编辑器许可和首次条款需由使用者完成。许可文件、Android 调试签名和个人 IDE 配置不进入 Git。

本机通过官方 Unity CLI 安装的 Android 模块位于 `/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer`，与 `Unity.app` 同级；这是已通过真实构建的安装布局。环境检查同时兼容旧的 `Unity.app/Contents/PlaybackEngines/AndroidPlayer` 布局，不需要手动移动模块或创建软链接。

## 打开项目

在 Unity Hub 中添加当前仓库根目录；使用上面的固定版本打开 `Assets/Scenes/Bootstrap.unity`，点击 Play。首次导入会生成 `Library/` 和包缓存。在 Unity 的 External Tools 中将 External Script Editor 设为 VS Code；双击 C# 文件即可跳转。

也可以使用菜单 `XanaduYonder / Configure VS Code` 设置外部编辑器并生成解决方案。默认路径为 `/Applications/Visual Studio Code.app`，其他位置用 `UNITY_SCRIPT_EDITOR` 指定。

2048 在手机上滑动，编辑器支持鼠标滑动和方向键；跳一跳按住蓄力、松手起跳，编辑器也支持空格键；旸了个旸点击未被盖住的牌，编辑器用鼠标点击；成语接龙输入四字词后点发送，点击对话查看释义；数独点击格子和数字，支持铅笔候选与快速输入；04 合成大西瓜在棋盘上拖动瞄准、松手投球，编辑器使用鼠标拖动与释放。07 扫雷支持点击翻开、长按 0.4 秒立即标记、单指平移与双指缩放和平移，编辑器右键松手直接标记。大厅按固定分类纵向滚动，支持双列卡片、分类目录、单列大卡；直接点击整张卡片进入游戏。规则、公共设置和各自存档逻辑均与手机共用。

大厅 UI 的批准稿、设计尺寸与图标接入见 [大厅 v2 设计归档](design/lobby-v2/README.md)。`AppController` 负责入口和真实纪录，`LobbyView` 只负责展示。展示偏好独立使用 `xanadu.lobby.display.v1`，不改各游戏存档；没有自定义排序。两个在研游戏的图标已经预留为 `Lobby/Icons/kingdom-rush`（王国守卫战／皇家守卫军）和 `Lobby/Icons/age-of-war`（战争进化史），它们尚不是大厅入口。

## 命令

在仓库根目录执行：

```bash
scripts/check-environment.sh
scripts/test-core.sh
scripts/test-input.sh
scripts/test-persistence.sh
scripts/test-jump.sh
scripts/test-jump-persistence.sh
scripts/test-sheep.sh
scripts/test-sheep-persistence.sh
scripts/test-sheep-layouts.sh
scripts/test-idiom.sh
scripts/test-idiom-persistence.sh
scripts/test-idiom-resources.sh
scripts/test-idiom-presentation.sh
scripts/test-idiom-ui.sh
scripts/test-sudoku.sh
scripts/test-sudoku-content.sh --bank
scripts/test-sudoku-persistence.sh
scripts/test-sudoku-session.sh
scripts/test-sudoku-profile.sh
scripts/test-sudoku-selection.sh
scripts/test-minigame-ui.sh
scripts/test-lobby-design.sh
scripts/test-startup-handoff.sh
scripts/test-watermelon.sh
scripts/test-watermelon-persistence.sh
scripts/test-watermelon-pointer.sh
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-input.sh
scripts/test-minesweeper-persistence.sh
scripts/test-minesweeper-ui.sh
scripts/test-happy-ball.sh
scripts/test-happy-ball-input.sh
scripts/test-happy-ball-persistence.sh
scripts/test-happy-ball-ui.sh
scripts/test-link-match.sh
scripts/test-link-match-input.sh
scripts/test-link-match-persistence.sh
scripts/test-link-match-ui.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

`scripts/test-minigame-ui.sh` 在 Unity 中渲染两种高度的页面，检查原有数独填写/剩余计数、工具点击、玩法弹窗的输入隔离与计时恢复，并检查包含战争进化史系列入口的十八张大厅卡片顺序，输出 `artifacts/validation/minigame-ui/` 截图和报告；这些编辑器检查不代替 Android 真机回归。

`scripts/test-sheep.sh` 直接编译生产规则代码，检查叠层遮挡、7 槽三消、工具、快照与多种子解法重放；`scripts/test-sheep-persistence.sh` 使用真实 Newtonsoft 解析器和临时目录检查新游戏存档。独立测试不能代替 Unity 编译与设备输入/表现验证。

`DEVICE_SERIAL` 从 `adb devices -l` 取得。安装脚本显式拒绝模拟器；不传序列号时仅在恰好存在一台已授权物理设备且没有其他未就绪设备时自动选择。可以传第二个参数指定 APK。

构建脚本默认使用 `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity`。非默认安装位置用 `UNITY_EDITOR=/absolute/path/to/Unity` 覆盖；ADB 可用 `ADB` 覆盖，测试脚本可用 `DOTNET_ROOT` 指定 SDK 目录。

构建输出为 `artifacts/builds/XanaduYonder-0.9.0.apk`，日志为 `artifacts/logs/build-android.log`。当前源码版本为 0.9.0、Android versionCode 为 9；本次发布范围见[v0.9.0 发布记录](releases/v0.9.0.md)。构建本地 APK 不会自动发布新版。开发包使用 Development Build 便于本地日志与存档检查，关闭脚本调试和 profiler 自动连接；使用本机 Android 调试签名，不是商店发布包。

如需单独创建/检查启动场景与构建设置：

```bash
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
mkdir -p artifacts/logs
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.PrepareProject \
  -logFile "$PWD/artifacts/logs/prepare.log"
```

使用同样命令将 `-executeMethod` 替换成 `XanaduYonder.EditorTools.PersistenceChecks.Run`，可以在 Unity 运行真实存档验证。批处理前关闭此项目的交互编辑器，避免同一工程被两个进程占用。

## 大厅、启动交接与成语整合验证

v0.8.0 保留 17 个游戏入口。大厅支持双列卡片、分类目录、单列大卡；固定分类和组内顺序，整卡进入游戏，选择浮层与游戏输入隔离。启动层在大厅布局和输入就绪后同步解除遮挡及射线拦截，避免大厅已显示却不能滚动或点击。

整合成语改动后已重跑：`LobbyDesignChecks` 2939、`MiniGameUiChecks` 1211、`StartupHandoffChecks` 223、`WatermelonPointerChecks` 20、`IdiomUiChecks` 8413、`IdiomPresentation` 198 个断言全部通过；成语资源检查核对全部 45,776 条详情引用及打包哈希。入口依次为 `test-lobby-design.sh`、`test-minigame-ui.sh`、`test-startup-handoff.sh`、`test-watermelon-pointer.sh`、`test-idiom-ui.sh`、`test-idiom-presentation.sh` 和 `test-idiom-resources.sh`，均位于 `scripts/`。Unity 检查与构建必须在同一工作树串行运行。

详细范围见[大厅整合验证](design/lobby-v2/implementation-validation.md)、[大西瓜边界投放](superpowers/specs/2026-10-30-watermelon-edge-release-design.md)、[成语资料保留](verification-idiom-source-preservation.md)和[成语键盘与用户验收](verification-idiom-keyboard.md)。这些记录区分源码/资源检查、Unity 输入与渲染、独立测试包设备操作和用户反馈；不代表 v0.8.0 正式包已完成所有游戏的真机回归。正式包版本、签名、文件长度与 SHA-256 统一记录在[v0.8.0 发布记录](releases/v0.8.0.md)。

成语设置新增“发送后收起键盘”（默认开）和“是否显示拼音”（默认关），只对缺失字段补默认。词库版本变化仍由既有存档保护处理：`dictionaryVersion` 不匹配时保留原文件，等待用户确认重新开始，不自动覆盖，也没有新增跨词库迁移。

## 黄金矿工独立验证

在黄金矿工的隔离工作树运行以下入口；Unity UI 检查和构建必须串行，避免同一工程被两个编辑器占用。

```bash
scripts/test-gold-miner.sh
scripts/test-gold-miner-input.sh
scripts/test-gold-miner-persistence.sh
scripts/test-gold-miner-levels.sh
scripts/test-gold-miner-catalog.sh
scripts/test-gold-miner-campaign.sh
scripts/test-gold-miner-assets.sh
scripts/test-gold-miner-ui.sh
scripts/build-gold-miner-validation.sh
```

前六项直接编译同一份生产 C#；目录检查遍历 240 张地图和 2000 组种子，Levels 检查用生产模拟重放全部成功见证。UI 脚本启动真正的 Unity Play Mode，使用隔离临时目录与实际 UGUI 命中，报告/截图在 `artifacts/gold-miner-development/ui/`。它不会读写正式玩家存档。资源导入入口为 `XanaduYonder.EditorTools.GoldMinerAssetImporter.Prepare`。

APK 为 `artifacts/builds/XanaduYonder-0.9.0-gold-miner-validation.apk`，日志在 `artifacts/logs/build-gold-miner-validation.log`。显式选择 `adb devices -l` 的物理设备，安装及启动 `com.duanyao.xanaduyonder.goldminertest/com.unity3d.player.UnityPlayerActivity`；不要使用普通安装脚本覆盖正式包。测试包与正式版、其他工作树的测试包各有独立数据目录。

`scripts/prepare-gold-miner-touch.sh` 只在本机编译 Android 原生多指验收驱动，输出 `artifacts/gold-miner-development/device-tools/gold-miner-touch.jar`；不自动连接或操作设备。手工选择设备后可用 `adb -s SERIAL shell CLASSPATH=/data/local/tmp/gold-miner-touch.jar app_process / NativeTouch dual X1 Y1 X2 Y2 HOLD_MS` 验证真正的 Android MotionEvent 双指链路。实际设备、构建和验证范围见[黄金矿工验证](verification-gold-miner.md)。

## 合成大西瓜独立验证

04 合成大西瓜使用 Unity 原生 `PhysicsScene2D` 独立场景与固定步长模拟，避免改变其他游戏的全局物理设置。水果、表情、校徽只改变贴图；同等级使用相同圆形碰撞体。水果、表情、前 10 级校徽和音效来自用户授权的固定版本仓库，第 11 级中山大学校徽来自学校官网；完整路径、哈希及来源说明见[素材清单](watermelon-assets.md)。

```bash
scripts/test-watermelon.sh
scripts/test-watermelon-persistence.sh
scripts/test-watermelon-physics.sh
scripts/test-watermelon-pointer.sh
scripts/build-watermelon-validation.sh
ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.9.0-watermelon-validation.apk
"$ADB" -s DEVICE_SERIAL shell am start -n com.duanyao.xanaduyonder.watermelontest/com.unity3d.player.UnityPlayerActivity
```

`test-watermelon.sh` 编译生产规则模块，检查掉落队列、冷却、同级合并与重复碰撞、计分、最高级处理、警戒宽限和快照约束。`test-watermelon-persistence.sh` 使用真实 Newtonsoft.Json 解析器检查主题与完整运动状态往返、缺失/错误字段、损坏恢复、写入失败和其他游戏存档隔离。独立测试不运行 Unity 的物理求解器，也不能替代构建与设备回归。

`test-watermelon-physics.sh` 启动 Unity Play Mode，验证真实碰撞合并、堆叠、出生位置、独立场景、恢复运动及越线，结果位于 `artifacts/validation/watermelon-physics.txt`。执行前关闭此工作树的 Unity 编辑器。编译或运行故障会返回非零，超时会终止本次测试进程。

`test-watermelon-pointer.sh` 通过真实 UGUI 射线与生产指针处理器检查区域内起手、左右越界释放、上下越界取消、原指锁定、重复释放和帮助文案；它不替代 Android 原生触摸取消或多指验收。左右越界允许投放，上下越界和后台取消后不补投，完整行为见[边界投放记录](superpowers/specs/2026-10-30-watermelon-edge-release-design.md)。

验证脚本输出 `artifacts/builds/XanaduYonder-0.9.0-watermelon-validation.apk`，日志为 `artifacts/logs/build-watermelon-validation.log`；应用包标识为 `com.duanyao.xanaduyonder.watermelontest`。它使用独立应用数据目录，不覆盖正式应用或数独等其他工作树的安装。请使用上面的显式 ADB 安装与启动命令；普通 `install-android.sh` 启动的是正式包名。生成此包不会创建新版本或 GitHub Release，此前十款合集的发行情况见[v0.5.0 发布记录](releases/v0.5.0.md)。

`watermelon-v1.json` 独立保存棋盘状态、当前/下一颗、随机状态、主题、得分、最高分及目标提示状态，保存备份为 `watermelon-v1.json.bak`。暂停、退回大厅和应用切后台时应保留续局；主题切换应保持物体位置、大小与速度。需要在真机覆盖拖动松手、连续合并、触摸取消、暂停/恢复、进程重启续局、声音/触感设置及三个主题显示。

相对参考项目，本实现采用松手投球、持续 2 秒的稳定越线判定，以及首次合成最高级后的继续挑战。验收应覆盖短暂越线后回落、正在下落与弹跳的物体、持续堆高失败和第 11 级达成后继续。这些是当前实现的验收要求，文档不据此宣称已通过设备验证。

## 扫雷独立验证

扫雷的 Unity 界面测试和 APK 构建通过 `scripts/minesweeper-unity-environment.sh` 使用当前工作树的 `Library/MinesweeperGradle`，并分配独立临时 ADB 端口。不要把这些目录链接到其他工作树；该脚本只改变当前命令的环境，不修改全局 Gradle/ADB 设置。临时端口分配需要 Python 3。

07 扫雷已包含在正式 v0.5.0 的十款游戏合集中；当前开发源码包含十九个大厅入口（战争进化史入口包含两代，另含王国保卫战初代），版本为 0.9.0、Android versionCode 为 9。独立测试包沿用当前源码版本，不因生成测试包创建发布版本。玩法与数据契约见[完整设计](superpowers/specs/2026-10-27-minesweeper-design.md)，实际验证结果与未完成项统一记录在[扫雷验证记录](verification-minesweeper.md)。

本轮开发、测试和构建只使用 `/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`；不切换、清理、构建或修改其他工作树，不共享其他检出的 Library/Temp。以下命令均从这一本工作树根目录执行：

```bash
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-input.sh
scripts/test-minesweeper-persistence.sh
scripts/test-minesweeper-ui.sh
scripts/test-minigame-ui.sh
scripts/build-minesweeper-validation.sh
```

前四项使用 .NET 10、C# 7.3，直接编译生产规则、Session/Profile、整帧输入、Viewport/ControlLayout 与 Codec/Store。输入检查包括 400 ms 长按即时提交与一次反馈、最终 UP 位置、同帧多指仲裁、全抬释放门禁、0.5..2 连续缩放、总览返回和 44 dp 按钮布局换算；持久化检查使用真实 Newtonsoft.Json 13.0.2 与临时目录，覆盖完整快照、字段/token、备份及文件异常。

`test-minesweeper-ui.sh` 启动生产 UGUI 页面，注入内存 Profile 或明确的临时 Store，不读取或清空日常 `Application.persistentDataPath`。日志为 `artifacts/logs/minesweeper-ui-checks.log`，截图与报告位于 `artifacts/validation/minesweeper-ui/`。它需要图形渲染，不加 `-nographics`。两个 UI 检查与 Android 构建必须顺序执行；运行前关闭当前树的交互 Unity 编辑器。编辑器合成触点和尺寸换算不能代替真机多指、取消、物理 dp 与生命周期检查。

构建脚本调用 `ProjectBuild.BuildAndroidMinesweeperValidation`，通过 try/finally 临时替换包名/产品名并恢复原配置，输出 `artifacts/builds/XanaduYonder-0.9.0-minesweeper-validation.apk`，日志为 `artifacts/logs/build-minesweeper-validation.log`。仅成功退出且产物非空才可作为本次构建结果，独立包名是 `com.duanyao.xanaduyonder.minesweepertest`。以下安装/启动命令仅在本次构建成功并选定实际设备后执行：

```bash
MINESWEEPER_ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$MINESWEEPER_ADB" -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.9.0-minesweeper-validation.apk
"$MINESWEEPER_ADB" -s DEVICE_SERIAL shell am start -n com.duanyao.xanaduyonder.minesweepertest/com.unity3d.player.UnityPlayerActivity
```

不要用普通 `install-android.sh` 启动此包，该脚本启动的是正式包名。扫雷测试包使用独立数据目录；`minesweeper-v1.json` 与 `.bak/.tmp` 不修改其他游戏的存档。设备验收需覆盖真实双指/第三指/取消、50% 小格和 100% 基准尺寸、按钮点击范围、全屏/总览/返回层级、共享设置后台关闭、锁屏恢复、胜负一次结算、保存失败后重试及已有六款游戏回归。是否已执行逐项以验证记录为准。

## 推箱子独立验证与 UI 参考

推箱子的界面取舍、实际问题、内容与存档契约见[开发复盘与 UI 参考规范](sokoban-development-guide.md)，执行结果见[验证记录](verification-sokoban.md)。当前主线入口包含教学卡片、普通数字关卡和真实续局；游戏只做完整自动适配，通过方向盘、方向键或 WASD 移动。

从当前工作树根目录执行：

```bash
scripts/test-sokoban.sh
scripts/test-sokoban-persistence.sh
scripts/test-sokoban-input.sh
scripts/test-sokoban-content.sh
scripts/test-sokoban-ui.sh
scripts/test-minigame-ui.sh
scripts/build-sokoban-validation.sh
```

最后三项会启动 Unity，必须串行执行，运行前关闭当前树的交互编辑器。专用环境脚本隔离 Gradle 和 ADB；UI 夹具注入内存 profile，不读取日常玩家进度。构建输出当前版本的 `artifacts/builds/XanaduYonder-0.9.0-sokoban-validation.apk`，包名为 `com.duanyao.xanaduyonder.sokobantest`。这仍是包含当前全部游戏的合集构建，独立包名用于保护正式应用及其他测试包的数据。

安装前核对本次成功构建的文件、哈希和实际设备序列号；不要用启动正式包名的普通安装脚本替代下面的专用目标：

```bash
SOKOBAN_ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$SOKOBAN_ADB" devices -l
"$SOKOBAN_ADB" -s DEVICE_SERIAL install --no-streaming -r artifacts/builds/XanaduYonder-0.9.0-sokoban-validation.apk
"$SOKOBAN_ADB" -s DEVICE_SERIAL shell am start -W -n com.duanyao.xanaduyonder.sokobantest/com.unity3d.player.UnityPlayerActivity
```

稳定存档为 `sokoban-v1.json`，与其他玩法分离。继续、撤回、选关、后台恢复、多指取消、系统安全区和音频仍需按[检查表](sokoban-development-guide.md#8-后续小游戏可直接使用的检查表)完成对应验证；安装成功只证明本次安装步骤。

## 连连看独立验证

17 连连看从大厅进入轻松／挑战选关页，五档为 24／36／48／96／192 张、6／9／12／24／48 种图案，每种四张；两个模式各五关共十个独立续局槽位。挑战初始时间为 90／120／150／300／600 秒，新两关为 5／10 分钟。先读[设计与调研](link-match-research.md)、[UI 原型说明](prototypes/link-match/README.md)和[架构契约](architecture.md#连连看的独立边界)；原型的交互或截图不能替代生产 UGUI、Android 输入与存档验证。

```bash
scripts/test-link-match.sh
scripts/test-link-match-input.sh
scripts/test-link-match-persistence.sh
scripts/test-link-match-ui.sh
(PROJECT_ROOT="$PWD"; source scripts/link-match-unity-environment.sh; scripts/test-minigame-ui.sh)
scripts/build-link-match-validation.sh
```

前三项分别使用生产 C# 规则、输入／布局及真实 Newtonsoft 存档代码；规则覆盖合法路径和生成见证，输入覆盖单次释放及取消，存档检查应覆盖十槽、严格 v1 六槽迁移、计时／道具、终局幂等和文件事务；新增密度还需检查 96／192 张的布局、相邻格命中与性能。`test-link-match-ui.sh` 启动 Unity Play Mode，以生产 UGUI、`Camera.Render` 和指针射线检查页面，输出 `artifacts/validation/link-match-ui/report.txt` 及截图，日志为 `artifacts/logs/link-match-ui-checks.log`。这些命令描述验证入口，不表示已经执行或通过。

后三项会使用 Unity，必须在当前工作树中串行执行，运行前关闭本树的交互编辑器。连连看的 UI／构建脚本 source `scripts/link-match-unity-environment.sh`，使用本树 `Library/LinkMatchGradle` 和临时独立 ADB 端口；不复用其他工作树的 Library、Temp 或构建产物，不停止共享 ADB／Gradle 服务。

`build-link-match-validation.sh` 调用 `ProjectBuild.BuildAndroidLinkMatchValidation`，目标为 `artifacts/builds/XanaduYonder-0.9.0-link-match-validation.apk`，日志为 `artifacts/logs/build-link-match-validation.log`，独立包名为 `com.duanyao.xanaduyonder.linkmatchtest`。这是包含当前源码游戏的合集开发验证包；构建入口在 finally 中恢复原包名和产品名。正常开发包、正式包的路径及标识不变，也不会自动创建 Release。产物是否成功以本次构建结果、时间和哈希为准，不能只凭目录已有同名 APK 判断。

确认设备序列号后显式安装、启动此独立包，不用普通安装脚本代替包名隔离：

```bash
LINK_MATCH_ADB="${ANDROID_SDK_ROOT:-$HOME/Library/Android/sdk}/platform-tools/adb"
"$LINK_MATCH_ADB" devices -l
"$LINK_MATCH_ADB" -s DEVICE_SERIAL install --no-streaming -r artifacts/builds/XanaduYonder-0.9.0-link-match-validation.apk
"$LINK_MATCH_ADB" -s DEVICE_SERIAL shell am start -W -n com.duanyao.xanaduyonder.linkmatchtest/com.unity3d.player.UnityPlayerActivity
```

当前 `link-match-v2.json` 及 `.bak/.tmp` 只属于连连看，schema/content 均为版本 2。仅当 v2 主档与备份都不存在时，才严格校验 v1 六槽后按模式及稳定关卡 ID 迁移，保留 v1 文件；旧挑战槽 3／4／5 映射到新 5／6／7，新增关卡槽为空。已有 v2 损坏时不回退 v1；迁移写失败保留内存进度与持续错误提示，后续可重试。安装验证包前先保留旧进度，使用覆盖安装，不卸载清数据。

验收需分别覆盖：入口浏览不建局、十槽恢复和目标槽重开、合法及非法 v1 迁移、旧备份恢复、新版损坏不回退、迁移写失败、下一关已有局的续接、第 03／04 关前往下一关与第 05 关末关、最后一对与计时截止、无路自动重排、后台遮盘与明确继续、短屏 48／96／192 张及外围路线、真实相邻触点取消／多指、游戏与公共设置保存失败提示，以及从 Zuma／Dino 横屏返回后的宿主恢复。Android dp、系统安全区、音效触感与实际设备交互另行记录；构建成功或安装启动成功都不代表这些场景已通过。

首版三关的历史测试、截图和 APK 记录保留在[原验证记录](verification-link-match.md)。新增五关、v2 迁移、生产 UGUI 对照和设备边界单独记入[扩关验证记录](verification-link-match-expanded.md)；[浏览器密度对照](prototypes/link-match-density/README.md)仅用于设计展示，不能替代这些验证。

## 正式发布 APK

发布构建使用独立、长期保留的私有签名。通过安全的本地凭据管理方式给当前进程设置以下环境变量，不要把口令直接写入命令历史、仓库或日志：

| 环境变量 | 用途 |
| --- | --- |
| `XANADU_KEYSTORE_PATH` | 私有 keystore 的绝对路径 |
| `XANADU_KEY_ALIAS` | 发布密钥别名 |
| `XANADU_KEYSTORE_PASSWORD` | keystore 口令 |
| `XANADU_KEY_PASSWORD` | 密钥口令 |

```bash
scripts/build-release.sh
```

脚本缺少签名配置时立即失败。Unity 子进程不会收到上述变量，只生成关闭 Development Build、脚本调试与 profiler 连接的中间 APK；签名配置不进入 Unity 的 `ProjectSettings`。构建入口在结束或抛出异常时恢复原来的 Development 和自定义签名开关。

脚本使用配套 SDK 的 `apksigner` 替换中间 APK 的调试签名，口令只经环境变量传入；拒绝 debuggable APK，并在签名验证成功后原子替换 `artifacts/builds/XanaduYonder-0.9.0-android-arm64.apk`。中间文件不会作为发布产物保留。脚本返回非零代表本次失败，即使目录中存在较早的正式 APK，也不能将其当作本次构建成功。可以通过 `APKSIGNER`、`AAPT` 覆盖工具路径。

Unity 日志为 `artifacts/logs/build-android-release.log`，本地签名验证日志为 `artifacts/logs/sign-android-release.log`。只上传最终 APK、校验和及公开发布说明；不要上传 keystore、口令、私有签名日志或整个构建目录。后续版本必须使用同一发布密钥才能覆盖升级。正式签名与现有开发签名不同，Android 会拒绝直接覆盖开发包；测试迁移前应先保护设备存档，不能直接卸载并丢失进度。

### GitHub CLI

macOS 通过 Homebrew 安装 GitHub 官方 CLI，后续创建 Release、上传附件及校验可直接使用 `gh`：

```bash
brew install gh
gh auth login --hostname github.com --git-protocol ssh --web --skip-ssh-key
gh auth status
```

首次登录在 GitHub 完成设备授权与必要的二次验证；复用现有 SSH 密钥，不生成或上传新密钥。登录后检查 `gh auth status` 显示系统钥匙串存储，不在仓库、脚本或环境文件中保存令牌。参见 [GitHub CLI 登录文档](https://cli.github.com/manual/gh_auth_login)。

发布前先验证 APK、签名、校验文件及实际目标分支，将代码和对应标签推到远端。通过 `gh release create --draft --verify-tag --notes-file` 创建草稿并上传指定的 APK 与校验文件；回读附件确认文件长度和 SHA-256 后，再用 `gh release edit --draft=false --latest` 发布。版本号、附件路径和说明文件均应使用本次版本的真实值，不覆盖已有版本附件。

## 欢乐球球独立验证

欢乐球球初版保留源码版本 0.5.0/versionCode 5；当前构建沿用本次 0.9.0/versionCode 9，通过 `scripts/happy-ball-unity-environment.sh` 使用本工作树独立 `Library/HappyBallGradle` 和临时 ADB 端口，不修改全局环境。UI 检查与构建必须串行执行，测试使用临时存档目录。

```bash
scripts/test-happy-ball.sh
scripts/test-happy-ball-input.sh
scripts/test-happy-ball-persistence.sh
scripts/test-happy-ball-ui.sh
scripts/test-minigame-ui.sh
scripts/build-happy-ball-validation.sh
```

APK 为 `artifacts/builds/XanaduYonder-0.9.0-happy-ball-validation.apk`，包名 `com.duanyao.xanaduyonder.happyballtest`、名称“Xanadu 球球测试”，使用调试签名，独立于正式应用。图形报告/长短屏截图位于 `artifacts/validation/happy-ball-ui/`，构建日志为 `artifacts/logs/build-happy-ball-validation.log`。包名和产品名在构建结束后恢复。

有已授权设备时，使用 Unity 配套 `adb -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.9.0-happy-ball-validation.apk` 安装，并显式启动测试包；不要用启动正式包名的通用安装脚本。真机需验证左右转向、释放停止、多指取消、越过竖板的时机、连续红板的重击例外、分裂动效、音效触感、后台/锁屏暂停、读档及长局性能。实际完成范围见[验证记录](verification-happy-ball.md)。

## 项目与缓存

应提交 `Assets/` 及其 `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/`、测试和脚本。`Library/`、IDE 生成的 `.sln/.csproj`、测试 `bin/obj`、APK、设备截图/日志、签名与许可均不提交。

开发环境建议预留约 60 GB，且只保留需要的编辑器版本与平台模块。Android 工具链与个人已有的 Android Studio SDK 分开管理，避免版本混用。

## 应用图标与启动标识

`Assets/Branding/XY_logo.png` 原样使用项目所有者提供的完整图像，不裁剪或修改原图像素。每次构建的 `AppBranding.Configure()` 配置默认及 Android Legacy/Round/Adaptive 图标。Legacy/Round 位图保留原图中的 Logo 与白边比例；Adaptive 图标在生成的 XML 中给完整前景图像添加每边 `1/6`（`16.666667%`）的 inset，抵消 Android 将 108 dp 图层的中央 72 dp 显示为图标造成的放大，使可见图标内的 Logo 与白边比例保持与原图一致。机制见 [AdaptiveIconDrawable 官方说明](https://developer.android.com/reference/android/graphics/drawable/AdaptiveIconDrawable)与 [InsetDrawable 比例内缩说明](https://developer.android.com/reference/android/graphics/drawable/InsetDrawable)。

Unity 原生 Splash 和 Unity 标识均禁用，原生 Logo 列表清空。Bootstrap 上的 `BrandSplash` 使用位于场景根节点的独立 Canvas 居中显示完整原图，图片宽度为屏幕短边的 `0.5`。所有 `Awake` 完成后、首帧渲染前重新应用启动层排序，避免动态创建的大厅提前盖住仍拦截触摸的启动层。白底静止显示至少 `1.75` 秒，并等待大厅布局及输入链路就绪；随后同帧解除射线拦截、隐藏并销毁启动层，不再淡出。组件或宿主停用时也立即清理独立启动层。Android 12 系统启动窗口保持白底，引用经过上述 inset 修正的 `app_icon`；Gradle 生成后回调同时覆盖实际 Activity 主题的窗口背景。

Unity 6 Personal 允许关闭或定制 Unity 启动标识，见[官方发行说明](https://unity.com/releases/editor/whats-new/6000.0.0f1)与 [6000.3 Player Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettingsAndroid.html)。构建后需检查生成的 Adaptive XML、原生 Splash 禁用配置，并通过系统应用图标截图及冷启动录像确认留白比例、Android 原生窗口与 `BrandSplash` 的显示效果。


## 五子棋独立验证

完整设计决策、算法边界、开发问题与后续维护约定见[五子棋开发知识与维护记录](gomoku-development-record.md)。

在五子棋工作树内运行以下命令；同一工作树的 Unity 界面测试和 APK 构建应串行执行：

```bash
scripts/test-gomoku.sh
scripts/test-gomoku-session.sh
scripts/test-gomoku-persistence.sh
scripts/test-gomoku-input.sh
scripts/test-gomoku-ui.sh
scripts/build-gomoku-validation.sh
```

前四项直接编译生产 C# 源码；界面测试用真实 UGUI、相机渲染和指针事件，输出 `artifacts/validation/gomoku-ui/`。验证构建使用独立包名 `com.duanyao.xanaduyonder.gomokutest`，输出 `artifacts/builds/XanaduYonder-0.9.0-gomoku-validation.apk`，不覆盖正式应用的数据。`ProjectBuild.BuildAndroidGomokuValidation` 用 try/finally 恢复包名与产品名；不修改发布版本或签名。

两个 Unity 入口均 source `scripts/gomoku-unity-environment.sh`，使用本工作树的 `Library/GomokuGradle` 及临时私有 ADB 端口。不复用或链接其他工作树的 Library、Temp 和构建产物，不停止共享 ADB / Gradle 服务。安装时明确指定已确认设备序列号和测试包，设备未连接时不要把编辑器测试称为真机验证。实际证据及限制见[五子棋验证记录](verification-gomoku.md)。


本次发布入口为 [v0.9.0](releases/v0.9.0.md)，包含十九个大厅入口，新增战争进化史两代与王国保卫战初代；已完成三视图、大厅清单和菜单内容检查，正式包与设备验证边界见该记录。历史 [v0.7.0](releases/v0.7.0.md) 新增四款游戏，发布时未追加整套回归；[v0.6.0](releases/v0.6.0.md) 包含十三款游戏，各版本当时的发布边界保持原记录。黄金矿工开发问题、知识与维护规范简述见[开发复盘](gold-miner-development-notes.md)。


## 数字华容道独立验证与开发复盘

[开发复盘与维护规范](sliding-puzzle-development-guide.md)整理 UI 参数、实际问题根因、计时与生命周期、历史重放和文件事务，以及后续接入检查表；规则与存档字段见[玩法说明](sliding-puzzle-game.md)，实际执行结果见[验证记录](verification-sliding-puzzle.md)。

从当前隔离工作树执行：

```bash
scripts/test-sliding-puzzle.sh
scripts/test-sliding-puzzle-persistence.sh
scripts/test-sliding-puzzle-ui.sh
scripts/build-sliding-puzzle-validation.sh
```

后两项启动 Unity，必须串行执行；环境脚本隔离本树 Gradle 缓存和 ADB 服务，UI 夹具使用内存 profile 或独立临时目录。UI 检查需要图形渲染，不加 `-nographics`。验证包使用 `com.duanyao.xanaduyonder.slidingpuzzletest`；输出文件名从 `ProjectBuild.Version` 读取，当前为 `artifacts/builds/XanaduYonder-0.9.0-sliding-puzzle-validation.apk`，旧版 APK 不作为新构建成功的依据。设备安装需指定实际 ADB 服务端口和设备序列号，不覆盖正式包或其他小游戏测试包。
