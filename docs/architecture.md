# 架构与存档契约

当前 0.5.0 源码接入 2048、跳一跳、旸了个旸、合成大西瓜、成语接龙、数独、扫雷、Zuma、Dino、五子棋和欢乐球球，共十一款游戏。大厅、设置和生命周期由公共 shell 管理，每款游戏独立拥有规则、表现与存档；Dino 只跨进程保存纪录和方向偏好，不保存对局。本页描述源码中的模块契约，发布情况见[v0.5.0 版本记录](releases/v0.5.0.md)。

| 模块 | 规则 / 表现 | 独立文件 | 记录 |
| --- | --- | --- | --- |
| 2048 | `Game2048` / `BoardView` | `xanaduyonder-v1.json` | 对局、最高分、最高数字及共用声音/触感设置 |
| 跳一跳 | `Core/Jump` / `JumpPage`、`JumpSceneView` | `jump-v1.json` | 对局、最高分 |
| 旸了个旸 | `Core/Sheep` / `SheepPage`、`SheepBoardView` | `sheep-v2.json` | 三种模式独立对局、四档难度、固定配额、新局偏好、累计通关与标准无尽最高分 |
| 成语接龙 | `Core/Idiom` / `IdiomPage`、虚拟列表 | `idiom-learning-v1.json` | 四组对局、草稿、最佳数量、生词本、个人学习与跨局近期回复 |
| 数独 | `Core/Sudoku` / `SudokuSession`、`SudokuProfile`、`SudokuPage`、`SudokuBoardView` | `sudoku-v2.json` | 每难度一局、撤回、计时、提示、模式、全局通关记录 |
| 合成大西瓜 | `Core/Watermelon` / `WatermelonPage`、`WatermelonPhysicsWorld` | `watermelon-v1.json` | 对局、物体运动状态、最高分、主题 |
| 扫雷 | `Core/Minesweeper` / `MinesweeperSession`、`MinesweeperPage`、`MinesweeperBoardView` | `minesweeper-v1.json` | 三档独立对局、模式、正常视口、累计胜负和最佳毫秒数 |
| Zuma | `Core/Zuma` / `ZumaPage`、`ZumaView` | `zuma-v1.json` | 18×4 最佳成绩、完整对局、结果提交/确认标记 |
| Dino | `Core/Dino` / `DinoPage`、`DinoViewport`、`DinoTrackView` | `dino-v1.json` | 横／竖屏最高纪录和上次成功进入方向；不保存跑酷局面 |
| 五子棋 | `Core/Gomoku` / `GomokuSession`、`GomokuPage`、`GomokuBoardView` | `gomoku-v1.json` | 人机／双人各一局、完整棋谱与新局偏好；预选和 AI 任务不落盘 |
| 欢乐球球 | `Core/HappyBall`、`HappyBallPage`、`HappyBallSceneView` | `happy-ball-v1.json` | 完整塔/球/重击/RNG 状态、本局与最高分、开始与结算标记 |

[跳一跳模块](jump-game.md)、[旸了个旸模块](sheep-game.md)、[成语接龙模块](idiom-game.md)、[数独模块](sudoku-game.md)与[Dino 模块](dino-game.md)分别说明各自规则和存档；合成大西瓜的独立边界见下文；扫雷的规则、整帧输入、只读总览和存档边界见[完整设计](superpowers/specs/2026-10-27-minesweeper-design.md)。新增游戏不修改以下 2048 和公共设置的旧协议。

Zuma 模块见[原版 UI、横屏与独立续局设计](superpowers/specs/2026-10-27-zuma-design.md)、[完整开发记录](zuma-development-record.md)和[验证记录](verification-zuma.md)，保持以下旧存档协议不变。独立横屏画布采用 1280×720 设计尺寸，退出后恢复大厅方向；原竖屏布局常量保持不变。

## 职责

- `Assets/Scripts/Core` 不引用 Unity。`Game2048` 是棋盘状态的唯一来源，输入方向后返回移动轨迹、合并得分和新方块位置。快照采用防御复制，外部不能修改进行中的对局。
- `AppController` 管理大厅、游戏、设置和弹窗。大厅使用 `ScrollRect` 与 `RectMask2D`，顶部导航固定，离线说明及状态文案随卡片流滚动；内容中的卡片高 350 设计单位、间距 24，返回时保留滚动位置。游戏内继续使用各自的底部状态区域。
- 普通游戏页顶部导航在居中 720 单位列中使用统一几何：返回图标 `(56,34,58,58)`，右侧辅助图标 x536、设置 x606，顶部均为 34。带文字的返回入口可保留自身宽度，但左上位置一致。同排标题应对齐导航中心；标题字号、下方棋盘与操作区由各游戏自行布局。输入页继续保持键盘避让策略；扫雷专注布局的工具栏按其触控尺寸要求单独布局。
- `BoardView`、`JumpSceneView` 和 `SheepBoardView` 显示对应规则模型，不计算胜负。各页面在暂停、失焦、离开时终止尚未提交的输入或结束动画，并按最终模型重绘。shell 切换时使其他游戏页面离开，跳一跳同时关闭独立相机。
- `SwipeGesture` 保存一次触摸的所有权。只有从棋盘开始、超过阈值、由原手指正常结束的操作可以提交。UGUI 会把取消触摸派发为 pointerUp，适配层因此还会核对 `TouchPhase.Canceled`。
- `GameFeedback` 合成短音效，并通过 Android 系统触感接口反馈合并；开关由公共存档保存，不申请网络服务。
- `JsonSaveCodec` 使用 Unity 官方 Newtonsoft JSON 包的实际解析器，逐项校验 token 类型；`LocalSaveStore` 负责临时文件、原子替换和备份恢复。
- Dino由独立安全区根节点承载横／竖屏布局，公共竖屏框架不承载其横屏场景。`AppController` 负责方向请求、路由和公共设置数据；`DinoPage` 自己展示设置、暂停和结算，使用注入的委托读写共用开关，不向公共 UI 基类添加Dino业务分支。

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

## 推箱子的独立边界

`SokobanLevel` 和 `SokobanCatalog` 校验固定关卡、来源及解法；`SokobanGame` 是四向移动、单箱推动、归位、完整动作历史与撤回的唯一规则所有者。静态目标与动态人箱分离，阻挡不计步。`SokobanSession` 管理尝试身份与输入门禁；`SokobanProfile` 按关卡去重完成记录，并保存最少步数和最少推动对应的两个真实成绩元组。

`SokobanPage` 使用原型测量后的安全区布局：入口展示真实续局与分组关卡，游戏页使用统一导航和标题、单行信息、圆角棋盘和集中操作区。`SokobanViewport` 只做完整居中适配；`SokobanInput` 仲裁方向控件，忽略棋盘滑动、缩放与平移，不修改地形。多指、弹窗、离页和后台取消未提交输入。预览与正式活动局分离，浏览入口不产生假存档；旧缩放存档恢复历史后归一到完整适配。AppController 负责大厅路由、共享设置及生命周期。可复用方法见[开发复盘与 UI 参考规范](sokoban-development-guide.md)。

`SokobanSaveCodec` 与 `SokobanSaveStore` 独立保存 `sokoban-v1.json`。严格校验 JSON 类型、必要字段、版本、有限视口数值及动作重放后的盘面一致性，限制 2 MiB、深度 12、50,000 步；临时文件刷盘后原子替换，只有合法主档可更新备份。内容版本变更时重新开始尝试并保留仍可对应的完成记录。损坏文件归档，写入失败不阻断内存游玩，并持续显示未保存提示。

入包内容从已归档许可资源导入，8 关原创与 100 关 Boxoban 都通过生产规则重放；运行时不联网、不自动求解。源池和开发工具放在 Resources 之外，发布资源仅包含精选素材和关卡。详见[方案](superpowers/specs/2026-10-28-sokoban-design.md)与[验证记录](verification-sokoban.md)。

## Dino的独立边界

`DinoGame` 是跑酷状态的唯一来源，拥有角色运动、障碍、确定性随机状态、距离分和碰撞终局。`DinoRules`、`DinoMotion` 与 `DinoCollision` 不引用 Unity；横屏和竖屏统一使用 Chrome 普通移动端模式的固定 60 Hz 规则。初速按实际视野缩放，每固定步增加 0.001 原图速度，最高均为 13 原图像素／步；世界单位换算、跳跃和障碍间距见[玩法说明](dino-game.md#共享规则与视野约束)。`DinoTraversal` 仅用于编辑器诊断，不参与生产生成。官方细分碰撞框配合连续扫掠使所有对象停在首次接触比例；地面独立累计与仙人掌相同的取整位移，计分距离保留原版未取整行程。

`DinoPage` 负责方向选择、页面状态和固定步时钟。`DinoViewport` 将实际安全区映射到独立 UI 根节点，保持横竖屏角色尺寸基准并把横屏额外宽度用于前方视野。`DinoActionOwners` 仲裁 Jump／Duck 的独立 owner，`DinoTouchControl` 处理原手指松开、滑出与取消。页面在持有跳跃输入且落地时尝试下一跳，下蹲优先；松开或清理 owner 后不排队续跳。生命周期暂停冻结模型动作意图后清理真实输入；继续前倒数三秒，暂停及倒数不推进分数或障碍。`DinoTrackView` 只显示模型并管理像素、背景和昼夜，`DinoAudio` 独占短音效音源并读取共用开关。

每次从大厅进入先选择方向，成功进入准备页才提交方向偏好；重开沿用本局方向，返回大厅前恢复竖屏。Dino的同进程后台局面仅保存在内存，离开或进程重启后重新开局。`DinoSaveCodec` 严格校验 `dino-v1.json` 的 schema 1、`gameId: "dino"`、两项非负 64 位十进制字符串纪录和限定方向，限制 4 KiB／深度 4；缺字段、null、错误类型、溢出、重复键和未知版本均拒绝。`DinoSaveStore` 仅访问该文件及 `.bak/.tmp`，flush 临时文件后原子替换，只以合法主档更新备份。新纪录按一秒节流写盘，暂停、碰撞、离开和退出立即补写；写失败保留可玩状态并显示未保存提示。

图集、音效与完整上游许可位于 `Assets/Resources/Dino/`，来源见[素材记录](dino-assets.md)。独立构建使用 `scripts/build-dino.sh` 和 `com.duanyao.xanaduyonder.dinotest`，不覆盖正式应用或其他小游戏测试包。

## 欢乐球球的独立边界

`HappyBallGame` 在纯 C# 中独占球的高度/速度、塔角度、层状态、连续下落、一次重击、分数和 RNG；固定步为 1/120 秒，球与有厚度的扇形横板/黑竖板进行连续扫掠判定。黑色侧碰撤回本步转角并按原角度重新计算竖直运动，红色只有向下重击横板时可以砸穿。状态从 `Armed` 到 `Spent` 再恢复 `Normal`，碰撞规则不依赖拖尾颜色或动画是否结束。

`HappyBallGenerator` 使用有界结构化生成，每层有 1～3 个缺口，位置与宽度变化，支持多段红黑区域；缺口随深度总体收窄并保留至少三个球直径的通过空间。完整入场带避免紧邻出口的即时红面，实际模型的多入口重放用于离线验证，不在每次生成时穷举路线。危险红面在第 6 层后出现，竖板在第 16 层后逐步加入，高度从约 0.85～1.05 递增到 1.25～1.50，层距为 2.4。活动窗口与已通过层保留量有上限，累计下降后平移局部原点。有限重放验证不代表真人难度或触控手感已经验收。

`HappyBallPage` 负责连续输入、固定步推进、明确暂停/继续和独立存档；`HappyBallTouchInput` 的整帧 reducer 取消多指/丢失触点/失焦输入，并清空尚未消费的转角。`HappyBallSceneView` 使用独立 layer 30、相机、RenderTexture 和共享核心尺寸生成网格；表现不使用 Unity 3D Physics。普通穿层和重击接触分别触发脱离，已有碎层恢复后不重播；碎块固定脱离时的旋转，8 组/16 块对象池只负责视觉。页面隐藏时停相机、回收动效，销毁时释放网格、材质、纹理与程序音效。

`happy-ball-v1.json` 保存 schema 1、`gameId=happy-ball`、核心规则版本、完整层扇区/竖板、球/塔/重击状态、RNG、最高分、HasStarted 和 ResultRecorded。当前规则版本为 2；版本 1 先按旧层距 1.65 和旧竖板上限 0.5 校验，再按当前层重新定位球和层高度，保留球速、塔角、随机状态及已有竖板。旧 Spent 连击标记仅补记尚未入账的重击分一次；已入账的状态不重复加分。构造模型和真实 Decode 共用迁移，之后保存版本 2。分数字段为非负十进制字符串；严格拒绝缺失/未知/重复字段、null、错误类型、非法 JSON 扩展、非有限数和不一致快照，上限 256 KiB/深度 12。真实文件写入采用临时文件 flush 后替换，只有有效主档才能更新备份；恢复优先主档再备份，失败提示可重试。进入游戏与恢复前台都保持暂停，不保存旧触点或碎片动画。其余游戏及公共设置文件不变。实际测试、截图与设备边界见[欢乐球球验证](verification-happy-ball.md)。

## 验证层次

Zuma 的纯规则由 `ZumaGame` 拥有，`ZumaPage` 调度导航、输入、运行与保存。`ZumaLevelCache` 保存纯路径数据，独立于可释放的视觉资源，使退出后仍能校验和重试保存。`ZumaSaveStore` 独占 `zuma-v1.json/.bak/.tmp`，未知版本及无法可靠识别的超限文件受写保护。成绩提交和结果确认分别持久化，声音/触感只通过宿主回调写入原公共设置。

Zuma 失焦/后台时取消触摸、冻结模拟并同步保存，回前台显示暂停。有效发射、换球、导航及每 2 秒运行周期保存；失败保留 dirty 内存并重试。新测试入口为 `scripts/test-zuma.sh`、`scripts/test-zuma-input.sh`、`scripts/test-zuma-persistence.sh` 和 `python3 scripts/test-zuma-assets.py`。

小游戏切换避免额外加载画面：Dino直接显示正式方向卡，旋转期间保留旧页并冻结交互；Zuma通过PresentationReady在目标完整画面就绪后交接宿主Canvas，退出保留旧图直到返回竖屏。宿主等待时关闭触摸及键盘交互，退出时取消缓存的按钮按压。Zuma只在自身进入/退出请求方向，非活动状态不修改其他游戏的方向。`scripts/test-zuma-navigation.sh`覆盖画面交接、取消、加载错误、方向归属及残留输入。

`scripts/test-core.sh`、`scripts/test-input.sh`、`scripts/test-persistence.sh` 编译同一份生产 C# 源码；持久化测试在真实临时目录执行，使用与 Unity 包对应的 Newtonsoft.Json 13.0.2。Unity 内还执行同一组存档检查。跳一跳另有 `scripts/test-jump.sh` 和 `scripts/test-jump-persistence.sh`；旸了个旸另有 `scripts/test-sheep.sh` 和 `scripts/test-sheep-persistence.sh`，覆盖同一份生产规则与存档代码。测试中的生成解法通过真实 `SheepGame.Pick` 重放，检查遮挡与卡槽容量约束，而不只检查牌数是否为 3 的倍数。Android 真机验证负责触摸分发、滚动、图形、IL2CPP、文件系统和应用生命周期的最终集成；源码审查和独立测试不代表设备验收已完成。

当前旸了个旸采用 schema 2；移出/撤回/洗牌默认上限为 2/3/1，设置支持 0..99，已有对局保持开局上限及剩余次数，复活每局一次。无尽按消除张数累计计分，换波不补道具或复活。几何仍支持旧布局 0..2，新挑战与无尽只生成 3..14 并排除上一布局。布局、会话和难度分别通过 `scripts/test-sheep-layouts.sh`、`scripts/test-sheep-session.sh`、`scripts/test-sheep-difficulty.sh` 验证；难度评估不读取生成解法，模拟完成率不代表真人胜率。

Dino的 `scripts/test-dino.sh` 编译生产规则与碰撞代码，`scripts/test-dino-session.sh` 检查输入 owner 和时钟，`scripts/test-dino-persistence.sh` 检查真实 JSON 解析与临时目录读写。这些入口分别验证规则、输入辅助模型和文件协议；实际 UGUI 事件分发、方向切换、音频、布局及 Android 生命周期仍需 Unity／设备集成检查，测试脚本或 APK 的存在不代表对应检查已经通过。

## 数独的独立边界

`SudokuGame` 拥有正式数字、候选、撤回、错误与提示配额；`SudokuControls` 将普通/快速和正式/铅笔四种输入模式映射成规则操作。`SudokuSession` 拥有暂停、计时和已消耗次数的帮助，导出时带上本局产生的完成记录；`SudokuProfile` 合并全局完成记录并管理每个难度的一场未完成对局。`SudokuPage` 负责 Unity 呈现、输入和异步请求生命周期。

内置题库有四档各 500 道题，题面与唯一答案固定版本。答案计算、唯一性搜索、逻辑难度评级和完整提示证据重放在开发机完成。客户端启动只检查资源 SHA-256、题面与答案一致性、答案行列宫合法性；填写时直接比对内置答案。题库导入与校验见[内容管线](sudoku-content.md)。

提示从当前正确盘面重新构建候选，在后台线程中进行一步逻辑推导，有取消检查和 200ms 预算。无支持技巧或超预算时明确提供直接揭晓。主线程只接受仍属于当前会话、请求、盘面 revision 且在前台未暂停的结果，并在展示时扣除一次配额；过期、取消和计算失败不扣次。帮助与扣次同次保存，重看同一帮助不重复扣次，任何盘面变化令其失效。

`sudoku-v2.json` 按难度保留最多一场未完成对局及全局完成记录；仅在新版主档和备份均不存在时迁移 `sudoku-v1.json`，保留旧文件，已有新版存档时不回退到旧版进度。数独存档单独校验所有 JSON token、候选、合法动作历史、累计错误/提示、关卡 ID 和终局一致性，以临时文件 flush 后原子替换主档。其他游戏的协议未改动。测试入口分别检查规则、真实文件持久化、会话生命周期、四档存档迁移、关卡选择策略和完整题库证明，Android 验证范围记录在[初版数独验证](verification-sudoku.md)及[本次修订验证](verification-sudoku-refinement.md)。


## 10 五子棋的独立边界

`Core/Gomoku/GomokuGame` 拥有 225 格棋盘和落子历史，四向连续五子或更多即胜，无禁手；无胜者且棋盘满为和棋。历史重放拒绝越界、重复和终局后的继续落子。对外棋盘、历史和胜利连线均防御性复制。坐标 A–O 从左向右，1–15 从下向上，H8 为中心。

`GomokuAi` 是原创离线 C# 搜索：全盘即时胜负扫描，邻域候选和棋形排序，迭代加深 Negamax / alpha-beta、定长置换表；困难加入有界连续冲四证明。简单、普通、困难分别采用 100 / 450 / 1200ms 目标预算，迭代深度目标 2 / 4 / 8，强制防守叶延伸另受 24 层及共享预算限制。预算耗尽返回已完成深度的结果或合法兜底点，取消则抛出取消异常。分支裁剪和有限搜索不保证最优或必胜，时间目标不等于所有设备上的精确耗时。

`GomokuSession` 拥有预选、确认、回合、暂停及撤销策略。人机撤回上次人类落子及其后的电脑回复，人类执白时保留电脑开局；双人由页面取得对方确认后撤回一手。AI 请求绑定会话对象、SessionId、revision 和 generation；同一请求只可接受一次。页面在后台线程仅传入纯规则副本，主线程接受结果。弹窗、设置、悔棋、换模式、新局、离开、失焦或禁用都会撤销请求，且不阻塞主线程等待搜索线程。

`GomokuBoardGraphic` 用单个 UGUI 网格绘制棋线、棋子、预选、末手点和胜利线，四边坐标由统一字体呈现。`GomokuInput` 验证从棋盘开始的完整单指点击，拖动、多指、取消及跨弹窗旧手势不提交预选。`GomokuPage` 复用 Home / Book / Settings、公共音效与触感设置，采用常规布局，无全屏模式。

`gomoku-v1.json` 及 `.bak/.tmp` 独立保存两种模式各一局和新局偏好；32 KiB、深度 8 上限，严格检查字段、JSON token、会话身份与完整合法历史。写入临时文件并 flush 后原子替换，只有有效主档可以成为备份。主档损坏可恢复有效备份，两份都损坏时必须确认新局才能覆盖。恢复局明确暂停，终局保留只读结果，预选和后台任务不落盘。保存失败显示可重试入口，返回大厅时提示尚未保存的风险。


## Flappy Bird

- `Core/FlappyBird`：固定 60Hz 的原生 C# 规则；圆形鸟体与管身/端帽扫掠碰撞按时序结算。场地始终 360×600，UGUI 仅等比容纳。五档管道高度仅生成经构造证书验证的同高/相邻 13 条连接；证书及连续路径边界见 `Tests/FlappyBird/README.md`。
- `FlappySession`：准备、运行、死亡、结算、暂停、三秒倒数；超过 100ms 的帧触发暂停；退出或进程重启不恢复局面。结果显示 0.35 秒后才可重开。
- `Runtime/FlappyBird`：独立 UGUI 页面、单一 EventSystem 指针入口、画面/合成音效和纪录存储。场地与导航共享触点所有权；触摸释放不提交操作。公共设置由 AppController 提供，页面不复制声音/触感字段。
- `FlappySaveStore`：真实 Newtonsoft 严格解析独立 `flappy-bird-v1.json`，仅 schema/gameId/十进制字符串 bestScore；临时文件 flush 后原子替换，保存合法备份。页面区分内存最高分与已落盘分数，失败重试不改变场地尺寸。
- `AppController`：第 13 张卡片，延迟建立页面，统一 Back/进入其他游戏/后台/焦点/设置保存；大厅显示真实最高纪录与待保存状态。构造大厅只读取已加载的数据，UI 夹具不访问正式玩家存档。
