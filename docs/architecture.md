# 架构与存档契约

当前源码接入 2048、跳一跳、旸了个旸、合成大西瓜、成语接龙、数独、扫雷和贪吃蛇；最新已发布的 v0.4.0 仍包含前六款。大厅、设置和生命周期由公共 shell 管理，每款游戏独立拥有规则、表现与存档。本页描述源码中的模块契约，最新正式发行版为 v0.4.0，发布情况见[版本记录](releases/v0.4.0.md)。

| 模块 | 规则 / 表现 | 独立文件 | 记录 |
| --- | --- | --- | --- |
| 2048 | `Game2048` / `BoardView` | `xanaduyonder-v1.json` | 对局、最高分、最高数字及共用声音/触感设置 |
| 跳一跳 | `Core/Jump` / `JumpPage`、`JumpSceneView` | `jump-v1.json` | 对局、最高分 |
| 旸了个旸 | `Core/Sheep` / `SheepPage`、`SheepBoardView` | `sheep-v2.json` | 三种模式独立对局、四档难度、固定配额、新局偏好、累计通关与标准无尽最高分 |
| 成语接龙 | `Core/Idiom` / `IdiomPage`、虚拟列表 | `idiom-learning-v1.json` | 四组对局、草稿、最佳数量、生词本、个人学习与跨局近期回复 |
| 数独 | `Core/Sudoku` / `SudokuSession`、`SudokuProfile`、`SudokuPage`、`SudokuBoardView` | `sudoku-v2.json` | 每难度一局、撤回、计时、提示、模式、全局通关记录 |
| 合成大西瓜 | `Core/Watermelon` / `WatermelonPage`、`WatermelonPhysicsWorld` | `watermelon-v1.json` | 对局、物体运动状态、最高分、主题 |
| 扫雷 | `Core/Minesweeper` / `MinesweeperSession`、`MinesweeperPage`、`MinesweeperBoardView` | `minesweeper-v1.json` | 三档独立对局、模式、正常视口、累计胜负和最佳毫秒数 |
| 贪吃蛇 | `Core/Snake` / `SnakeSession`、`SnakePage`、`SnakeBoardView` | `snake-v1.json` | 六组经典续局、闯关续局、独立纪录、顺序解锁 |

[跳一跳模块](jump-game.md)、[旸了个旸模块](sheep-game.md)、[成语接龙模块](idiom-game.md)与[数独模块](sudoku-game.md)分别说明各自规则和存档；合成大西瓜的独立边界见下文；扫雷的规则、整帧输入、只读总览和存档边界见[完整设计](superpowers/specs/2026-10-27-minesweeper-design.md)。新增游戏不修改以下 2048 和公共设置的旧协议。

## 职责

- `Assets/Scripts/Core` 不引用 Unity。`Game2048` 是棋盘状态的唯一来源，输入方向后返回移动轨迹、合并得分和新方块位置。快照采用防御复制，外部不能修改进行中的对局。
- `AppController` 管理大厅、游戏、设置和弹窗。大厅使用 `ScrollRect` 与 `RectMask2D`，顶部导航固定，离线说明及状态文案随卡片流滚动；内容中的卡片高 350 设计单位、间距 24，返回时保留滚动位置。游戏内继续使用各自的底部状态区域。
- 普通游戏页顶部导航在居中 720 单位列中使用统一几何：返回图标 `(56,34,58,58)`，右侧辅助图标 x536、设置 x606，顶部均为 34。带文字的返回入口可保留自身宽度，但左上位置一致。同排标题应对齐导航中心；标题字号、下方棋盘与操作区由各游戏自行布局。输入页继续保持键盘避让策略；扫雷专注布局的工具栏按其触控尺寸要求单独布局。
- `BoardView`、`JumpSceneView` 和 `SheepBoardView` 显示对应规则模型，不计算胜负。各页面在暂停、失焦、离开时终止尚未提交的输入或结束动画，并按最终模型重绘。shell 切换时使其他游戏页面离开，跳一跳同时关闭独立相机。
- `SwipeGesture` 保存一次触摸的所有权。只有从棋盘开始、超过阈值、由原手指正常结束的操作可以提交。UGUI 会把取消触摸派发为 pointerUp，适配层因此还会核对 `TouchPhase.Canceled`。
- `GameFeedback` 合成短音效，并通过 Android 系统触感接口反馈合并；开关由公共存档保存，不申请网络服务。
- `JsonSaveCodec` 使用 Unity 官方 Newtonsoft JSON 包的实际解析器，逐项校验 token 类型；`LocalSaveStore` 负责临时文件、原子替换和备份恢复。

## 2048 规则

棋盘以左上角为索引 0，按行存储 16 格。每条线从移动目标边缘处理，合并双方消耗后不会在本次移动再次合并。初始生成两个方块；每次有效移动后随机选择一个空位，并以 90% / 10% 的概率生成 2 / 4。无效移动既不生成方块，也不消耗随机数。

总分使用 64 位整数，达到 `long.MaxValue` 后饱和。最大方块为 2^30，此值不再合并，避免整数溢出。达到 2048 后可继续，胜利提示的已确认状态随对局保存。

## 2048 与公共设置文件

存放于 `Application.persistentDataPath/xanaduyonder-v1.json`。Android 正常路径为应用独占的外部文件目录；不读取其他应用的数据。`.bak` 为上一份合法存档，`.tmp` 为待提交文件。

```json
{
  "schemaVersion": 1,
  "saveKind": "xanaduyonder",
  "game": {
    "schemaVersion": 1,
    "gameId": "2048",
    "cells": [2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    "score": "0",
    "winState": 1
  },
  "bestScore": "0",
  "bestTile": 4,
  "soundState": 1,
  "hapticsState": 1
}
```

- 根版本、游戏版本与标识必须准确匹配。未知版本不迁移。
- 格子必须是 16 个整数，每格为 0 或 2..2^30 之间的二次幂；已保存的正常对局至少有两个非空格。
- 分数使用非负十进制字符串，避免跨工具处理时丢失大整数精度。缺失、null、数值类型、负值、溢出均拒绝。最高分不能低于当前分数。
- `bestTile` 为 2..2^30 之间的二次幂整数，不能小于当前盘面最大值；用于跨重开保存最高数字。兼容旧 schema 1 缺少该字段的文件，从当前保存盘面的最大值初始化；显式 null、错误类型及非法数字拒绝。升级前已经结束对局的最高数字无法从旧档恢复。编码时内存默认值 0 或尚未刷新的合法记录会与当前盘面取最大值后写出。
- `winState` 仅允许 1（未确认）/2（已确认）；已确认时必须有至少一个 >=2048 的方块。
- 声音与触感状态仅允许 1（开启）/2（关闭），不把缺失字段静默解释为关闭。
- 重复键、非法类型、过深或超过 64 KiB 的文件被拒绝；不向调用方暴露部分解码结果。

每次有效移动、设置变化以及应用暂停/失焦/退出前同步保存当前模型。写入先 flush 临时文件，再替换正式文件；只有正式文件合法时才用其更新备份，避免损坏文件覆盖最后一份可恢复数据。读取优先正式文件，其次备份，均不可用时启动新对局并显示提示。写入失败继续允许游玩并提示当前未保存，后续写入成功会清除该错误。

## 旸了个旸的独立边界

`SheepGame` 拥有棋盘、7 格卡槽、移出区、三消与道具状态；`SheepBoardLayout` 根据关卡和布局编号派生几何位置；`SheepLevelGenerator` 构造初始局和一条可解取牌顺序。`SheepSession` 管理模式切换、开局固定规则、换波及记录；`SheepPage` 在取牌或使用道具后先保存模型，再播放表现；连续快速点击可接续已提交的新状态。完整挑战获胜时先更新 `CompletedRuns/WinRecorded` 再写盘，避免恢复终局后重复计数。

`SheepSaveCodec` 严格校验 schemaVersion 2、gameId `sheep`、固定字段、JSON token 类型、模式/难度/波次、牌数与位置列表、固定上限及余量、撤回状态与记录一致性，文件限制 128 KiB、深度 8。三个模式槽可以为 null；已有对局必须完整合法。`SheepSaveStore` 仅访问 `sheep-v2.json` 及对应 `.bak/.tmp`，不迁移或续接 `sheep-v1.json`；损坏主档尝试合法备份，写入失败不清空旧档。详见[字段契约](sheep-game.md#独立存档)。

## 成语接龙的独立边界

`IdiomGame` 拥有完整回合、双方已用集合、按人计次的提示、当前回合辅助来源、结束原因与分别用于 APP/提示的随机状态；`IdiomLexicon` 拥有首字/首音索引。人机有效提交先完成用户词、学习记录与 APP 回复的规则更新，再保存、呈现；双人每次只提交当前方的词，不积累机主熟悉词。

`IdiomPage` 负责字/音 × 人机/本地双人的四组对局、对话列表、草稿与弹窗。四局共享一个 `IdiomLearningProfile`，记录熟悉词、生词、独立/辅助使用及近期 APP 展示；生产入口注入时钟。只进入成语页时加载压缩目录，详情按分片读取并保留最多 4 个缓存分片。Android 键盘通过独立桥接计算可见区域；IME 抢焦与应用切后台分别处理，暂停前保留草稿并结束原生编辑会话。

`IdiomSaveStore` 原子保存 `idiom-learning-v1.json` 及对应备份/临时文件，将四局、生词本和个人学习资料作为同一事务，整体上限 64 MiB，生词本最多 50,000 项/32 MiB。严格检查版本、字段类型、完整状态与生词条目/学习标记的一致性；失败的生词编辑同时回滚内存中的生词与学习状态。坏档不会直接覆盖，恢复前先归档原文件。当前入口不再使用独立 `IdiomNotebookStore`，不迁移旧成语文件，也不修改其他小游戏协议。推荐质量、权重和恢复见证见[选词设计](idiom-selection.md)。

## 04 合成大西瓜的独立边界

`WatermelonGame` 拥有球身份、等级、队列/RNG、得分、冷却与越线计时。`WatermelonPhysicsWorld` 为此对局创建独立 `LocalPhysicsMode.Physics2D` 场景，每步先由原生求解器模拟、再完整回传运动、按球 ID 顺序原子合并、最后推进失败判定。碰撞回调只收集接触；被消耗球在延迟销毁前立即停止模拟和碰撞。创建刚体前同步 Transform，启用后恢复保存的位置/旋转/速度。

`WatermelonPage` 负责单指瞄准、主题、图鉴、反馈和生命周期。主题只替换球体图片，不触碰物理尺寸与规则。暂停/弹窗/失焦不推进模拟；投放立即保存，运动中的快照每 0.8 秒保存，进入设置、离开、后台与结果弹窗前同步保存。所有视图、音频和局部物理场景由页面或其对象独占；销毁成对释放。

`WatermelonSaveCodec` 只接受 schemaVersion 1、gameId `watermelon`、固定字段、精确类型和合法完整快照，限制 128 KiB、深度 8。保存球的位置、速度、旋转、角速度、接触历史和年龄，以及当前/下一颗、RNG、下一个 ID、冷却、危险计时、终局、主题、最高分和目标确认状态。`WatermelonSaveStore` 仅操作 `watermelon-v1.json/.bak/.tmp`，原子替换时只备份有效主档。恢复不默认填补缺失字段；新对局保留主题与最高分。

核心和存储分别运行独立 C# 检查，碰撞行为由 `scripts/test-watermelon-physics.sh` 在 Unity Play Mode 内验证；[Android 验证记录](verification-watermelon.md) 分开记录真机、模拟器和未人工验收项。

## 贪吃蛇的独立边界

`Core/Snake` 拥有格子、边界、碰撞、增长、速度和确定性随机状态；`SnakeSession` 拥有最多两步方向队列、逻辑节拍、暂停、倒计时及有效游戏时间。四边相通在坐标归一化后仍检查蛇身与障碍；满盘先于关卡目标判胜，空格为零时不再生成食物。前台帧超过 250 ms 或将积压超过两步时整帧暂停，后台不补跑。

`SnakePage` 使用统一导航、完整 20×24 棋盘、左侧操作与右侧十字键；`SnakeDropdown` 复刻数独选项面板的几何与色彩。展开菜单先暂停；经典难度选择直接加载独立槽，手动关卡预览确认后开始。轻量菜单返回保留来源意图，重开与边界变更取消仍保留原局暂停。页面和控制区不提供全屏、缩放或常驻提示；存档失败使用异常横条提示。`SnakeBoardView` 只接收规则和快照绘制，`SnakeInput` 负责连续手势和按下转向。

`SnakeProfile` 分别保留三档难度×两种边界的六场经典对局，以及一场闯关对局；记录与解锁只在终局记账一次。`SnakeSaveCodec` 使用真实 Newtonsoft token 校验版本、坐标邻接、食物、计分、随机状态与终局一致性，`SnakeSaveStore` 独立原子写入 `snake-v1.json/.bak/.tmp`。不兼容的内容版本清理活动槽、保留可验证纪录，并在覆盖前归档旧文件。底层恢复会话保持暂停；用户主动进入 / 开始时直接倒计时，系统前台恢复仍需明确继续。开始、吃食、终局、暂停、离开、后台及运行中每两秒保存，不改其他游戏协议。

内置 `Resources/Snake/levels.json` 与设计阶段 12 关配置一致；地图包、规则、schema 分别版本化。静态连通与通道校验不代表任意蛇身局面可解。图形为原创 UGUI 几何，音效与触感沿用 `GameFeedback` 和共用设置。

独立验证入口为 `scripts/test-snake.sh`、`scripts/test-snake-persistence.sh`、`scripts/test-snake-ui.sh`；Android 验证包使用 `.snaketest` 后缀。完整入口与返回政策见[操作动线](snake/interaction-flow.md)；实际完成的编译、运行与设备验证分别记录在[贪吃蛇实现验证](snake/verification-implementation.md)。

## 验证层次

`scripts/test-core.sh`、`scripts/test-input.sh`、`scripts/test-persistence.sh` 编译同一份生产 C# 源码；持久化测试在真实临时目录执行，使用与 Unity 包对应的 Newtonsoft.Json 13.0.2。Unity 内还执行同一组存档检查。跳一跳另有 `scripts/test-jump.sh` 和 `scripts/test-jump-persistence.sh`；旸了个旸另有 `scripts/test-sheep.sh` 和 `scripts/test-sheep-persistence.sh`，覆盖同一份生产规则与存档代码。测试中的生成解法通过真实 `SheepGame.Pick` 重放，检查遮挡与卡槽容量约束，而不只检查牌数是否为 3 的倍数。Android 真机验证负责触摸分发、滚动、图形、IL2CPP、文件系统和应用生命周期的最终集成；源码审查和独立测试不代表设备验收已完成。

当前旸了个旸采用 schema 2；移出/撤回/洗牌默认上限为 2/3/1，设置支持 0..99，已有对局保持开局上限及剩余次数，复活每局一次。无尽按消除张数累计计分，换波不补道具或复活。几何仍支持旧布局 0..2，新挑战与无尽只生成 3..14 并排除上一布局。布局、会话和难度分别通过 `scripts/test-sheep-layouts.sh`、`scripts/test-sheep-session.sh`、`scripts/test-sheep-difficulty.sh` 验证；难度评估不读取生成解法，模拟完成率不代表真人胜率。

## 数独的独立边界

`SudokuGame` 拥有正式数字、候选、撤回、错误与提示配额；`SudokuControls` 将普通/快速和正式/铅笔四种输入模式映射成规则操作。`SudokuSession` 拥有暂停、计时和已消耗次数的帮助，导出时带上本局产生的完成记录；`SudokuProfile` 合并全局完成记录并管理每个难度的一场未完成对局。`SudokuPage` 负责 Unity 呈现、输入和异步请求生命周期。

内置题库有四档各 500 道题，题面与唯一答案固定版本。答案计算、唯一性搜索、逻辑难度评级和完整提示证据重放在开发机完成。客户端启动只检查资源 SHA-256、题面与答案一致性、答案行列宫合法性；填写时直接比对内置答案。题库导入与校验见[内容管线](sudoku-content.md)。

提示从当前正确盘面重新构建候选，在后台线程中进行一步逻辑推导，有取消检查和 200ms 预算。无支持技巧或超预算时明确提供直接揭晓。主线程只接受仍属于当前会话、请求、盘面 revision 且在前台未暂停的结果，并在展示时扣除一次配额；过期、取消和计算失败不扣次。帮助与扣次同次保存，重看同一帮助不重复扣次，任何盘面变化令其失效。

`sudoku-v2.json` 按难度保留最多一场未完成对局及全局完成记录；仅在新版主档和备份均不存在时迁移 `sudoku-v1.json`，保留旧文件，已有新版存档时不回退到旧版进度。数独存档单独校验所有 JSON token、候选、合法动作历史、累计错误/提示、关卡 ID 和终局一致性，以临时文件 flush 后原子替换主档。其他游戏的协议未改动。测试入口分别检查规则、真实文件持久化、会话生命周期、四档存档迁移、关卡选择策略和完整题库证明，Android 验证范围记录在[初版数独验证](verification-sudoku.md)及[本次修订验证](verification-sudoku-refinement.md)。
