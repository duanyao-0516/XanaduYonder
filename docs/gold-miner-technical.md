# 黄金矿工技术契约与验收矩阵

状态：**游戏实现、资源装配、240 张参考图及真实 Core 见证、独立测试、Unity UI、隔离 APK 干净构建与包体增量核验已落地；物理手机、人工两模式十关及 Android 性能验收尚未完成。** 2026-10-27 已合并 `origin/master`，当前 HEAD 为 `b793fe8`，沿用 v0.4.0 构建版本。最终源码 APK（含最新长屏修复）大小为 94.09 MiB，相同配置干净基线增量为 34.317892 MiB，SHA-256 与方法见 §8；该干净 APK 已在 emulator-5560 通过 Android 2373×1080 商店与真实 IO 失败购买原子性复验。具体命令、产物和未覆盖项见[验证记录](verification-gold-miner.md)；本地构建不代表发布。

[首版玩法与数值方案](superpowers/specs/2026-10-27-gold-miner-design.md)记录初始设计；当前累计目标与旧档兼容以 [v3 经济调整及实测](gold-miner/economy-v3.md)和 Core 规则为准。资源、切片和音频见 [资源接入](gold-miner-assets.md)，执行顺序见 [实施计划](superpowers/plans/2026-10-27-gold-miner.md)。本文记录已实现的技术边界、状态与数据契约以及仍须满足的验收标准；金额、速度、概率、目标和物件数量从规则版本读取，不另设一份数值表。

## 1. 接入边界与现有基础

- 大厅新增一张 **07 黄金矿工** 卡，继续使用固定高度卡片和纵向滚动；卡片进入竖屏模式选择页。单人、合作各有一个独立槽位，不新建两张大厅卡。
- `Assets/Scripts/Core/GoldMiner` 为独立纯 C# 模块，不引用 Unity。运动、计时、物件认领、扫掠碰撞、商店和结算均由 Core 决定；不使用 Unity Physics/Physics2D 作为黄金矿工规则来源。
- `Assets/Scripts/Runtime/GoldMiner` 负责 Unity 输入、布局、图片、声音、方向、生命周期及存储适配，不另算一套收益或胜负。
- 接入只扩展 `AppController` 的页面枚举、卡片、进入/离开、暂停、保存、错误显示和返回键分发。既有游戏规则、存档路径和公共设置协议保持不变，不为本次接入重构全部游戏框架。
- 当前 `AppController.BuildCanvas` 使用 720×1320 设计基准，`SafeDesignFrame.Refresh` 按该基准缩放；公共 `CreateModal` 也创建竖屏安全框。黄金矿工新增自己的页面根和弹窗适配，不能仅旋转既有竖屏根节点。
- 当前 `WatermelonPointer` 是单指所有权模型；它的取消触摸检查可参考，但不能直接承担合作双指路由。`AppController` 已有暂停/失焦/离开保存入口，新增模块需要成对挂入。

## 2. 文件与职责

以下生产文件已存在于当前工作区，职责按实际源码划分。

| 路径（相对 `Assets/Scripts`） | 职责 |
| --- | --- |
| `Core/GoldMiner/GoldMinerRules.cs` | `rulesVersion=3` 的只读规则、对象目录、关卡表、价格和布局配置；内容对应玩法稿和 v3 经济调整 |
| `Core/GoldMiner/GoldMinerTypes.cs` | 模式、游戏阶段、钩状态、物件状态、命令结果和事件类型 |
| `Core/GoldMiner/GoldMinerGame.cs` | 一场挑战当前关的唯一运动状态来源；固定步推进、出钩、炸药和结算条件 |
| `Core/GoldMiner/GoldMinerCollision.cs` | 钩尖与运动物件的相对扫掠、首次接触排序、边界和独占认领 |
| `Core/GoldMiner/GoldMinerLevelGenerator.cs` | 离线有界候选生成、图案/边界检查；不在手机上生成矿图 |
| `Core/GoldMiner/GoldMinerPlanner.cs` | 离线有界规划与真实模拟见证重放；不参与玩家实时输入决策 |
| `Core/GoldMiner/GoldMinerInputState.cs` | 纯 C# 指针所有权、UI 消费、取消、generation 与恢复隔离状态机 |
| `Core/GoldMiner/GoldMinerEconomy.cs` | 购买候选、库存、玩家炸药、共享增益及金额一致性 |
| `Core/GoldMiner/GoldMinerSession.cs` | 单人/合作槽、历史纪录、检查点重试、事务候选和提交 |
| `Core/GoldMiner/GoldMinerSnapshot.cs` | 完整快照、非递归检查点、防御复制与规则一致性校验 |
| `Runtime/GoldMiner/GoldMinerPage.cs` 及 Layout/Dialogs 分部 | 页面入口、阶段 UI、生命周期 gate、保存调度、错误和事务提示 |
| `Runtime/GoldMiner/GoldMinerLayout.cs` | 横竖屏安全区、HUD/控件/矿场矩形、世界到画面等比映射 |
| `Runtime/GoldMiner/GoldMinerOrientationScope.cs` | 页面方向与全屏请求、稳定等待、退出恢复、过期请求取消 |
| `Runtime/GoldMiner/GoldMinerInputRouter.cs` | 将 Unity 指针/射线结果转为 InputState DTO，再执行合法模型命令 |
| `Runtime/GoldMiner/GoldMinerSceneView.cs` | 矿工、绞盘、钩绳、物件与反馈显示；消费模型事件，不改规则 |
| `Runtime/GoldMiner/GoldMinerArtwork.cs` | 延迟加载正式资源、切片/锚点映射、实例缓存及释放 |
| `Runtime/GoldMiner/GoldMinerAudio.cs` | 根据模型事件播放声音并遵守公共开关；暂停/离开停止 |
| `Runtime/GoldMiner/GoldMinerLevelCatalog.cs` | 校验随包目录/分片，加载当前关卡并用布局 RNG 选择已验证矿图 |
| `Runtime/GoldMiner/GoldMinerSaveCodec.cs` | 严格 JSON 编解码及跨字段验证，不默认修补非法局面 |
| `Runtime/GoldMiner/GoldMinerSaveStore.cs` | 独立文件、临时写入、原子替换、合法备份、错误分类与恢复 |

`Assets/Resources/GoldMiner/Levels` 放置按模式/关卡版本化的参考矿图、输入见证与内容散列。导入器为 `Assets/Editor/GoldMinerAssetImporter.cs`，编辑器验证入口为 `Assets/Editor/GoldMinerUiChecks.cs`，构建设置恢复及无构建验证入口为 `Assets/Editor/GoldMinerBuildSettingsScope.cs`。材质、音效和图片路径由资源文档统一管理。

## 3. 方向、全屏与布局

### 3.1 方向作用域

1. 从大厅进入黄金矿工时，保存此前请求方向、自动旋转标记和全屏状态，建立一次页面作用域。模式选择强制 `Portrait`。
2. 选择单人后使用 `Portrait`；选择合作后锁定 `LandscapeLeft`，不随握持方向自动翻转为另一侧。合作的准备、矿场、商店、说明、暂停和结果均保持横屏。
3. 方向请求期间显示准备状态，取消全部指针、停止音效，计时和运动均冻结。只有窗口宽高关系符合目标方向，且窗口尺寸与 `safeArea` 连续 3 帧稳定后才完成布局；完成布局本身不自动开始计时。
4. 方向请求 5 秒仍未稳定时显示“屏幕方向尚未就绪”及重试/返回入口，不推进游戏。每次方向请求携带递增 generation，旧请求完成回调不能覆盖较新的模式或退出操作。
5. 从合作返回模式选择先恢复竖屏；从黄金矿工离开到大厅或销毁页面时，恢复进入作用域前的方向、自动旋转标记和全屏状态。正常大厅原状态就是竖屏。恢复过程中也不推进隐藏游戏。
6. 构建仍以竖屏启动，只为运行时合作入口允许 `LandscapeLeft`；不将整个 APP 改为自动横竖屏。黄金矿工活动页请求全屏；系统临时显示导航栏时按新安全区重排，不承诺系统栏永久不可唤出。

首版通过 `Screen.orientation`、`Screen.fullScreen` 和自动旋转标记执行作用域，不增加 Android 原生插件。Unity 会随方向切换旋转触摸坐标，所以必须清除旧指针；safeArea 按当前应用窗口坐标处理，Android 15+ 不能依靠 renderOutsideSafeArea 标记代替控件避让。依据：[Unity 6000.3 Screen.orientation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-orientation.html)、[Screen.safeArea](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html)。

### 3.2 画面矩形与触控区域

- 背景层覆盖完整窗口，允许裁切或延展装饰。HUD、暂停、炸药、商店按钮、弹窗和关键说明放在 `Screen.safeArea` 内，并保留边缘间距；背景覆盖刘海不等于按钮进入刘海。
- 模式选择、单人和合作使用各自布局配置。布局基准分别采用竖屏 720×1320、横屏 1320×720；根据安全区重排，不继承公共竖屏 `SafeDesignFrame` 的缩放假设。
- 顶部 HUD、矿工地表带、底部操作控件先占位，其余矩形作为 `MineRect`。控件的最小点击框按 720 设计宽度保证至少 58×58 单位，不以图标可见笔画代替点击框。
- 规则世界宽高来自玩法稿。使用 `scale=min(MineRect.width/worldWidth, MineRect.height/worldHeight)`，完整等比显示世界；多出的区域由低对比背景延展填充，不裁掉可采集物、不非等比拉伸钩路或碰撞圆，也不因手机比例改变规则坐标。
- 世界显示矩形上边与钩轴基准对应；多余装饰区不产生物件和碰撞。HUD/炸药按钮不会覆盖规则世界内的可采集对象。安全区变化只改变映射，不重生成矿图、不重抽袋子、不修改物件位置或剩余时间。
- 左右归属按**完整游戏窗口的中线**判定，不随非对称刘海或矿场留边偏移。实际可出钩区域是地表/矿区输入面扣除 HUD、按钮、弹窗及安全区外区域；说明中的“左右半屏”不允许穿透这些 UI。
- 金额、时间、价格、道具余量、贡献和文案由实时文字显示，不能依赖带示例数值的完整效果图。完整界面稿只作布局参考，独立素材用于运行时拼装。

### 3.3 黄金矿工弹窗

暂停、说明、确认和结算由 `GoldMinerPage` 在自身方向根中显示。遮罩覆盖整窗且阻挡所有下层输入；正文与按钮限制在安全区。横屏使用横向版面，不调用会重新套入竖屏设计框的公共 `CreateModal`。声音/触感继续读写公共设置，通过黄金矿工的适配面板展示，避免为修改设置退出方向作用域。

## 4. Core 状态机与固定步

### 4.1 持久化游戏阶段

| 阶段 | 允许的动作 | 下一阶段 |
| --- | --- | --- |
| `Ready` | 查看目标/说明、开始、返回；无计时 | 开始后 `Running` |
| `Running` | 出钩、带载炸药、固定步、暂停 | 时间归零或矿场已空且钩闲置后 `Draining` |
| `Draining` | 仅推进已出发钩、动物和带载炸药；禁止新出钩 | 全部钩结束后准备一次结算事务 |
| `Won` | 查看成功结果、进入商店、返回 | 非末关进入 `Shop` |
| `Failed` | 重试本关、返回 | 检查点恢复到 `Ready` |
| `Shop` | 购买、进入下一关、返回 | 下一关完整生成并提交后 `Ready` |
| `Completed` | 查看挑战完成、返回、新挑战 | 新挑战提交后 `Ready`；没有末关商店 |

`Suspended` 是会话 gate，不替代上表的持久化阶段；它保存暂停原因与继续要求，底层仍记得是 `Running`、`Draining` 或商店等阶段。停止播放动画不等于重置钩子或完成抓取。

钩状态只有 `Swinging`、`Extending`、`RetractingEmpty`、`RetractingLoaded`。`Draining` 中 `Swinging` 钩不再摆动；已在 `Extending` 的钩仍完成本次伸出、可能抓取、再回收。动物继续移动，直到所有在途钩动作完成；暂停 gate 会同时冻结它们。

### 4.2 固定步与事件顺序

- `GoldMinerGame.Step()` 固定推进一个 1/60 秒逻辑步；没有接受任意浮点 `dt` 改变规则速度的入口。剩余时间为整数 `remainingTicks`，显示时间按玩法稿换算。
- Unity 适配层只把前台、方向稳定、无阻挡弹窗且已明确继续期间的单调经过时间累积为逻辑步。单帧最多执行 8 步，剩余累积量完整保留到后续帧，不 clamp 丢弃前台时间；积压超过一整步时暂拒新的出钩/炸药输入，补算完成后才重新接收，避免把当前输入插入旧时刻。收到暂停/后台事件时先阻止新输入并停止音效，将事件时刻之前已经累计的完整步补算到当前游戏阶段停止模拟为止，再关闭模拟 gate、丢弃不足一步的余量、保存并重设时间基点；不能直接清除尚未处理的完整前台步。恢复不追赶后台时间。
- 输入 Down 在当前模型立即提交出钩状态，下一逻辑步开始位移，不等待动画结束。模型事件先发生，视图按事件播放声音/效果；动画完成回调不能再次入账或推进关卡。
- 每步顺序：确定本步物件运动轨迹 → 建立伸出钩/物件/边界的候选接触 → 按接触先后分配物件 → 完成钩与动物位移及回收 → 提交本步入账 → 扣本步时间并检查阶段边界 → 如符合条件，返回结算候选。
- 收获在负载回到钩轴下方的停靠长度 `0.2` 时发生（与空闲钩长度相同）；该物件立即标记 `Collected`，玩家贡献与余额同次更新。命中、被抓起、播放金币动画和炸药销毁均不提前入账。
- 时间归零后的步不再扣为负数，但继续完成在途动作。本步回收带来的收入必须参加随后结算。第 10 关成功直接准备 `Completed` 事务，不能生成第 11 关或第 10 关商店。

### 4.3 碰撞与双钩仲裁

钩尖是圆，物件使用规则定义的圆。对动物用钩尖与动物在同一步内的相对运动求最早接触时间，不能只比较帧末重叠；动物在边界反向的步需按反向点拆分轨迹。绳索、两钩互相、回收中的负载均不参与再次抓取。

同一步的候选按 `(接触时间, 玩家ID, 实体ID)` 稳定排序；接触时间完全相同时 P1 优先。被认领物件立即从后续候选排除。失败方仍检测本步剩余路径上的下一件未认领物件，不能复制收入或无故停住另一钩。边界先于物件则空钩回收；物件接触与边界完全同刻时优先合法物件接触。所有计算使用有限 `double`，禁止 NaN/Infinity；相同快照、命令和规则版本应产生一致结果。

### 4.4 API 与版本隔离

| 接口 | 约定 |
| --- | --- |
| `GoldMinerGame.Step()` | 仅由已通过会话 gate 的调用方执行；返回不可变事件和可能的结算请求 |
| `TryLaunch(playerId, commandSeq)` | 仅 `Running` 且该钩 `Swinging` 时生效；无队列、不缓存忙态点击 |
| `TryUseDynamite(playerId, commandSeq)` | 仅 `Running/Draining`、带载回收且本玩家有库存时消费；其他情况不扣库存 |
| `GetSnapshot()` | 返回防御复制，数组和子对象不可反向修改进行中模型 |
| `GoldMinerSession.EnterMode/Suspend/Resume` | 选择槽位及管理继续 gate；本身不推进模拟 |
| `StartLevel()` | 只将当前 Ready 改为 Running 并标记 dirty；无重新选图/抽袋，清除开始按钮手指后才接矿场输入 |
| `PrepareNewRun/PrepareRetryLevel/PrepareEnterShop/PreparePurchase/PrepareNextLevel/PrepareSettlement` | 从当前根状态构造完整事务候选，不立即修改已发布状态；EnterShop 仅接收非末关 Won |
| `Commit(candidate)` | 只在候选持久化成功且基准版本仍相符时一次发布；重复提交无副作用 |
| `GoldMinerLevelGenerator.Generate(mode, level, pattern, variant, seed, out candidates, out expanded, expansionBudget, recordAttempt)` | 仅离线调用，按规则预算生成候选；Planner 验证成功后才可导出资源，不创建玩家会话检查点 |
| `GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason)` | 通过真实 `GoldMinerGame` 重放时刻与命令，验证取得规则要求的收入；不只检查金额总和 |
| `GoldMinerLevelCatalog.CreateStart(mode, levelIndex, runSeed, previous)` | 校验目录及当前关分片后，按稳定 ID 排序、排除前图案，用私有 RNG 副本选图并抽袋；失败不消耗会话 RNG |

`commandSeq` 与 `txnSeq` 分开：

- `commandSeq` 属于当前 `runId`，只标记实时出钩/炸药命令。所有送达且玩家合法的命令先消费递增序号，再检查忙态等条件；即使本次不生效，同一旧序号也不能稍后在空闲时生效。小于等于 `lastCommandSeq` 的命令无副作用，允许序号有间隙。
- `txnSeq` 属于整个黄金矿工存档根，仅在离散事务提交后递增；购买、重试、新挑战、换关和结算使用它判定重复提交。实时 tick 和自动保存不冒充一笔新交易。
- 两种序号到达 Int64 上限均禁止继续增加，不环绕或回退；命令序号耗尽可新开挑战，根事务序号耗尽须报告存档版本错误并保留原档。
- 事务候选携带当前 `txnSeq`、内存 `stateRevision`、活动槽位和页面 generation。同步写盘与提交之间不推进模型；任一基准改变就拒绝过期候选。
- `pageGeneration` 在新页面、切模式、重试、换关、离开和销毁时递增。输入、资源加载、方向请求、弹窗和音效完成回调都捕获 generation；旧回调只能结束自身工作，不能写入新页面或新挑战。

## 5. 双人输入与暂停 gate

### 5.1 指针所有权

- 输入面用 `pointerId → {playerId, generation, consumed}` 记录每次 Down。合作以 Down 时的窗口 X 决定玩家；跨中线移动不换玩家，Up 不再次发射。同一指针重复 Down 不重复执行命令。
- `GoldMinerInputState` 接收明确的 `Down/Up/Cancel`、pointerId、playerId（按 Down 中线预先分类）、uiConsumed、generation 和 gate 状态 DTO；返回至多一个 Launch 意图或 None，不引用 `PointerEventData`。Runtime 负责射线命中及 Unity 数据转换，炸药按钮独立调用 TryUseDynamite 并将该次 Down 标为 uiConsumed；纯 .NET 输入测试编译这份真实状态机，不能用 Unity stub 绕过生产路由。
- 同一玩家同时只接受一个仍按住的指针，额外手指忽略；两名玩家各自的指针可同时生效。忙态 Down 直接消费为无动作，抬起后必须重新点击，不自动排队到回收结束。
- UI 优先于矿区：按到炸药按钮只尝试该玩家炸药；按到暂停、HUD、弹窗或设置不发送出钩。没有“按钮处理后再把同一次触摸交给底层”的透传。
- `TouchPhase.Canceled`、原生失焦、弹窗打开、方向/安全区变更、页面禁用和离开均清除全部指针所有权。取消后的 Up 没有任何动作。
- 暂停恢复时已经按住的 finger ID 加入隔离集合，直到它们结束/取消才移除；用户必须在“继续”之后重新按下才能出钩。点击“继续”的那根手指也不能顺带释放钩子。
- 编辑器提供独立左右操作键用于规则联调；编辑器鼠标单击不能被记录为 Android 双指验收证据。

### 5.2 暂停、背景与返回

暂停条件为集合：`NotVisible`、`AppPaused`、`FocusLost`、`OrientationPending`、`BlockingModal`、`NeedsExplicitContinue`。任一条件成立都禁止 Step 和实时游戏命令；关闭一个弹窗不能误清除其他暂停原因。

- 失焦、切后台、主动暂停和阻挡弹窗立即停止游戏音效、清指针、捕获当前完整模型并尝试保存；不将伸出钩吸回、不把持物当作已入账。
- 恢复前台、关闭说明/设置、冷启动恢复活动关卡后，保持 `NeedsExplicitContinue`。点击继续只清该标记，前提是方向已稳定且其他阻挡原因已解除。
- 商店、准备、成功、失败和完成页本来不计时；从后台回来仍恢复原页面，不把它们自动转为采矿中。
- Android 返回键在矿场打开暂停，在弹窗关闭当前层，在模式选择返回大厅。离开保存完整槽位并释放页面拥有的视图/音频资源；RootSession 由 APP 生命周期持有。离开保存失败仍持续保留唯一内存局、dirty 和错误状态，重新进入不能重新 Load 旧磁盘档覆盖它；只有冷启动或显式放弃后才重新读取持久化数据。

## 6. 快照、检查点与严格恢复

### 6.1 文件与根对象

文件仅为 `Application.persistentDataPath/gold-miner-v1.json` 及同名 `.bak/.tmp`。固定 `gameId="gold-miner"`、`schemaVersion=1`，新会话使用 `rulesVersion=3`。文件名保持 v1，因 JSON 格式与字段未变。合法 `rulesVersion=1/2` 存档由 Session 在完整校验后的私有副本上升级到 3，保留双槽、余额、道具、记录、在途钩、剩余 tick、布局及检查点；标记 dirty，走原有持久化事务并保留旧 `.bak`。升级后沿用 v2 的回收速度；旧挑战的本关、重试和后续关卡全部沿用旧目标曲线，仅新开挑战采用 v3 曲线，不补发或扣减时间/资金。历史最佳纪录继续使用明确支持的旧曲线作为合法性下限。不读写其他游戏文件，不迁移第三方黄金矿工存档。

| 根字段 | 类型与语义 |
| --- | --- |
| `schemaVersion` / `rulesVersion` | JSON 整数；schema 仅支持 1，rules 支持旧档 1/2 和当前 3，未知版本仍拒绝 |
| `gameId` | 固定字符串 `gold-miner` |
| `txnSeq` | 非负 Int64 十进制字符串，最近一次已提交离散事务序号 |
| `lastMode` | `solo` 或 `coop`，仅用于默认选择展示 |
| `records` | 必须包含 `solo`、`coop`；每项含 `bestClearedLevel`、`bestRunGrossIncome`、`completionCount` |
| `slots` | 必须包含 `solo`、`coop`；每槽可以为 null，否则必须是完整合法挑战快照 |

所有金额、累计毛收入、完成次数、命令/事务序号均使用非负 Int64 的规范十进制字符串：仅 `0` 或不以 0 开头的数字序列，无符号、空白、小数、指数或溢出。玩法中的等级、库存、ID、tick 和 uint RNG 状态使用 JSON 整数，禁止浮点形式代替整数。

`records` 的最好关卡范围来自关卡表；最佳毛收入只在成功结算事务更新；完成次数只在最终成功事务增加一次。完成次数达到 Int64 上限时饱和，不能溢出为负。规则不修改旧存档字段含义，调整数值时需同步版本策略。

### 6.2 槽位与当前状态

| 槽位字段 | 类型与约束 |
| --- | --- |
| `runId` | 32 个小写十六进制字符；新挑战生成新 ID，重试本关保持不变 |
| `mode` | 必须与所在槽键一致 |
| `runSeed` | 非零 uint；仅新挑战重置 |
| `attemptIndex` | 0～Int32.MaxValue，本关重试递增；换关归零，不从检查点回退；达到上限拒绝重试事务并可开始新挑战，不能环绕 |
| `lastCommandSeq` | 非负 Int64 字符串；同一挑战重试、换关也不回退 |
| `current` | 下表的完整当前状态 |
| `checkpoint` | §6.3 的单层关卡开始检查点；不含任何 slot/current/checkpoint 嵌套 |

`current` 的所有字段均必需，只有明确标注的字段可为 null：

| 字段 | 内容与校验 |
| --- | --- |
| `phase`、`levelIndex`、`layoutProfileId` | 阶段枚举、有效关卡、与模式匹配的版本化世界布局 ID |
| `layoutId`、`catalogVersion`、`layoutContentHash` | 已选矿图的稳定 ID、目录整数版本、来源分片的 64 字符小写十六进制 SHA-256；current/checkpoint 必须一致 |
| `cash`、`target`、`shopSpent` | 金额字符串；rules 1/2 的 target 必须等于旧曲线；rules 3 的 target 必须等于该模式/关卡明确的旧或新目标，未知目标拒绝；累计消费不能超过总毛收入 |
| `remainingTicks`、`simulationTick` | 前者在 0～3600；后者在 0～`GoldMinerRules.MaximumSimulationTicks(mode)`，当前 Solo 上限 4853、Coop 上限 5346；还须满足阶段与剩余回收时间约束（§6.4），不能只检查 Int32 范围；只保存模拟步 |
| `layoutRng`、`bagRng` | 两个独立非零 uint 状态；使用玩法稿固定的 xorshift32 算法，不存在商店 RNG |
| `patternId`、`previousPatternId`、`levelLayoutSeed` | 当前/前关图案及本关布局初始随机状态；第一关前图案为 null，后续图案去重 |
| `players` | 单人 1 项、合作 2 项，playerId 唯一且按 0/1 排序；每项含 `bombs`、`totalCollected`、`levelCollected` |
| `hooks` | 与玩家一一对应；每项含 `playerId`、`state`、`angleRadians`、`swingDirection`、`length`、`heldEntityId` |
| `entities` | 完整实体数组，保留 Available/Held/Collected/Destroyed 状态，不因动画播放而删除身份；结构见下文 |
| `activeBuffs`、`pendingBuffs` | 各含 strength/clover/rockBook/diamondOil 四个严格 bool；分别为本关、下一关效果 |
| `shop` | 仅 Shop 阶段非 null，其余为 null；精确字段见下文 |
| `settlement` | 仅 Won/Failed/Completed 非 null；精确字段见下文 |

实体字段固定为 `id`、`kind`、`status`、`x`、`y`、`moveDirection`、`bagValue`、`ownerPlayerId`。kind 来自规则对象目录；坐标是有限 double。非动物 moveDirection 为 0，动物为 −1/1；非袋子 bagValue 为 null，袋子金额必须是规则允许值；仅 Held 有 ownerPlayerId，其余必须为 null。携钻动物是一个实体和一个负载，外观钻石不是第二个可独立认领实体。

半径、重量、动物速度和基础价值不重复持久化，由 kind+rulesVersion 唯一派生；最终收入由规则加 activeBuffs 计算，袋子直接使用已定的 bagValue，携钻动物按动物部分/钻石部分分别应用规则。袋子在最终选图后一次抽值，保存其结果。物件当前位置可以变化，但未被认领静态物件必须留在世界内；运动动物、钩长度和角度按规则边界校验。Collected/Destroyed 不再参与抓取。

金额一致性：`cash = sum(players.totalCollected) − shopSpent`；`levelCollected` 不超过对应 totalCollected。回收时共享现金、个人本关贡献和个人挑战毛收入原子增加同一金额。购货只增加 shopSpent 并减少 cash，不减个人贡献。

Shop 的字段固定为 `id`（runId/关卡）、`entryCash`、`entryShopSpent`、`entryBombs`（每项 playerId/bombs）、`spent`、`offers`。每个 offer 固定为 `itemId`、`price`、`remaining`、`purchased`、`allocations`；只有炸药 allocations 为按玩家排序的 `{playerId,count}` 数组，其余为 null。固定五种 offer 及价格从当前关卡规则反算校验；剩余库存与已购数之和等于初始库存。炸药分配合计等于已购数，玩家库存等于入店库存加分配；已购增益与 pendingBuffs 对应。`spent=sum(price×purchased)`、`entryCash−spent=current.cash`、`entryShopSpent+spent=current.shopSpent`，不能加载半事务。

Settlement 的字段固定为 `id`（runId/levelIndex/attemptIndex）、`outcome`（won/failed/completed）、`cash`、`target`、`contributions`（每项 playerId/amount）、`runGrossIncome`、`recordCommitted`。金额逐项等于 current/players 的值；成功结果 recordCommitted 必须 true，失败必须 false。结果与根 records 同次提交，重复打开结果只展示已存数据。

### 6.3 非递归关卡开始检查点

`checkpoint` 是专门的 `GoldMinerLevelStartState`，**不是完整 RootSnapshot 或 SlotSnapshot 的复制**。只含以下字段：

- `levelIndex`、`layoutProfileId`、`layoutId`、`catalogVersion`、`layoutContentHash`、`patternId`、`previousPatternId`、`levelLayoutSeed`；
- `openingCash`、`openingShopSpent`；
- `openingPlayers`：每位玩家的 playerId、bombs、totalCollected；本关 levelCollected 固定从 0 开始；
- `activeBuffs`、`initialHooks`、`initialEntities`；
- `layoutRng`、`bagRng`：本关布局及全部袋子金额确定后的状态。

检查点不含 `checkpoint`、`current`、`slots`、`records`、`shop`、`settlement`、commandSeq 或 txnSeq，禁止无限递归和回退幂等序号。初始实体全部 Available、无 owner；初始钩均为空闲初始状态，无负载。实例数组必须深复制，运行中的实体不能反向改写检查点。

新挑战或离店换关时，先从校验通过的当前关卡目录选择矿图，再按生效的四叶草用 bagRng 一次确定每个袋子金额，最后创建检查点并作为同一个事务保存。检查点已经包含上一商店扣款后的余额、买到的增益与各人炸药；重试只恢复这个截面，不撤销购物。冷启动恢复直接使用快照中的完整实体、袋子和 RNG，不重新选图；已支持的 catalogVersion 与合法内容标识须保留，不能因目录更新静默换图。

重试本关的候选：恢复检查点的余额、累计消费、炸药、增益、矿图、袋子金额和挑战毛收入；本关贡献归零、剩余时长回到规则初值、simulationTick 归零、phase 为 Ready。保持 runId 与 lastCommandSeq，attemptIndex 加一，txnSeq 只在提交成功后增加。禁止再次调用布局生成器、随机抽袋或上一商店购买逻辑。历史纪录保留，但失败尝试收入不会纳入新的最佳挑战毛收入。

### 6.4 大小、跨字段与版本校验

- UTF-8 文件最多 1 MiB，JSON 深度最多 12。拒绝重复键、未知键、缺字段、未声明的 null、非法 UTF-8、错误 token 类型、NaN/Infinity、越界数和尾随垃圾；解码失败不返回部分状态。
- 每根快照恰好两个槽键；每槽玩家/钩数组最多 2 项且数量由模式决定。每份 current 或 checkpoint 实体数组最多 128 项；因此两个槽连同检查点最多保存 512 项实体。商店恰好 5 项 offer；贡献/炸药分配数组最多 2 项。不保存无界事件日志、触摸队列或历史快照数组。
- 实体 ID 必须唯一。Held 实体与 RetractingLoaded 钩一一对应且 owner 相同，不能两钩持有同一实体；空钩 heldEntityId 必须为 null。被持物的位置与钩尖位置在浮点容差内一致。
- Ready 必须对应本关检查点初态，`simulationTick=0`、`remainingTicks=3600`；Running 要求剩余 tick 为正且 `simulationTick=3600−remainingTicks`。Draining/终局若时间已到，`remainingTicks=0` 且 `simulationTick≥3600`；若提前结束，则须矿场无可采集物且全部钩空闲，tick 仍与剩余时间精确对应。终局禁止在途钩；成功/失败余额与目标判定一致，Completed 只允许最后一关成功。
- 所有阶段的 `simulationTick` 均不得超过 `MaximumSimulationTicks(mode)`：规则 v2/v3 的 Solo 为 **4853**，Coop 为 **5346**，分别包含 3600 个常规 tick 和最多 1253/1746 个回收 tick。首版规则 v1 的 4150/4365 仅为历史数值。上限由世界对角线、伸出速度和最慢合法回收速度推导，伸出/回收分别向上取整并加 2 tick 浮点余量；不允许使用 `Int32.MaxValue` 作为游戏时间上限。
- 对时间已到的 Draining，恢复校验还要求 `RemainingDrainTicks(current, mode) ≤ MaximumSimulationTicks(mode)−simulationTick`。已伸出的钩按当前角度到边界的剩余伸出距离，加边界到停靠长度的最慢回收时间估算；回收钩按当前长度到停靠长度估算；各自向上取整并加 1 tick，取所有钩的最大值。即使 tick 本身未越界，也拒绝剩余额度不足以结束当前动作的存档。全部空闲的边界状态仍可合法结算。Core 用例覆盖两模式越界/巨大 tick、长钩剩余额度不足，以及最后一 tick 发射重载后合法收尾。
- 结果 ID 使用 runId、levelIndex 和 attemptIndex 组成。成功记录、Completed 的通关计数、settlement 标记及槽位阶段必须出现在同一根快照事务内。终局恢复只展示现有结果，不能再次增加纪录或通关次数。
- active/pending 增益期限必须与阶段对应；成功提交进入 Won/Completed 时清空 activeBuffs，Shop 同样不保留上一关增益。Ready/Running/Draining 的 pendingBuffs 为空，activeBuffs 来自本关检查点；Failed 保留本次增益，重试恢复同一初始值。checkpoint 始终保留本关开始的增益，不随结果页清除。
- 仅对明确支持且完成全量校验的 rules v1/v2 执行上述无损升级；其他 schema/rules 版本不支持时不猜测迁移、不按默认值读取，也不自动覆盖旧文件。提供明确提示；用户选择新挑战时先归档旧黄金矿工文件，再创建当前版本，其他游戏文件不受影响。

### 6.5 随包矿图目录与成功见证

首版已离线产出两模式×10 关×12 图，共 240 图，且全部通过同一生产 Core 见证重放；每关三种图案各 4 图。手机仅选图，不调用 Generator/Planner 或计时超时回退，因此相同种子不受设备速度影响。离线候选/搜索预算使用玩法稿；预算耗尽即生成失败，缺少任何一图或成功见证都阻止资源交付。

- 文件为 UTF-8、无压缩 JSON：`Levels/catalog.json` 以及 `solo-01.json`～`solo-10.json`、`coop-01.json`～`coop-10.json`。目录最多 64 KiB，每个分片最多 512 KiB；运行时只解码当前 mode/level 分片，离开后释放。
- catalog 根字段固定为 `schemaVersion`、`rulesVersion`、`catalogVersion`、`shards`；当前 schemaVersion/catalogVersion 为 1、rulesVersion 为 3（首版均为 1），shards 恰好 20 项，每项 `{mode,levelIndex,file,sha256}`。file 只允许上述固定文件名；sha256 对应分片原始 UTF-8 字节，不允许任意路径或联网下载。
- 分片根固定为 `schemaVersion`、`rulesVersion`、`catalogVersion`、`mode`、`levelIndex`、`layouts`；layouts 恰好 12 项，按 ID 排序，且每种图案恰好 4 项。每图字段为 `id`、`patternId`、`variantSeed`、`entities`、`witness`；id 采用 `{mode}-{两位关卡}-{patternId}-{0至3变体}`。entities 为完整初始实体，袋子先按保底金额保存，运行时选中后才覆盖一次袋子抽值。
- witness 字段为 `requiredIncome`、`inputs`、`expectedIncome`、`endTick`；每条 input 是 `{tick,playerId,action}`，action 只允许 launch（保底见证不使用炸药），按 tick/playerId 排序，同刻同玩家最多一条。输入在对应 Step 前执行，内部使用递增 commandSeq。每图最多 1024 条输入、128 个实体；结束 tick 为有限非负整数。以无增益、袋子保底值和同一生产 Core 重放，真实取得 requiredIncome 才通过。
- 加载先验证目录版本、分片大小及 SHA-256，再严格解码结构/图案/边界；坏档停留准备状态并给重试/返回，不消耗会话随机状态，不生成临时替代地图。快照的 layoutContentHash 指该图所在分片的 SHA-256，配合 layoutId 唯一标记来源。
- 运行时排除前一关图案后，以 layoutRng 从稳定排序候选中选择；首关 12 个候选，其后 8 个。选中完整矿图后再用独立 bagRng 抽值；资源加载、失败购买、查看说明、读档和重试均不额外抽取。测试覆盖固定种子集合及全部 240 条真实见证。

## 7. 持久化与 IO 失败语义

### 7.1 保存时机

出钩、炸药、回收入账后的模型标记 dirty；活动运动中每 0.8 秒尝试保存完整当前根快照，暂停/失焦/离开前同步捕获保存。`GoldMinerPage.Save` 在会话未变化且无保存错误时跳过写入，避免离开矿工后旧游戏的公共 Save 重复读取/解析/flush 矿工档；失败保留 dirty/错误，下一次保存仍会重试。金币动画是否播完不影响快照。保存的双钩和动物来自同一逻辑步边界，不能 P1 是上一帧而 P2 是下一帧。

进入商店、购买、重试、新挑战、换关和结算使用 **Prepare → 编码校验 → 写盘 → Commit**：候选完整包含现金、物件/库存、模式槽、检查点和纪录，写盘成功前不向正在显示的模型发布。失败时保留原状态，按钮可重试同一候选；不能先扣款或加通关次数，再单独保存另一半。

结算时若写盘失败，原模型保持 Draining 且无在途钩，界面展示“结算暂未保存”与重试，停止实时输入。未提交的成功结果不会先增加历史纪录。失败重试或新挑战的写盘失败也不清空当前局。完成事务后再播购买/过关反馈。

### 7.2 原子写入和恢复

- 编码必须先验证整个候选，再写 `.tmp` 并 flush，随后原子替换主文件。只有原主文件完整合法时才更新 `.bak`；不能把损坏主档覆盖合法备份。
- 正常读取优先合法主档，再尝试合法备份。恢复备份要提示；孤立 `.tmp` 不是已提交状态，不能直接续玩它。文件全部不存在时安静显示新挑战入口。
- 主档/备份都损坏或版本不支持时，保留原文件、显示问题，不自动启动并保存一个空档。用户明确选择新挑战后先归档黄金矿工旧文件，再写新根；归档失败则不覆盖。
- 连续运动的自动保存失败保留内存局与 dirty 状态，提示“当前进度尚未保存”，后续重试整个快照；不能显示保存成功。此时进程被杀只保证恢复上次已持久化截面，不能承诺恢复未写盘的动作。
- 离散事务写入失败则不 Commit，保持原界面和原模型。若底层写入结果不明，回读主档并核对候选内容散列及 txnSeq；与候选完全一致才按成功 Commit，否则继续报告失败，不能重新生成价格、袋子或新挑战种子再算一次。
- 所有存储操作在固定串行入口执行，不并发覆盖同一文件。成功后发布根状态、清除保存错误；不会修改其他模式之外的槽内容或其他游戏的文件。

## 8. 验证入口与独立安装

以下入口已实现；实际执行结果、截图和设备边界见[验证记录](verification-gold-miner.md)：

| 入口 | 责任 |
| --- | --- |
| `scripts/test-gold-miner.sh` / `Tests/GoldMiner` | 固定步、规则、结算、经济、检查点、双钩仲裁与会话事务 |
| `scripts/test-gold-miner-input.sh` / `Tests/GoldMinerInput` | 编译生产 GoldMinerInputState，测试指针路由、序号、取消和恢复隔离 |
| `scripts/test-gold-miner-persistence.sh` / `Tests/GoldMinerPersistence` | 使用生产 Codec/Store、真实临时目录的严格恢复与写失败测试 |
| `scripts/test-gold-miner-levels.sh` / `Tests/GoldMinerLevels` | 离线生成预算、240 图散列/见证、生成统计及五种 seed 的首关完整 Generate 复现 |
| `scripts/test-gold-miner-campaign.sh` / `Tests/GoldMinerCampaign` | 两模式各十关生产 Session/Store 闭环、全道具/失败/付费检查点分支与落盘冷加载 |
| `scripts/test-gold-miner-catalog.sh` / `Tests/GoldMinerCatalog` | 严格目录/分片加载、全部图选择及 2000 固定种子样本 |
| `scripts/test-gold-miner-assets.sh` / `Tests/GoldMinerAssets` | 44 PNG 原字节、43 作者切片及音频合成契约 |
| `scripts/test-gold-miner-ui.sh` / `GoldMinerUiChecks.Run` | Unity 实际布局、raycast、文字、横竖屏安全区及截图 |
| `scripts/build-gold-miner-validation.sh` / `ProjectBuild.BuildAndroidGoldMinerValidation` | 固定验证包构建，供 Android 设备集成验收 |
| Unity `-executeMethod XanaduYonder.EditorTools.GoldMinerBuildSettingsScope.RunValidation` | 不构建 APK；修改代表设置后验证正常/模拟异常退出均完整恢复 |

验证包固定为 `com.duanyao.xanaduyonder.goldminertest`；APK 输出 `artifacts/builds/XanaduYonder-0.4.0-gold-miner-validation.apk`。0.4.0 仅沿用当前构建基线，不代表本次确定发布版本。应用数据位于验证包自己的目录，不覆盖正式应用或其他分支验证包。关卡报告位于 `artifacts/validation/gold-miner/`，Unity 截图/资源样本位于 `artifacts/gold-miner-development/ui/`，构建与编辑器日志位于 `artifacts/logs/`；设备产物路径在验证记录中逐项列明。

最终源码 APK 已干净构建成功，大小 **98,662,338 bytes（94.09 MiB）**，SHA-256 为 `e3c930856b858766f9527c37c92eb9b3d59b2daabb57d45ba41177e2033f45e1`。`artifacts/logs/build-gold-miner-final-clean.log` 明确记录构建成功与 `Exiting batchmode successfully`，构建后 ProjectSettings 无 Git 差异。该干净 APK 已安装 emulator-5560，2373×1080 商店截图确认完整文字、无重叠；物理手机仍锁屏未安装。

包体对照的两个项目副本均从源码重新导入并串行构建。基线来自 `git archive b793fe8` 的 `Assets/Packages/ProjectSettings`，复用最终构建入口与设置作用域，只移除 Gold 资源导入调用；最终副本复制当前 `Assets/Packages/ProjectSettings` 和 `docs/gold-miner/production-assets-v1.json` 清单。双方只带相同 `Library/EditorUserBuildSettings.asset`，没有 Bee/Gradle 缓存或旧输出 APK；使用相同 Unity、Android 构建入口和设置，`aapt badging` 与签名证书 SHA-256 相同。干净基线为 **62,677,420 bytes**，SHA-256 `b4b64edb5b85a887b1c98d8eefb6d9f8b36892714d680c587677847a14e57cf5`；最终包增量为 **35,984,918 bytes（34.317892 MiB）**，低于 40 MiB 目标。有效压缩条目之和从 62,621,268 增至 98,595,011 bytes。

`artifacts/gold-miner-development/apk-size-comparison/compare-clean-apks.py` 生成的 `comparison-clean.json` 为 PASS：两包 CRC、无重复活动条目通过，基线 metadata 无 `GoldMinerGame` 且所有活动 Unity 数据无 `coop-10-`，最终包两者均存在；local header 间隙检查中最大 4096 bytes 均为正常 APK 签名块，非签名间隙为 0。工具不独立证明构建配置或验证签名密码学有效性，相关配置与证书证据另见验证记录。此前克隆 Library 的对照带有陈旧资源/未引用 ZIP 数据，`comparison.json` 已标为 `INVALIDATED`，其包体差值不用于验收。

最终干净包再次执行真实 IO 故障检查，以 `gold-miner-v1.json.tmp` 空目录阻断写入：给 P2 购买炸药时磁盘文件完全不变，资金 3150、库存余 3 保持不变，页面显示清晰错误条。移除空目录后重试同一按钮，txn 4→5、资金 3150→3080、P2 炸药 1→2，P1 与单人槽未变，错误消失。报告及快照为 `artifacts/gold-miner-development/emulator/final-shop-write-failure-report.json`、`final-shop-before/failed/retry.json`，截图为 `35/36/37-final*.png`；报告已绑定上述干净 APK 的 SHA-256，三张截图已刷新为该包结果。

该干净包从 Shop 进入第二关 Ready 后开始，Android 原生双指（两次 Down 相隔 10 ms）使命令 seq 60→62，单人槽完全不变；`39-final-native-dual.png` 显示 P1 已收获 500、P2 正在回收，矿场完整渲染。快照与输入日志为同一设备证据目录下的 `final-native-before/after.json`、`final-native-dual.log`。

`BuildAndroidGoldMinerValidation` 在 `Prepare()` 前捕获 Unity 已加载的 PlayerSettings、QualitySettings、EditorSettings 完整序列化快照（含平台映射、输入系统、图标/启动页、方向、graphics 和 keystore），另外捕获构建用户 5 个标志、构建场景及当前质量值。using/finally 在批处理错误退出前恢复对象内存并保存，逐项比对；某类恢复失败仍尝试其余类别。正常与模拟异常恢复已通过真实 Unity 验证。Logo 导入和 Bootstrap 场景准备属于构建资产准备，不由设置作用域回滚。

现有 `scripts/device-fixture.py` 仅允许首版 2048 的固定路径，不能直接用于黄金矿工注入。设备验证需显式序列号、确认验证包与路径，备份/恢复仅操作该验证包的黄金矿工文件。不得把正式包的安装与存档当作本次可随意覆盖的测试环境。

## 9. 验收矩阵

下表保留逐项验收标准，不能把自动检查覆盖的层次扩写为设备通过。当前证据状态如下，逐项命令和产物以[验证记录](verification-gold-miner.md)为准：

| 验证层 | 已完成证据 | 尚未覆盖的边界 |
| --- | --- | --- |
| Core / 输入 / 真实文件 IO | Core 47、Input 54、Persistence 28；完整状态/事件/入账逐 tick 散列 Solo 3651、Coop 3723；普通/四叶草各 100 万分布及暂停恢复/重试不重抽通过 | 散列精确相等不放宽浮点容差；这些独立检查不代替设备交互 |
| 关卡与运行时目录 | Levels 9579，240/240 Core 见证；Catalog 22 组、2000 固定公式种子；指定五种 seed × 两模式 × 首关三图案共 30 对完整 Generate 复现、60 次新见证通过 | 完整重复生成范围为首关；第 2～10 关通过全部生产图重放，不宣称本项重新全量生成 |
| 自动挑战闭环 | Campaign 960 检查；两模式无购买基线各十关真实 Completed；独立全道具/失败/付费检查点分支；80 次落盘事务、8 次冷加载 | 未宣称带全增益十关完成；自动输入/见证执行不计为两模式人工十关体验 |
| 生成统计 | 240 条：228 条历史记录 + 12 条字节一致重现，见 `generation-archive.json` | 重现耗时不代表历史耗时；历史 planner-only 耗时未记录 |
| 资源与 Unity UI | Assets 347；Unity UI 15,449；公共 minigame UI 205；标准屏 UGUI 与四尺寸布局夹具，新增 720×1582/1582×720 实际 Shop 截图、完整文字/不重叠检查；Running/Shop 非空错误提示位于安全区且不覆盖按钮/贡献文字；20 次进出资源采样；生产长屏布局已修复，最终干净 APK 的 2373×1080 模拟器截图复验通过 | 物理安全区/系统栏与视觉动作待验；编辑器内存样本不代表 Android 性能 |
| 旧基础回归 | 本轮 `test-core/input/persistence` 分别 43/10/13 通过；模拟器旧六游戏入口打开/返回冒烟、2048 滑动、Sudoku 开局已完成 | 不是旧六游戏完整玩法/真人验收；物理导航、生命周期和存档续局仍待验 |
| 构建设置与 APK | 真实 Unity 正常/异常恢复通过；最终源码干净 APK 成功，98,662,338 bytes（94.09 MiB），SHA-256 见 §8；构建后 ProjectSettings 无 Git 差异；该包模拟器商店/真实 IO 失败购买复验通过（见 §8） | 物理安装、帧时间与内存仍待实测 |
| 相同配置 APK 包体 | 两个无构建缓存/旧 APK 的源码副本串行构建；基线 62,677,420 bytes，最终包增量 35,984,918 bytes（34.317892 MiB），低于 40 MiB；ZIP 活动条目/类型和资源标记/CRC 核验通过，配置与证书对照一致 | 仅为当前 v0.4.0 验证构建的差值；不代表未来发布配置的包体或 Android 运行时性能 |
| 设备与人工挑战 | emulator-5560 已验证原生双指/跨屏/同侧去重、后台与进程冷启动；合作首关 Won（3150）、两笔购买/重复点击去重、第二关 Ready；坏主档+备份明确确认后归档，两个 17 字节原文件完整保留并创建新 Ready，随后恢复测试存档 | PKM110 物理手机仍锁屏未安装；物理双指、系统生命周期、两模式人工各十关待验 |

新增报告为 `artifacts/validation/gold-miner/generation-reproducibility.json` 与 `campaign.json`。Core 的逐 tick 比较同时散列完整快照、每步事件和累计入账账本；普通/四叶草分别使用固定种子的 100 万次抽样，要求每袋仅消耗一次 RNG、各金额桶频率偏差小于 0.4 个百分点。

验收报告必须记录命令/设备、输入、实际结果、产物路径和未覆盖项；没有对应证据的矩阵行不能填写“通过”。

### 9.1 Core、碰撞与经济

| ID | 场景 | 必须观察到的结果 |
| --- | --- | --- |
| C01 | 同一快照和命令序列重复重放 | 每步钩、实体、余额、RNG 和最终结果一致；视图帧率不改变结果 |
| C02 | 规则时长完整消耗 | 恰好规则规定 tick 数后禁止新出钩；显示不为负，不提前按动画结束计时 |
| C03 | 钩高速经过小钻石/移动动物 | 相对扫掠捕获真实最早接触，不因帧末无重叠穿透 |
| C04 | 动物当步触边反向 | 分段轨迹正确，动物不出界，反向前后都能发生抓取 |
| C05 | 双钩同一步抓同一物件 | 早接触者唯一获物；完全相同 P1 优先；失败钩继续剩余路径检测 |
| C06 | 抓取、带载回收、入账动画 | 抓取时不加钱，到轴时仅入账一次；后续动画回调不能再加钱 |
| C07 | 倒计时归零时仍在伸出 | 已出发整次动作完成，动物仍动，空闲钩不摆；最后收入参加结算 |
| C08 | Draining 带载使用炸药 | 仅本玩家库存减一、负载销毁且不入账；空钩/无库存不消费 |
| C09 | 提前达标/提前清场 | 达标继续采矿；无可采集物且钩全闲置可提前结算 |
| C10 | 五商品逐项购买与售罄 | 现金、库存、增益/指定玩家炸药同次提交；规则价格、上限与剩余需求一致 |
| C11 | 余额不足、炸药上限、重复事务 | 不扣钱不发货；同 txnSeq/旧候选不能重复消费 |
| C12 | 袋子恢复/重试/换玩家抓 | 同一个袋子价值不重抽；layout RNG 与 bag RNG 互不污染 |
| C13 | 下一关增益与重试 | 只影响规则指定关卡；同关重试恢复增益，不重买；成功换关后旧增益失效 |
| C14 | 失败及暂停中的重试 | 精确恢复 checkpoint，失败收入撤回、上店消费保留、地图/袋子相同、贡献归零 |
| C15 | 新挑战与另一模式 | 只重置选中槽、使用新 runId/种子；另一槽和历史纪录不变 |
| C16 | 末关成功、重复结果打开和冷启动 | 进入 Completed；无后续商店；完成次数只加一次，最佳毛收入仅成功更新 |

### 9.2 输入、方向与生命周期

| ID | 场景 | 必须观察到的结果 |
| --- | --- | --- |
| I01 | 双指同一帧分别按左右 | 两钩各出发一次，Up 不再次发射 |
| I02 | 按左后拖到右再抬起 | 始终属于 P1，不释放 P2 钩，不在 Up 重发 |
| I03 | 同侧第二指、忙态连点 | 第二指不抢占；忙态无缓存，回收后不会自动出钩 |
| I04 | 点击炸药/暂停/弹窗空白 | 只处理上层 UI；底层钩无额外动作 |
| I05 | Touch Cancel、禁用、切模式 | 所有权清除；迟到的 Up/旧 generation 回调无副作用 |
| I06 | 按住手指后后台，再恢复继续 | 时间/钩/动物不补跑；旧手指抬起后需新 Down，继续按钮不穿透 |
| I07 | 单人→选择→合作→选择→大厅 | 方向按契约切换，合作完整横屏；离开恢复原方向，其他游戏仍竖屏 |
| I08 | 方向变更延迟/失败/连续切换 | 稳定前不接收输入/计时；超时可重试返回，旧请求不覆盖新模式 |
| I09 | 短屏、长屏、左右刘海、导航栏临时出现 | 世界完整等比；按钮/文字在安全区；中线归属不偏移，关键目标不被遮挡 |
| I10 | Running/Draining/Shop/Result 分别 Home、锁屏、电话打断 | 精确保存原阶段；恢复显式继续或原静态页面；不错误完成回收/购物 |
| I11 | 准备/说明/商店停留任意时长 | 剩余 tick 不变；进入游戏后才消耗 |
| I12 | 快速退出、再进入及旧动画结束 | 不重复订阅、不重复音效/入账、不修改新挑战 |
| I13 | 低帧率和前台单帧长卡顿 | 累积前台完整 tick 不丢，积压期间不接新输入；暂停先结清完整步再清小数余量，恢复不补扣后台时间 |

### 9.3 存档与事务

| ID | 场景 | 必须观察到的结果 |
| --- | --- | --- |
| S01 | 两模式所有阶段完整 round-trip | 字段、实体归属、RNG、检查点、纪录和库存逐项一致，数组无别名 |
| S02 | 双钩分别伸出/带载时冷启动 | 还原同一步快照，先等待继续；不丢负载、不复制收入 |
| S03 | 缺字段/null/重复键/错误类型/未知版本/非有限坐标 | 整体拒绝，无默认填补或部分恢复；原文件保留 |
| S04 | 同物件双owner、非法钩/实体对应、金额不平衡 | 整体拒绝并可回退合法备份 |
| S05 | 主档损坏、合法备份、孤立tmp | 恢复合法备份并提示；不将坏主档替换进备份，不恢复未提交tmp |
| S06 | 写临时文件/flush/替换失败 | 自动保存保留内存与dirty；离散事务保持原状态，无半次扣款或清档 |
| S07 | 写入结果不明后重试 | 回读候选散列/txnSeq确认；已经提交的候选最多发布一次 |
| S08 | 成功结算后在结果页被杀进程 | 恢复结果不再次增加通关数或纪录；重试按钮不重新奖励 |
| S09 | 本关重试前后比较checkpoint与序号 | checkpoint无递归、初始矿图不变；commandSeq不回退，txnSeq只成功加一 |
| S10 | 任意金币/库存/袋子变化及失败保存 | 正式应用和其他小游戏文件散列完全不变 |

### 9.4 关卡、资源与交付

| ID | 场景 | 必须观察到的结果 |
| --- | --- | --- |
| L01 | 每模式每关样本编号 1～100、种子由固定公式派生，共 2000 次运行时选图 | 图案去重、数量/边界合法；相同种子选图/袋子值一致，手机不运行规划 |
| L02 | 全部 240 图输入见证及目录计数 | 每关每图案恰好 4 图；同一生产 Core 实际取得保底收入，不能仅凭矿物总价判定可过 |
| L03 | 离线预算耗尽、坏目录/分片、散列不符 | 离线失败阻止资源交付；运行时坏档拒绝开局且保留原状态/RNG，不随机回退 |
| U01 | 生产PNG组装的横竖屏 UI | 无示例数值烘焙、透明边缘正常、按钮文字不裁切，射线顺序符合输入契约 |
| U02 | 动物循环、钩开合、角色分层动作 | 锚点/基线一致，无动作切换跳位；动画不拥有计分/计时权 |
| U03 | 静音、暂停、离开、同时收获 | 公共开关生效，无暂停后残留循环声；两个合法收入事件不互相吞掉 |
| D01 | 独立验证 APK 安装与启动 | 包名正确、原应用可并存、六款现有游戏无明显导航/方向/存档回归 |
| D02 | 真机同时双指与系统生命周期 | 实际触摸、全屏、方向、刘海、锁屏和恢复满足 I01–I12；编辑器截图不替代本项 |
| D03 | 两模式各完成10关挑战 | 记录每关尝试/结果/剩余时间/卡点；无卡住状态或重复结算，末关完成可返回 |

验收结束必须分别报告：纯 C# 规则、真实文件 IO、Unity 布局/渲染、Android 构建、真实设备输入与生命周期、真人难度体验。现有实现和自动检查覆盖见 §9 的证据表；物理设备、Android 性能和真人完整挑战仍待验收，通过其中一层不能代替其余层。
