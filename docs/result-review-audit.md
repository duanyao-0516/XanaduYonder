# 九款终局回看审查与截图清单

审查日期：2026-11-08。初始审查源码：`1f478a0`，以下行号对应修复前的通用底部按钮实现。范围仅数独、扫雷、推箱子、五子棋、2048、合成大西瓜、俄罗斯方块、贪吃蛇、Dino；与 `gameplay-polish.md:11`、`superpowers/specs/2026-11-08-gameplay-polish.md:11`、`superpowers/plans/2026-11-08-gameplay-polish.md:15` 一致。

数独 footer 遮挡已通过真实 UGUI 渲染复现，修复后的九款终局矩阵已重新运行并逐组看图核查。第 1 节保留修复前证据，第 2–5 节记录审查契约与覆盖清单，第 6–7 节记录独立源码复核和额外修复；本次实际执行结果及验证边界见第 8 节。历史检查计数不能替代本次截图。

## 1. 已确认问题：共享组件占用了页面未预留的底部空间

`Assets/Scripts/Runtime/ResultPresentation.cs:43–52` 在任意传入根节点的底部创建宽 `232s`、高 `64s` 的按钮，底边距 `24s`，其中 `s=min(1,W/720)`。以页面左上为原点，按钮范围为：

`x=(W−232s)/2 .. (W+232s)/2；y=H−88s .. H−24s`。

该实现没有询问页面可用区域，也没有调整已有布局；`:68` 每帧将按钮移到该根的最后一个 sibling。页面本身已正确处理安全区并不意味着底部留有空位。`UiFactory.cs:239–255` 的 `SafeDesignFrame` 负责物理安全区和缩放，不能替页面协调新增 UI 与原有内容。

| 页面 | 原有区域及证据 | 与旧按钮的交集 / 影响 |
| --- | --- | --- |
| 数独 | 宿主 `AppController.cs:146–147` 的共用 Status，`y=H−42..H−8`；`SudokuPage.cs:121–127` 的数字余量位于更上方 | 720 宽时与 Status 垂直相交 18。用户反馈对应底部文本区域，不能只挪数独数字键。宿主 Status 与页面子节点的实际绘制顺序须看截图，但两者矩形必相交。 |
| 扫雷 | `MinesweeperPage.Layout.cs:72–75,141–142`：hint `H−84..H−48`，全屏 compact status `H−42..H−8` | hint 的 36 高完整落在按钮的垂直范围内，居中文本区域被占；全屏 status 另交 18。普通与全屏都需覆盖。 |
| 推箱子 | `SokobanLayout.cs:20`：操作区顶 `H−152s`，这里 `s=W/388`；`SokobanPage.Layout.cs:44–47`：重来位于操作区 `(0,80s,166s,48s)` | W=720 时，重来绝对范围约 `x36..344,y=H−133.6..H−44.5`，与旧按钮交约 `100×43.5`。属于可用重开入口被覆盖。 |
| 五子棋 | `GomokuPage.Layout.cs:53–56`：actions 顶 `H−118`、高 62；中间暂停 `x264..456` | 与按钮交 32 高，中间暂停区域被占；宿主非空 Status 同样相交。已有确认行可直接承担回看入口，无需第二个按钮。 |
| 2048 | `AppController.cs:1117–1121` 的操作和说明在更上方；`:146–147,1145–1151` 启用底部 Status | 与非空 Status 相交 18，包含最后一手合并信息 / 保存提示；棋盘本身没有因此重排。 |
| 合成大西瓜 | `WatermelonPage.cs:114–120`：actions 顶 `H−124`、高 64，暂停 `x264..456` | 与中间暂停区域交 28 高；非空宿主 Status 也相交。目标达成回看和失败回看均使用此布局。 |
| 俄罗斯方块 | `TetrisLayout.cs:36–52`，`TetrisView.cs:231–247`：操作区与旋转按钮由 W/388 排布 | W=720 时，旧按钮与旋转点击矩形边缘相交约 `15×43.5`；左右交换后发生在另一侧。该项证明点击矩形相交，实际圆形图案遮挡程度需截图判断。 |
| 贪吃蛇 | `SnakePage.Layout.cs:52–65`：720×1320 时边界按钮 `x36..356,y1192..1288`，其中开关 `x288..340,y1224..1256` | 旧按钮 `x244..476,y1232..1296` 与边界按钮交 `112×56`，与开关本体也相交。高屏还需检查 Band 的 `.8` 高度补偿。 |
| Dino | `DinoPageLayout.cs:28–35`：横屏状态位于两操作键之间，`y=H−74..H−32` | 旧按钮完全覆盖状态框的 42 高垂直范围，中心区域相交。竖屏布局不同，不能用竖屏截图代替横屏。 |

这是同一个布局所有权错误的多个表现。修复应移除共享组件默认占据根底部的行为，保留各页面的原有棋盘、提示、保存状态和操作区。

## 2. 最小布局契约建议

页面负责提供回看入口及其位置、尺寸、可见性替换对象；共享组件只负责等待、回看状态、重新打开结算和取消陈旧展示。入口可以是页面新建的专用停靠区，也可以是已有按钮。共享组件不需要知道任何游戏类型、footer 名称或页面坐标。

建议契约应满足：

1. owner 显式绑定回看入口；没有入口时不偷偷创建根底部按钮。已有按钮可直接调用 `Reopen`。
2. Inspect 时显示入口、隐藏被替换的品牌；Waiting / Presented / Reset / 离开时恢复品牌，并恢复其原来的 active 状态。恢复必须幂等。
3. owner 在安全区 / 布局 / 横竖屏变化时更新停靠区。Dino 重建 content 时必须重新绑定新节点；不能只缓存已销毁品牌或旧父节点。
4. 控件只在 owner 确认可以展示结算时可见、可点。设置 / 帮助 / 页面弹层打开时不把按钮每帧置顶穿透弹层。
5. 点击入口只重开同一终局的弹窗，不产生游戏操作，不改终局身份，不重新结算成绩。

| 页面 | 可使用的 owner 区域 | 必须处理的变体 |
| --- | --- | --- |
| 数独 | `SudokuPage.cs:91–96` 的品牌 `(210,34,300,58)`；在其内部居中放置回看按钮 | 品牌和按钮互斥；不要在原 58 高区域放 64 高控件；选择页不得遗留入口。 |
| 扫雷普通 | `MinesweeperPage.Layout.cs:22–27` 同样的品牌框 | 全屏时 normalChrome 隐藏，不能让回看入口跟着消失。 |
| 扫雷全屏 | `MinesweeperPage.Layout.cs:39–43` 的 focus chrome，整体高 118 | 原剩余状态占 x36..316、计时占 x384..578、暂停占 x600..684，中间只有 68 宽；不能直接塞入 232 宽。由 owner 为终局重新分配左右统计与中间入口，或显式复用其终局无效操作区域；不得遮盖计时和胜负状态。 |
| 推箱子 | `SokobanPage.Layout.cs:19–28,72`，比例 `s=W/388`，品牌 `(104,12,180,32)s`；可用独立 dock `(128,6,132,44)s` | 导航高 63s，Book 从约 276.6s 开始，上述 dock 止于 260s。保留两侧导航、紧凑/高屏布局及错误提示。该页没有 Dino 式独立横屏布局，不可套用 Dino 坐标。 |
| 五子棋 | `GomokuPage.Layout.cs:48–52` 的已有确认按钮 `(56,0,430,66)`，其终局动作见 `GomokuPage.cs:73–80` | 直接复用原“查看结果”路径，去掉重复入口。等待中不应跳过 Gate；Inspect 需走 Reopen。 |
| 2048 | `AppController.cs:1095–1098` 品牌 `(226,34,278,58)` | 显示范围仅 Game2048 页面；宿主上还有其他游戏，不能污染其顶栏。 |
| 合成大西瓜 | `WatermelonPage.cs:73–75` 顶栏 Home 与 Settings 之间的空白 | 无现有品牌需要隐藏；可在 `(244,34,232,58)` 新建 owner 专用入口；目标与失败都复用。 |
| 俄罗斯方块 | `TetrisView.cs:198–205` 比例 `s=W/388`，品牌 `(108,12,172,32)s`；可用 `(128,6,132,44)s` | 由 `RefreshLayout` 更新，导航高 56s；该导航位于页面 overlay 上方（`:119–121`），入口仅 Inspect 可见。左右交换与警告条不改变停靠区。 |
| 贪吃蛇 | `SnakePage.Layout.cs:13–17` 品牌 `(210,34,300,58)` | 顶部保存错误条高 30，按钮顶 34 不相交；下拉 / 帮助 / 设置期间隐藏。 |
| Dino | `DinoPage.cs:267–276` 品牌 `(viewport.Width/2−150,34,300,58)` | 竖横屏分别布局，但 `DinoViewport.cs:22–34` 均将安全短边映射为 720；使用 viewport 中心。重建 content 后重新绑定，不能引用已销毁品牌。 |

## 3. 状态、输入和保存审查

本节只陈述源码证据。没有据此宣称已完成运行或设备验收，也没有发现独立于上述布局问题的新确定阻塞。

| 游戏 | 稳定 identity / 冻结依据 | 结算、清理及返回依据 |
| --- | --- | --- |
| 数独 | Gate 持有 session，`SudokuPage.Dialogs.cs:95–97`；`SudokuSession.cs:18,35,42–45` 在 Won/Lost 禁止计时与规则操作 | `SudokuProfile.cs:37–48` 通过已完成 ID 集合合并，重复 Save 不累计次数；StartLevel / selection Clear，Leave 禁用页；后台关闭旧弹窗并暂停（`SudokuPage.cs:136–147`）。恢复后检查暂停页及再次查看入口。 |
| 扫雷 | Gate 持有 session，`MinesweeperPage.cs:222–224`；`MinesweeperSession.cs:27–31` 区分 CanView 与 CanInteract，终局允许看但不翻格/插旗/继续计时 | `MinesweeperProfile.cs:27–51` 用 ResultRecorded 与 SessionId 防重复；旧终局恢复先注册 Gate 再 Inspect（Page:129–133）。返回顺序仍需覆盖总览→局部、全屏→普通、难度选择。 |
| 推箱子 | Gate 持有 session，`SokobanPage.Dialogs.cs:121–124` 等待棋盘动画结束；`SokobanSession.cs:14–15,30–32` 禁止终局移动与撤回 | `SokobanProfile.cs:33–43` 完成 ID 去重并更新最佳；Restart 即便复用 session 也先 Clear（Page:130–134）。后台撤销输入，终局仍冻结；返回入口与重玩是显式用户动作。 |
| 五子棋 | identity 是 session 而非 `session.Game` 的拷贝；`GomokuSession.cs:39,43` 用 Active 禁止终局落子、AI 与撤回 | `GomokuPage.cs:120–128` 保存当前模式快照，无弹窗次数计分；StartGame/RestoreSelected Clear。系统返回走既有 RequestLeave 保存失败处理（`:160–175`）；Inspect 确认行显式 Reopen（`:77–80`）。 |
| 2048 | Gate 持有 game，`AppController.cs:1238–1240`；`:1209` 在未确认胜利和 GameOver 均禁止移动 | 保存发生在真实移动后（`:1200–1203`）；Continue 显式 Clear 后 AcknowledgeWin（`:1243–1248`）。重开及大厅 Clear；宿主负责前后台/外层 modal。 |
| 合成大西瓜 | Gate 持有 game；`WatermelonPage.cs:129–130,165–180` 在未确认目标与失败均停止物理步进 | `:310–324` Suspend/Save 后注册展示；Suspend 不 Clear，因此不会每次重置等待。Continue 先 Clear 再确认目标；Restart Clear 并换 game。最佳值记录非按弹窗次数累加。 |
| 俄罗斯方块 | Gate 持有 session，`TetrisPage.cs:207–218`；`:191` 仅 Running 开启操作 | `:138–150` 用 ResultRecorded / 最大分记录。Suspend Clear 并取消触控（`:74–80`）；窗口重排也会 Suspend。Inspect 后后台/重排可重新进入等待并自动弹出结果，应在截图回归中记录其实际行为；不是旧回调跨局。Back 明确返回大厅（`:91–94`）。 |
| 贪吃蛇 | Gate 持有 session，`SnakePage.cs:238–243`；`SnakeSession.cs:29,42` 终局不接受方向/继续运行 | `SnakeProfile.cs:75–95` 使用 SessionId、ResultRecorded 和已记录集合；重开/换关 Clear；后台清输入/弹层。Inspect 系统返回明确 Reopen（Page:278）。 |
| Dino | Gate 持有 game，`DinoPage.cs:562–563`；`:400–401` 只允许 Ready/Running 输入，`:558` 先 Finished、清输入和时钟再保存展示 | `:571–578` 记录最高分、dirty 控制保存；重开/离开 Clear；横竖屏切换进入 Turning 使旧 canShow 失效。检查重建内容后同一终局仍有入口。结果系统返回到方向选择（`:569`）。 |

共享状态的等待时长为 0.8 秒；只有引用身份改变才重新进入 Waiting，Inspect 不自动跳回 Presented。不可用时等待清零，恢复首帧跳过，禁用时 Reset（`ResultPresentationState.cs:14–35`，`ResultPresentation.cs:62–77`）。更换布局契约时须保留这些语义，并清理品牌替换状态。

## 4. 完整终局种类与截图 manifest

下列 ID 覆盖真实可达的终局分支及不同文字/方向。每个 ID 至少保存 `-result.png` 与 `-inspect.png`；同一 owner 的等待和重开路径另保存 `-waiting.png` 与 `-reopened.png`。如果布局/生命周期变体改变了控件位置或重建方式，该变体也需要对应四态证据。

| 游戏 | 终局 ID / 触发分支 | 源码依据 |
| --- | --- | --- |
| 数独 | `sudoku-win-next`（关卡<500）；`sudoku-win-last`（关卡500）；`sudoku-loss`（累计三错） | `SudokuPage.Dialogs.cs:99–109` |
| 扫雷 | `mines-win`；`mines-loss` | `MinesweeperPage.cs:225–228` |
| 推箱子 | `sokoban-win-next`；`sokoban-win-last` | `SokobanPage.Dialogs.cs:126–131` |
| 五子棋 | `gomoku-ai-human-win`；`gomoku-ai-computer-win`；`gomoku-ai-draw`；`gomoku-local-player1-win`；`gomoku-local-player2-win`；`gomoku-local-draw` | `GomokuPage.Dialogs.cs:102–109` 及 ResultTitle；模型状态 BlackWon/WhiteWon/Draw |
| 2048 | `2048-goal`；`2048-loss` | `AppController.cs:1242–1251` |
| 合成大西瓜 | `watermelon-goal-theme0`；`watermelon-goal-theme1`；`watermelon-goal-theme2`；`watermelon-loss-theme0`；`watermelon-loss-theme1`；`watermelon-loss-theme2` | `WatermelonPage.cs:315–324`；主题0目标标题不同，其余主题棋盘图案不同 |
| 俄罗斯方块 | `tetris-ended` | `TetrisPage.cs:207–210` |
| 贪吃蛇 | `snake-classic-loss`；`snake-classic-full`；`snake-campaign-loss`；`snake-campaign-win-next`；`snake-campaign-win-last` | `SnakePage.Dialogs.cs:166–178` |
| Dino | `dino-portrait-result`；`dino-portrait-record`；`dino-landscape-result`；`dino-landscape-record` | `DinoPage.cs:564–569` |

当前共 31 个基础截图 ID，包含主题/方向导致的可见差异；它们并非 31 种不同物理终止原因。当前 12 个 Snake 闯关关卡不会自然满盘通关：`SnakeCatalog.cs:83` 强制目标小于剩余增长容量，`SnakeGame.cs:60–61` 在达到目标时即终止。因此不能为真实完整清单虚构 campaign 满盘截图；经典满盘已覆盖该终局展示。

除基础 ID，还应在报告中标明下列变体由哪个文件覆盖，不能只写“9款已检查”：

| 变体 | 最少需要观察的内容 |
| --- | --- |
| 数独四难度、末关、错误/提示用量 | 最长结算文案，顶栏入口、数字余量与宿主底部文本互不遮挡。 |
| 扫雷初/中/高三难度、普通/全屏、总览/局部 | 普通与全屏入口都存在；胜负状态、计时、提示/保存警告可读；缩放/平移只改视口；终局存档重新进入仍可查看结算。 |
| 推箱子短屏/高屏、通关最佳记录、保存警告 | 导航/棋盘/操作区不相交，重来可点；宽度缩放和两侧方向键布局不被新入口影响。 |
| 五子棋人机/本地、黑白方选择、和棋满盘 | 获胜连线和满盘可见；只保留一个明确入口；该按钮在 Inspect 后打开正确模式、正确胜者结果。 |
| 2048 首次目标同时无路可走 | 先目标结果，主动继续后才展示失败；两次结果中间清除旧展示；Inspect 不偷偷确认目标。 |
| 大西瓜目标后主动继续再失败 | 目标未确认期间物理位置/分数保持；确认后允许新游戏进程；后续失败重新 Gate，三个主题显示正确图案。 |
| Tetris 正常/交换左右、短屏、保存警告 | 旋转和方向控件无重叠；后台/resize后实际显示状态有截图和说明；终局分数、行数与最佳值不变。 |
| Snake 普通/困难规则、穿墙/实体边界、非末/末关 | 边界开关、锁和方向键可见；系统返回能重开结算；终局不能方向推进；明确换规则/重玩可开新局。 |
| Dino 横/竖、低分/新纪录、窗口变化 | 横屏底部状态可读；宽屏顶栏入口可见；窗口重建后无旧节点引用和缺失入口；普通结果与纪录文案正确。 |
| 全部页面的 safe area | 至少验证实际目标设备安全区、短屏及有底部系统 inset 的 frame；截图记录分辨率与有效 safe area，避免把不同坐标系当成相同尺寸。 |

## 5. 四态与交互证据验收

- Waiting：触发真实终局后截最终棋盘，结果弹窗尚未出现；等待约 0.8 秒，不能因重复检查永久重置，也不能被输入提前推进。必要动画完成条件按页面原有 owner 保留。
- Presented：完整标题、得分/时间/步数、所有操作均在安全区内；不能裁掉末关按钮、新纪录行或长文案。
- Inspecting：棋盘、终局标记、footer、警告及导航均可见；入口与任何可见内容或可点击区域不相交。单独的 RectTransform 断言需配真实渲染图。
- Reopened：点击 owner 入口后显示同一终局；连续重复三次，成绩、完成计数、SessionId/AttemptId、棋盘快照和游戏时间保持一致。
- 对 Waiting 和 Inspecting 分别尝试规则输入、暂停、帮助/设置、系统返回、重开、离开、前后台；检查旧终局回调不会出现在新局或大厅。允许明确用户选择“继续挑战”后的继续，不允许回看本身恢复规则。
- 保存失败只展示原 owner 的错误与恢复入口，不能被回看按钮覆盖；恢复保存不应增加第二次胜负计数。

交付时应将“源码已覆盖”“已渲染截图”“Unity交互验证”“设备验证”分开标记。本文件的初始审查状态只完成第一项。

## 6. owner 停靠修复后的独立源码复核

在 root 完成生产修改后，重新读取当前 diff。新增 `SetReopenSlot` / `UseExistingReopenAction` 已由全部九款绑定：数独、扫雷、推箱子、2048、大西瓜、Tetris、Snake、Dino 使用 owner slot，Gomoku 复用已有确认行。没有其他游戏新增接入。本次源码复核未发现漏接或确定的新生命周期阻塞。

- `ResultPresentation.Reset` 清除状态和回调，`ReleaseControl` 恢复原 Graphic.enabled 并禁用、销毁按钮；保留 owner 的 slot / decoration / existing-action 注册，因此重开和页面重新启用后仍能创建入口。
- 同一 slot 重复绑定直接返回，不反复销毁按钮。slot 变化先恢复旧 decoration，再保存新 decoration 的原始 enabled；回看时只隐藏被替换 Graphic，不禁用包含 slot 的父级。
- Dino 的 `ClearContent` 先禁用旧 content 再销毁；`BuildGame` 和 `BuildSelection` 在同次同步构建中调用 `BuildNavigation` 绑定新 slot。旧引用不会被当作新顶栏继续使用；Turning / Selecting 使旧终局的 canShow 为 false。
- 扫雷 `StartGame→SetupBoard→Layout(true)` 与 `ToggleFullscreen→Layout(true)` 都重新选择普通或全屏 slot。普通品牌与全屏剩余状态在解绑时恢复，按钮不再挂在被隐藏的 normalChrome 中。
- 扫雷全屏采用明确替换：`focusRemaining` 原显示“排雷成功 / 触雷了”的区域现在显示“查看结算”，计时和暂停位置不受占用。该取舍需在终局截图中清楚呈现；重新打开结算或开新局后应恢复原文案。它不是把按钮覆盖在仍绘制的文字之上。
- Tetris 在 `RefreshLayout` 中更新 slot，按钮 Stretch 到该区域；窗口变化时原有 Suspend 会清除本次回看。Sokoban 使用其 W/388 的 owner 坐标，宿主 WidthOnly 保持逻辑宽度 720。
- 共享组件不再每帧把按钮移动到页面根的最后一个 sibling；页面 overlay 的既有遮挡层级得以保留。Reopen 仍先验证 Available，再恢复装饰和打开同一结果。

以上是源码复核结果，不包含修复后的截图或运行结论。第 4、5 节的覆盖项仍须由实际执行证据关闭。

## 7. Tetris 多指三连击问题与处理

再次独立审查上轮输入改动时发现：同一方块完成两次下键轻点后，第三次下键与另一根手指的移动/旋转同时发生，旧实现仍会直落。`TetrisInput.ProcessFrame` 原来只在两个下键 contact 同时存在时清除轻点历史，其他双指组合不会清除；第三次 release 与第二指 Began 同帧时也会先完成三连击。这违反本轮规格中“多指清除三连击状态”的约定。

已在限定范围内修复：对整帧 contact 先判断是否有第二根手指，提前清除三连击历史，并使当前及本帧新建的下键 contact 失去轻点资格；不取消原有双指移动/旋转命令或按住下键软降。判定包含 release 帧，避免依赖 contact 枚举顺序。

在现有 `Tests/TetrisInput/Program.cs` 添加两项回归：另一指先抬起后，下键单独松手也不能继续旧序列；第三次松手与第二指移动/旋转/空白触点同帧时，不论枚举顺序均不直落，同时保留第二指正常命令。修改生产代码前，两项均失败（原 30 项通过）；修复后 `scripts/test-tetris-input.sh` 为 **32 passed, 0 failed**。

本次同时重新执行的纯检查：Tetris 存档 20 项、布局 764 条断言，Snake 规则/会话 42 项、存档 202 条断言，均通过。源码另追踪了交换偏好的读取/保存、Snake 下一前台帧模拟与插值同步冻结、方向队列保留、暂停/恢复不携带临时停帧，以及大厅入口和宿主前后台转发；未发现另一项确定的新行为问题。未运行 Unity 或 ADB，未对以上输入行为作设备验收声明。


## 8. 实际执行与交付证据

本轮先保留旧实现执行数独终局，得到真实失败：`Review button does not cover visible text: 四档难度 · 2,000 道离线数独 · 自动保存`。旧截图保留在 `artifacts/result-review/before/sudoku-overlap.png`；相同棋盘的修复前后对比在 `artifacts/result-review/sudoku-before-after.png`。

修复后由各页面显式预留顶栏位置，回看时替换品牌显示；五子棋复用已有“查看结果”按钮。共享组件只控制展示状态，不再占据根节点底部，也不再每帧置顶。射线检查使用实际 Button；布局检查同时覆盖可见文字、按钮点击区与棋盘，而不只检查新按钮能否点击。

### 九款完整四态截图

每组包含等待终局帧、首次结果、查看最终局面、重新打开结果，共 112 组、448 张。扫雷的全屏总览 / 返回棋盘与五子棋原棋盘截图另列为补充，不重复计入四态数量。

| 游戏 | 本轮矩阵 | 四态截图数 |
| --- | --- | ---: |
| 数独 | 四难度 × 首关胜 / 首关失败 / 第500关胜 × 两尺寸 | 96 |
| 扫雷 | 三难度 × 胜 / 败 × 三尺寸 | 72 |
| 推箱子 | 普通关 / 最后关 × 三尺寸 | 24 |
| 五子棋 | 人机 / 本地 × 黑胜 / 白胜 / 和棋 × 三尺寸 | 72 |
| 2048 | 达标 / 失败 / 达标且无路可走 / 确认达标后的失败 × 两尺寸 | 32 |
| 合成大西瓜 | 三主题 × 达标 / 失败 × 两尺寸 | 48 |
| 俄罗斯方块 | 堆顶结束 / 旧档出生受阻 × 两尺寸 | 16 |
| 贪吃蛇 | 经典两种边界的失败与满盘、闯关首末关的失败与通关 × 两尺寸 | 64 |
| Dino | 新纪录 / 普通结果 × 竖屏短高屏和横屏 | 24 |

截图来自本项目生产 UGUI 与 Camera.Render；终局通过真实规则操作或经过校验的生产快照构造。它们是编辑器渲染与交互证据，不是手机截图。可见尺寸包含 720×1320、720×1582，推箱子另含 360×640，扫雷/五子棋含将720逻辑宽渲染到360×660的物理输出；Tetris 使用388逻辑宽，Dino包含1320×720横屏。

交付目录 `artifacts/result-review/gallery/` 含中文 HTML 分类索引、原始 PNG、manifest 与文件摘要；完整压缩包为 `artifacts/result-review/result-review-gallery.zip`。索引明确区分主矩阵与黄金矿工、连连看等补充图，排除旧命名及过期截图。归档时发现 Snake 测试夹具直接恢复终局后残留开局倒计时；已通过真实零增量生产帧刷新夹具状态，新增快照不变和倒计时不可见断言，重跑并替换全部 Snake 图。没有直接隐藏 UI 节点，也没有修改游戏倒计时行为。

没有用截图数量代替可见内容检查：逐组检查了结算文案、导航、棋盘、footer 和回看入口，疑似缩略图问题另看原始图。

### 本轮执行

- Unity UI：公共小游戏/数独/2048/大西瓜 6985；扫雷 10349；五子棋 3271；推箱子 5134；贪吃蛇 8267；Tetris 279；Dino 639，均通过。相关脚本仍为 `scripts/test-*-ui.sh` 和 `scripts/test-dino-page.sh`。
- 非结算改动也重新执行：黄金矿工 UI 16358、成语接龙 UI 8527、连连看 UI 11187 均通过；对应存档迁移、固定按钮默认值、图案映射稳定性及 Snake 入口由独立审查复核。成语现有检查不输出 PNG，因此只作为运行报告，不冒充截图验收。
- Tetris 输入回归先复现多指误直落，再修复为 32 项通过；公共规则与结算状态 47 项通过。
- Android 更新测试包已构建成功：`artifacts/builds/XanaduYonder-0.9.6-debug.apk`，独立 debug 包名、ARM64、非 debuggable，无 INTERNET 权限。此包为本次修复产物，尚未安装到设备。
- iOS 隔离导出重新通过 `IOS_EXPORT_PASS`：`artifacts/ios/xcode/Unity-iPhone.xcodeproj`。本轮未进行 Xcode 原生编译、签名或安装。

王国保卫战后台音乐沿用用户已确认的真机修复；本次没有再次操作手机，也没有覆盖正式应用或存档。设备安全区沿用上一轮的检查证据，本轮新增终局矩阵未做全量真机回归。GitHub 既有 v0.9.6 Release 不随本次提交重打或覆盖。
