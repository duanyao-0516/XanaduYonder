# 数字华容道开发复盘与小游戏工程参考

更新：2026-10-28。本文整理数字华容道从确认原型、Unity 接入到 Android 实测的工程经验，供本仓库后续小游戏参考。开发基线为 `0f6e4fd691e405020da49128c3034cb808797738`，开发分支为 `p/duanyao.0516/feature/sliding_puzzle`。本文将开发阶段证据与主线集成结果分开记录，合并回归见[验证记录](verification-sliding-puzzle.md#主线集成验证)。

集成基线为远端 `master@9262b306a90a7c3180cac757829434a3c8b9db5b`，包含黄金矿工与0.6.0版本配置。合并后华容道位于第14款，保留黄金矿工第13款；下文0.5.0独立验证包、当时大厅顺序和 OPPO 安装记录仍是开发阶段事实。

玩法与字段契约见[数字华容道玩法与接入](sliding-puzzle-game.md)，执行结果、截图和设备范围见[验证记录](verification-sliding-puzzle.md)。通用 UI 方法可结合[推箱子开发复盘](sokoban-development-guide.md)，共享模块边界见[架构与存档契约](architecture.md)，环境与构建约定见[开发说明](development.md)。下文明确区分本次需求、本次实际缺陷及可复用建议，不要求其他玩法照搬同一张页面或存档模型。

## 1. 先把需求变成可以验收的状态

本次交付包含自由 3×3／4×4／5×5、三章各六题的顺序闯关、单块相邻交换、撤回、计时、两类纪录及独立续局。只画一张正常游戏页不足以覆盖这些状态；原型进入正式实现后，还必须处理当前题续局、已完成题重玩、末关完成、后台恢复和保存失败。

| 需求收敛 | 最终行为 | 对工程的影响 |
| --- | --- | --- |
| 统一合集导航 | Home 返回大厅，Book 紧邻 Settings 左侧并打开玩法弹窗，声音与触感沿用公共设置 | 复用 `UiFactory.IconButton`、公共图标和宿主委托，避免另建一套导航或设置状态 |
| 简化操作区 | 原底部玩法按钮移到顶部，撤回与重开平分操作行 | 删除重复入口后同步改布局、射线命中和帮助文案 |
| 收敛常驻说明 | 首次计时、不限时、练习、自动保存合成最下方一行；删除“一格一格”文案与原型“体验局”提示 | 文案必须反映正式存档能力，不能保留已经失效的原型承诺 |
| 三章均能游玩 | 第一章完成后进入 4 阶，第二章完成后进入 5 阶；没有占位章节 | 18 题必须都有固定身份和合法解法，选关与进度读取同一个 Profile |
| 未解锁章节可辨认 | 标题和描述为 `#A2AAA3`，右侧绘制锁图标，整行不可点击 | 锁定表现和点击门禁一起实现；灰色文字本身不是权限判断 |
| 撤回可练习 | 撤回也加一步并标记练习；练习通关仍解锁，但不参与最佳纪录 | 完成进度与纪录资格分开建模，不能用同一个布尔值替代 |
| 完成后有明确去向 | 下一题、末题返回选关、自由模式换题，已通关题可重玩 | 明确区分恢复未完成同局与重新开始已完成同题 |

这些属于用户确认的产品选择，不应都写成“修复 bug”。例如，把玩法说明移到顶部是布局调整；点击已完成当前题却重新进入旧终局，才是行为缺陷。

## 2. 原型对齐要落到实际矩形和样式

### 2.1 保留参照，区分页面与浏览器外壳

[批准的 HTML 原稿](prototypes/sliding-puzzle/approved-reference.html) 作为参照保留。实现对比使用浏览器 DOM 的实际矩形、computed style，以及 Unity 生产 UGUI 的截图、控件几何与 `GraphicRaycaster` 命中结果。原稿中的网页背景、外层手机阴影和模拟手机圆角不属于 App 内容页面。

本页以 390 CSS px 内容宽为对照基准，Unity 页面缩放为 `min(rootWidth / 390, rootHeight / 820)`。长屏按宽度保持主体比例，底注贴近底部；短屏等比缩小以保留棋盘和操作区。这是本次固定方形棋盘与控件数量下的做法，不是所有小游戏都应等比缩小的通用结论。CSS px、Unity 设计单位、屏幕像素和 Android dp 也不能直接当成同一个量。

### 2.2 共享基础能力，保留有依据的局部样式

[UiFactory](../Assets/Scripts/Runtime/UiFactory.cs) 提供纸色 `#F6F4EC`、正文 `#173D35`、强调色 `#276653`、公共字体和图标。数字华容道按原稿使用局部次字色 `#65786D`；它与公共 `Muted=#718179` 并非同一个值。字体使用 App 已入库 Noto Sans CJK；浏览器系统回退与 Unity 的字形、合成字重、抗锯齿仍有细微差异，不能据几何一致宣称逐像素相同。

以下数值用于维护本次页面，不是所有后续页面的固定模板；完整对照见[严格 UI 验证](verification-sliding-puzzle.md#严格-ui-对比)及[布局源码](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.Layout.cs)。

| 元素 | 390 基准下的坐标与样式 |
| --- | --- |
| 标题／副标题 | x=28，y=89／140.25；33 加粗／13 常规 |
| 模式切换 | (28,183.05)，334×46；外圆角10、内圆角7；文字14 |
| 上下文／尺寸选择 | y=249.05、高32；文字14加粗／12；尺寸选择圆角6 |
| 统计卡 | y=293.05，162×76.3、间距10；标签12、数值26加粗 |
| 棋盘 | (28,384.35)，334×334、圆角12；底 `#BBCBB6`、空格 `#D9E1D3`、数字块 `#F5F1D9` |
| 棋盘内部 | 内边距为宽度2.63%、间距1.97%；数字块圆角8；3／4／5阶字号36／29／24加粗 |
| 操作区 | y=735.35、高42；两个163宽按钮、间距8；圆角9、文字13 |
| 底注 | 距内容底37、高20；字号11、单行 |
| 章节卡／题卡 | 章节卡334×111.6；题卡从y=380.64开始，3列×2行，每格104.67×80 |
| 其他章节行 | y=570.64／654.04、高83.4；标题15加粗、描述12 |

禁用态也属于设计参数：本页题卡和撤回按钮使用整体透明度0.48；未解锁章节按确认需求使用灰字与锁，保持不透明。不要让 Unity 默认 disabled tint 与手工颜色、透明度再叠乘一次。对照图见[自由拼图](screenshots/sliding-puzzle-comparison-free.png)与[闯关页](screenshots/sliding-puzzle-comparison-campaign.png)。

## 3. 实际遇到的问题、根因和修复

以下问题均有本轮源码修复和验证记录；测试覆盖的其他情形不自动等于发生过同类缺陷。

| 问题 | 根因 | 本次修复与回归要点 |
| --- | --- | --- |
| 重布局后玩法、重开或通关弹窗丢失 | 页面重建直接关闭弹窗，把视图销毁当成了用户主动关闭；结果已展示标记又阻止通关弹窗重现 | `Build` 先保存 `DialogKind`，重建后按类型重新打开；结果弹窗重新进入幂等检查。覆盖三种弹窗在尺寸变化后仍可操作，不把重布局写成业务取消 |
| 点击当前已完成题仍进入旧终局 | Profile 复用“同一道题”的 session，页面没有区分未完成续局与已完成重玩 | `StartLevel` 对当前已完成同题显式传入 `restart`；未完成同题仍保留原历史与计时。完成进度及旧纪录不被重开清除 |
| 禁用卡片颜色比原稿更暗 | 按钮默认 disabled tint 与已经设置的颜色／透明度叠加 | 局部按钮 helper 将 `disabledColor` 设为白色，再用 `CanvasGroup.alpha` 表达需要的0.48；章节灰字采用独立策略。只修正本页，不全局删除所有按钮禁用表现 |
| 短屏尺寸按钮的文字挤成两行 | 按钮缩小后仍保留公共按钮标签的固定内边距，实际文字矩形比视觉按钮窄得多 | 本页紧凑按钮标签 `Stretch` 到按钮矩形。以真实短屏、3×3／4×4／5×5 标签和射线点击复核，不靠无差别缩小所有文字 |
| 恢复通知在底部溢出 | 存储层的诊断性长句直接进入单行底注，信息量超出布局 | 页面改用简短恢复结果，前台显示8秒后消退；仅当没有保存失败时倒计时。保存失败仍持续提示，不能与一次性恢复通知共用自动隐藏语义 |
| 极长用时换行 | 时间分钟位持续增长，而统计卡只有固定宽度，默认文本换行破坏信息卡布局 | `SetNumber` 强制单行，先按正常26字号测量 `preferredWidth`，必要时逐步缩小至16；游戏页和结果页共用。普通计数保留原字号，100000步不缩小 |
| Android 回前台后用时跳增 | 恢复首帧的 `Time.unscaledDeltaTime` 带入了挂起区间；此时前台门禁已经打开，session 接收了不属于前台的 delta | 页面改用可注入单调时钟差值，在进入和每次前后台回调重建基准；session 仍只接收纯时间增量。增加无后台帧的恢复回归，并重建 APK 实测 |

对应实现集中在 [Page](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.cs)、[Layout](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.Layout.cs)、[Dialogs](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.Dialogs.cs)；真实页面回归位于 [SlidingPuzzleUiChecks](../Assets/Editor/SlidingPuzzleUiChecks.cs)。

### 后台计时为什么需要单独做设备验证

纯会话测试已经证明后台 `Tick` 不累计时间，但 Android 实测仍发现用时从73.6167跳到139.5667秒。问题发生在输入给 session 的时间区间，而非它是否正确检查 `Foreground`。只测试“后台有帧且每帧被门禁拒绝”覆盖不了“后台没有帧，恢复第一帧带来大 delta”。

本次页面以 `Time.realtimeSinceStartupAsDouble` 为默认时源，允许测试注入 `Func<double>`。`Enter` 和 `SetForeground` 重新设置 `lastRealtime`；`Update` 再计算非负差值。**单调时钟本身并不会自动排除后台时间，恢复时重建基准才定义了新的前台区间。** 这属于页面与平台生命周期的衔接责任，没有把 Unity 时钟移入纯规则，也没有顺带改动其他游戏。

新增的 UI 场景先推进前台1秒，然后在不产生任何帧的情况下推进后台180秒，恢复首帧要求零跳变，再推进前台2秒。最终 APK 复验记录为：**墙钟间隔13.633秒，仅增加0.802秒前台用时**，盘面不变。失败与修复后的证据均见[Android 验证记录](verification-sliding-puzzle.md#android-构建与运行)。这是本次已执行场景，不代表所有厂商生命周期行为都已覆盖。

## 4. 状态归属明确，输入和动画才容易修复

| 层 | 本次责任 | 边界 |
| --- | --- | --- |
| [SlidingPuzzleGame](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleGame.cs) | 排列、可解性、相邻交换、移动与撤回计数、终局 | 不依赖 Unity、计时、页面或磁盘；对外数组防御复制 |
| [SlidingPuzzleSession](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSession.cs) | 本局用时、活动／前台／阻挡门禁、结果已记账状态 | 不自行读取平台时间；首次有效移动后才累计传入增量 |
| [SlidingPuzzleProfile](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleProfile.cs) | 三个自由槽、一个闯关槽、选择、连续完成前缀和纪录 | 切换前处理当前胜局；布局重建不创建新局 |
| [Page](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.cs) | 进入、选关、弹窗、计时来源、页面保存时机和生命周期接线 | 不另写移动规则，不吞并公共设置 |
| [Pointer](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePointer.cs)／[BoardView](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleBoardView.cs) | 单指仲裁、命中、表现和可取消动画 | 不决定胜负或推进第二份棋盘 |
| [Codec](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveCodec.cs)／[Store](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveStore.cs) | 外部数据校验、恢复和文件事务 | 不用 UI 默认值掩盖损坏数据 |
| [AppController](../Assets/Scripts/Runtime/AppController.cs) | 大厅路由、共享设置、保存与系统生命周期分发 | 不拥有数字华容道的解锁、成绩或生成策略 |

点击数字与朝空格滑动共用一个 pointer-up 提交通道，没有额外 `onClick` 兜底再提交一次。按下时记录触点与起始数字；松开时检查原 owner、当前门禁、取消状态、当前位置和滑动方向。多指、系统取消、切后台、弹窗和重布局都会清理尚未提交的手势。规则仍是最终合法性裁判，输入层不能因为方向看似正确就绕过相邻校验。

合法移动先提交模型并保存，再播放数字块动画。离页或布局变化可以结束动画并按模型重绘；完成结果也从模型检查，而不依赖动画回调必须执行。该原则适用于有可取消表现的玩法，但不要求所有游戏复用同一种手势、同一种动画时长或同一个页面基类。

## 5. 可解、最优与真人难度分别取证

[生成器](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleGenerator.cs) 使用稳定自定义 PRNG，从完成排列进行合法移动，最后排除已完成状态。这样给出自由题的可解性来源；打乱次数只是生成参数，不等于到终局的最短距离，也不直接定义真人难度。

[固定题库](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleCatalog.cs) 嵌入18个不同初局与解法，三个章节各六题，内容版本为 `1`。第一题依次移动5、6，两步完成。所有解法均通过生产 `SlidingPuzzleGame.TryMove` 重放，确认没有非法移动、没有提前终局后继续操作，最终确实完成。

[规则测试](../Tests/SlidingPuzzle/Program.cs) 另用独立邻接和字符串状态实现 BFS，遍历全部181,440个可解3阶状态，证明第一章最短距离为2／4／6／8／12／16。验证最短距离的算法没有调用生产生成器或生产移动函数来决定邻接，减少实现同错仍相互通过的风险。

4／5阶题目目前只有合法解法见证，不声明最优；第二、三章的真人难度仍待校准。棋盘尺寸变大、见证更长或机器搜索耗时增加，都不足以独立证明某题对玩家更难。稳定题号与内容版本属于存档契约：后续替换题面必须明确版本及旧局处理，不能在同一身份下悄悄换题。

## 6. 存档要能恢复行为，而不只是恢复图片

### 6.1 历史的含义与边界

独立文件 `sliding-puzzle-v1.json` 保存三个自由尺寸槽与一个闯关槽，包含初始排列、当前排列、当前未撤回的 tile-value 序列、步数、撤回次数、用时与记账标记。`History` 是当前仍有效的移动轨迹，不是包含每次被撤销动作的完整事件日志。固定关系为 `Moves = History.Length + 2 × UndoCount`；练习状态由 `UndoCount > 0` 推导。

恢复从初始排列逐步重放有效历史，再核对最终盘面、计数和终局。胜利后历史继续移动、不可解或重复数字、伪造当前盘面、关卡初局与目录不符都拒绝。四个槽位都检查，不能因为某槽当前未显示就跳过它。完成关卡必须是连续前缀；练习完成可以解锁而没有普通纪录。

这里连续撤回依赖完整有效轨迹，因此不能为了缩小文件随意截断历史。每局累计操作上限100,000，文件上限2 MiB、深度12；达到操作安全上限后保留局面并明确提示重玩或换题。该限制用于资源保护，没有设倒计时失败或目标步数；其他玩法若不承诺同样的撤回能力，应按自己的状态契约选择快照或事件记录。

### 6.2 严格解析与真实文件事务

[SaveCodec](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveCodec.cs) 使用实际 Newtonsoft.Json 13.0.2，并在 token 层检查固定字段、类型、schema、游戏标识和内容版本。缺失、未知、重复键、错误类型、非有限数、非法 JSON 扩展或超限文件均拒绝；允许为 null 的槽和纪录项有明确含义。编码入口也校验可变 DTO，不能只对读盘路径严格、写盘路径全信任调用方。

[SaveData](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveData.cs) 进一步核对业务一致性：零移动不能已有用时，初局必须未完成，结果记账与棋盘终局一致，未完成关卡不能已有纪录，已证明最优距离的题不能出现低于最优的成绩。严格字段检查是当前版本协议的选择；以后增加字段或迁移版本，需要显式设计兼容行为。

[SaveStore](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveStore.cs) 先写 `.tmp` 并 flush，再替换主档；只有有效旧主档可以更新 `.bak`。主档加载后仍可能被外部改坏，因此保存前通过文件指纹检查变化，变化后重新校验，防止损坏主档覆盖有效备份。读取有字节上限和严格 UTF-8 解码，恢复优先主档再备份，异常文件另行归档。

保存失败保留内存状态并持续显示未保存；后续写成功才清除。一次性“已从备份恢复”通知与“当前进度还没保存”的风险不同，前者本次采用8秒通知，后者不能按这个时限自动消失。持久化测试应使用真实临时目录，同时验证坏档、外部改坏、备份恢复和失败写入不破坏旧主档，而非只 mock 一个保存成功返回值。

### 6.3 两个最佳指标必须各自保存真实成绩

最少步数按 `(步数, 用时)` 排序，最快用时按 `(用时, 步数)` 排序，两个记录都保留完整的一局元组。例如两次成绩为 `(20步,30秒)` 和 `(30步,10秒)`，记录分别保留这两次成绩，不生成并不存在的 `(20步,10秒)`。同值使用另一维作 tie-break，相同元组不重复刷新。

使用过撤回的胜局不进入这两个纪录，但可以推进闯关完成前缀。`ResultRecorded` 让显式结算、切模式时自动结算、反复保存和冷启动恢复保持幂等。保存“已完成”与保存“最佳成绩”不是同一个业务判断；将二者合并会误伤练习解锁或造成重复记账。

## 7. 工作树、构建工具与设备各自隔离

本轮在独立工作树开发，保留原目录的分支与未提交工作。仓库内容隔离还不够：Unity 批处理退出可能操作 Gradle／ADB 服务，构建和设备检查也要避免影响并行任务。

[专用环境脚本](../scripts/sliding-puzzle-unity-environment.sh) 将 `GRADLE_USER_HOME` 放在当前工作树的 `Library/SlidingPuzzleGradle`，为 Unity 使用的 ADB 选择独立本地端口，并关闭 mDNS 自动连接。UI 检查和 Android 构建在同一工作树串行执行；不通过共享 `Library` 缓存或修改全局配置来绕过这些边界。

验证包使用 `com.duanyao.xanaduyonder.slidingpuzzletest`，与正式应用数据分开；专用 Android36 ARM64 模拟器为 `XanaduSlidingPuzzleValidationApi36`／`emulator-5564`。安装和启动明确指定目标设备，不能把“存在一个已连接设备”视为允许覆盖任意设备上的应用。

[构建脚本](../scripts/build-sliding-puzzle-validation.sh) 读取 `ProjectBuild.Version`，本次构建前清理对应旧产物，要求成功日志指向同一 APK，再输出 SHA-256。验证时同时记录包名、版本、大小、哈希与实际执行场景，避免只检查目录里存在一个旧包。实际最终包信息保留在[验证记录](verification-sliding-puzzle.md)，本文不重复维护一份容易过期的哈希清单。

主线集成时仍应重新核对真实远端 `master`、最新游戏数量与序号、宿主进入／离开／保存／生命周期接线，并按合并后的变化运行相关检查。数字华容道在开发阶段0.5.0验证包中的第13款位置，不应作为主线的永久编号；独立验证 APK 内容与正式 Release 内容也不能互相替代。实际集成检查见[主线集成验证](verification-sliding-puzzle.md#主线集成验证)。

本次集成还发现黄金矿工 UI 夹具仍只列七款游戏，断言矿工应是第7张卡；最新主线实际已经是第13张。失败源于旧测试清单，应按真实十四款清单补齐后再运行，不能为了让测试通过而改乱已发布入口顺序。共享 UI 清单也同步覆盖黄金矿工与华容道，并检查最后一张卡之后的底注。新增游戏时需同时检索宿主与各游戏夹具中的历史序号，区分生产行为错误与测试预期过期。

## 8. 验证结论按层次记录

以下是2026-10-28[开发验证记录](verification-sliding-puzzle.md)中的执行结果。断言数量不是独立场景数量，也不是后续任何修改都自动有效的通行证。

| 层次 | 本轮证据 | 能说明的范围 |
| --- | --- | --- |
| 规则与题库 | `scripts/test-sliding-puzzle.sh`：15组通过 | 合法交换、撤回计步、终局、复制、恢复、18题解法及第一章 BFS |
| 会话与存档 | `scripts/test-sliding-puzzle-persistence.sh`：18组通过 | 时间门禁、四槽续局、练习与解锁、双元组、严格 JSON、最长历史与真实文件事务 |
| 生产 UGUI | `scripts/test-sliding-puzzle-ui.sh`：4716项断言通过 | 720×1150／1320／1514／1582、实际渲染和射线、输入、弹窗、重排、极端计数、宿主接线与无后台帧计时回归 |
| 共享 UI | `scripts/test-minigame-ui.sh`：251项断言通过 | 当前夹具覆盖的大厅顺序、跳一跳、数独、图标、旸了个旸等共享回归，不等于全部旧玩法验收 |
| 基础规则／输入 | `scripts/test-core.sh`：43项；`scripts/test-input.sh`：10项通过 | 现有基础规则与输入检查；基础规则项目本轮修正了递归误包含其他模块 JSON 源码的编译范围 |
| Android 构建 | 独立包 IL2CPP／ARM64／Development／StrictMode 构建成功 | 本次源码能够生成对应产物，仍需安装与运行检查 |
| Android 模拟器 | 真实点击和滑动、规则与设置、2步通关、解锁、撤回、实际磁盘保存、强制结束后冷启动、后台计时修复复验 | 首次完整流程在前一个构建执行；最终包再确认安装、冷启动、原档恢复及后台计时修复，不能写成最终包重跑了全部流程 |
| OPPO PKM110 | 最终哈希的独立包安装成功，包管理器信息和冷启动成功 | 仅安装与启动确认，不代表完成实体手机交互验收 |

UI 批处理曾出现 `UnityEditor.Search.SearchDatabase` 的 QuickSearch 索引内部异常，最终检查报告仍成功；原日志保留，不把这次运行写成“零告警”。Android 日志未发现本轮游戏异常、致命异常或保存失败，Development 包的 `il2cpp.usym` 调试查询也与游戏异常分开记录。

**尚未验收：实体手机多指与系统取消、音效听感、触感、不同厂商安全区及低端性能。** 第二、三章真人难度也待校准。桌面 UGUI 多指模拟、模拟器真实手势和 OPPO 安装记录各有价值，但不能叠加成没有执行过的完整实体手机验收。

## 9. 后续小游戏可复用的检查清单

### 需求与 UI

- [ ] 共享颜色、字体、图标和导航语义与合集一致；局部差异有原型或需求依据。
- [ ] 原型、用户调整和当前行为分别清楚，已删除的入口、文案与占位状态不残留。
- [ ] 新玩家、续局、同题重玩、末题、恢复通知、保存失败都有明确路径。
- [ ] 采用相同内容宽和局面的真实截图对照，并测量坐标、字号、颜色、圆角、间距和实际命中。
- [ ] 短屏、最大计数、最长文案、disabled tint、标签内边距与字体差异单独检查。
- [ ] 重布局保留当前业务状态；页面重建不等同于用户关闭弹窗。

### 规则、输入与时钟

- [ ] 状态只有明确所有者，页面、动画和规则没有各自推进一份模型。
- [ ] 点击与滑动只经一个提交通道，多指、取消、弹窗、重排和后台不会补发旧操作。
- [ ] 模型已提交的结果不依赖动画必须播完，重复终局检查不重复结算。
- [ ] 区分首次有效动作前、已开始后、练习、暂停和终局的计时语义。
- [ ] 平台恢复检查包含“后台没有帧”的场景；使用单调时钟时也明确哪些生命周期边界需要重建基准。
- [ ] 可解性、最优性与真人难度分别给证据；固定内容身份和版本稳定。

### 存档与交付

- [ ] 使用真实解析器验证缺失／null／类型／重复键／版本／大小／深度，并检查业务状态一致性。
- [ ] 需要重放的玩法保存完整有效轨迹；所有槽位都验证，资源保护不静默丢数据。
- [ ] 最佳指标保存真实成绩元组，辅助行为与完成进度、纪录资格分别判断。
- [ ] 真实临时目录验证原子替换、坏档归档、有效备份保护和失败写入。
- [ ] 工作树、Unity 缓存、Gradle、必要的 ADB 服务与测试包按本次并行环境隔离。
- [ ] 构建方法、版本、产物路径、成功日志与哈希对应同一次运行；安装明确目标设备。
- [ ] 源码审查、独立测试、UGUI、构建、模拟器和实体手机结果分别陈述，未执行项明确保留。
- [ ] 主线合并后按实际差异重跑相关检查，更新文档与交付结果，不把开发阶段结论直接改写成主线验收。

## 10. 维护入口

| 要维护的内容 | 先读哪里 |
| --- | --- |
| 当前玩法与数据定义 | [玩法与接入](sliding-puzzle-game.md)、[架构与存档契约](architecture.md) |
| 原型与视觉证据 | [批准原稿](prototypes/sliding-puzzle/approved-reference.html)、[严格 UI 对照](verification-sliding-puzzle.md#严格-ui-对比) |
| 主题、图标和宿主 | [UiFactory](../Assets/Scripts/Runtime/UiFactory.cs)、[AppController](../Assets/Scripts/Runtime/AppController.cs) |
| 页面、布局、弹窗、时钟 | [Page](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.cs)、[Layout](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.Layout.cs)、[Dialogs](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzlePage.Dialogs.cs) |
| 规则、生成、固定题库 | [Game](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleGame.cs)、[Generator](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleGenerator.cs)、[Catalog](../Assets/Scripts/Core/SlidingPuzzle/SlidingPuzzleCatalog.cs) |
| 解析、边界与文件事务 | [SaveData](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveData.cs)、[SaveCodec](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveCodec.cs)、[SaveStore](../Assets/Scripts/Runtime/SlidingPuzzle/SlidingPuzzleSaveStore.cs) |
| 自动化回归 | [规则测试](../Tests/SlidingPuzzle/Program.cs)、[会话与存档测试](../Tests/SlidingPuzzlePersistence/Program.cs)、[生产 UI 检查](../Assets/Editor/SlidingPuzzleUiChecks.cs) |
| 隔离构建与实际验收 | [环境脚本](../scripts/sliding-puzzle-unity-environment.sh)、[构建脚本](../scripts/build-sliding-puzzle-validation.sh)、[验证记录](verification-sliding-puzzle.md) |

复盘、原型、源码与可分享截图保存在仓库；原始日志、临时文件、APK 和设备检查产物继续放在既有忽略目录 `artifacts/`，不将玩家存档、设备私有数据、签名或凭据纳入知识文档。
