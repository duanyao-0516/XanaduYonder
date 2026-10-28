# 五子棋开发沉淀与维护规范

日期：2026-10-28。本文汇总本次产品决策、规则与算法、模块边界、输入和生命周期、存档协议、已解决问题、验证方法及交付约定，作为后续维护入口。实现以仓库源码为准；历史验证与本次整合验证分别记录，不把旧 APK 的证据用于证明新构建。

## 1. 范围、版本与交付背景

开发从 `master` 的 `8d64b5f` 创建独立工作树 `gomoku/XanaduYonder`，分支为 `p/duanyao.0516/feature/gomoku`。设计提交 `7996af1`，功能提交 `8ef6c74`，模拟器记录提交 `87abde0`。随后按用户要求整理本文，并在本工作树合入远端 `master` 的 `b6493ca`，保留其祖玛、小恐龙与横竖屏过渡修复。

整合后大厅顺序为：2048、跳一跳、羊了个羊、合成大西瓜、成语接龙、数独、扫雷、祖玛、小恐龙、**五子棋**。历史草图、模拟器截图和记录中的“第八款”对应整合前版本。正式版号仍为 `0.4.0 / versionCode 4`；提交源码到 master 不等于创建正式发布，也不自动更新手机上的测试包。

用户已确认的范围：离线人机对战、三档难度、本地同机双人、完整对局状态、各模式续局。没有联网匹配、房间、排行榜、禁手规则或全屏专注页。

## 2. 产品与视觉决策

最初比较常规游戏页和全屏专注页，用户认为差异不足，最终只保留符合现有 APP 调性的常规页。棋盘采用纸色背景、墨绿文字和点缀色，复用现有字体与控件：`Paper #F6F4EC`、`Ink #173D35`、`Accent #276653`。

顶部“返回大厅／玩法说明／设置”直接复用 `UiFactory.IconButton` 的 Home、Book、Settings，不另造一套按钮。设计坐标分别为 `(56,34)`、`(536,34)`、`(606,34)`，大小都是 `58×58`。屏幕使用 `720×1320` 基础设计框，较长屏幕通过本页 Band 分配空间；不为了单个游戏改全局 `SafeDesignFrame`。

由于提示采用 H10 等坐标，棋盘四边均标出 A–O 和 1–15。点击只预选：显示对应颜色的半透明棋子、行列高亮和坐标，用户再点“确认落子”；取消或换点不会写入棋谱。末手有标记，获胜显示连续线，终局保持只读。人机难度在页面上持续可见，保存错误也不能把难度信息挤掉。本地双人显示玩家身份和当前回合，不显示 AI 难度。

### 场景契约

| 场景 | 应有行为 |
| --- | --- |
| 初次进入／取消首个新局设置 | 保持可开始状态；不能因 `session == null` 禁用唯一入口 |
| 人机新局 | 简单／普通／困难与人类执黑／执白；确认前只改草稿，不修改旧局 |
| 人机思考 | 禁止人类确认落子；仍可暂停、悔棋或离开，并使旧结果失效 |
| 人机悔棋 | AI 应手前撤回人类一手，应手后撤两手；人类执白时保留 AI 的黑棋开局 |
| 本地双人 | 玩家 1／2 身份固定，执黑归属单独保存；回合卡随棋色切换 |
| 双人悔棋 | 需要对方同意，同意只撤一手，拒绝不改棋盘 |
| 双人再来一局 | 设置默认交换执黑玩家；取消保留原终局和归属 |
| 模式切换 | 两个模式各保留一局；恢复进行局需明确继续 |
| 暂停／后台／离开后回来 | 清除预选和在途 AI；显式继续之后才能再操作 |
| 规则／设置／临时确认框 | 阻止棋盘输入；仅在原会话和先前状态仍有效时恢复临时暂停 |
| 黑胜／白胜／长连／满盘和棋 | 显示结果，棋盘只读、不能悔棋；重启后保持终局 |
| 保存失败 | 保留内存对局，提示重试；离开时提示未保存风险 |
| 主档坏、备份好 | 恢复有效备份并提示；备份未必是上一手 |
| 主备份均坏 | 不静默覆盖；只有确认开始新局才解除坏档保护 |

完整设计见[设计说明](superpowers/specs/2026-10-28-gomoku-design.md)，实施阶段记录见[实现计划](superpowers/plans/2026-10-28-gomoku.md)。

## 3. 模块责任与数据所有权

| 层 | 源码入口 | 负责什么 |
| --- | --- | --- |
| 规则与棋谱 | [GomokuGame.cs](../Assets/Scripts/Core/Gomoku/GomokuGame.cs)、[GomokuCoordinates.cs](../Assets/Scripts/Core/Gomoku/GomokuCoordinates.cs) | 合法落子、回合、胜负、重放、坐标；无 Unity 依赖 |
| AI | [GomokuAi.cs](../Assets/Scripts/Core/Gomoku/GomokuAi.cs) | 在棋局副本上搜索，接受预算与取消令牌，返回落点及统计 |
| 会话 | [GomokuSession.cs](../Assets/Scripts/Runtime/Gomoku/GomokuSession.cs) | 唯一对局变更入口，预选、悔棋、暂停、请求身份与结果验收 |
| 输入与几何 | [GomokuInput.cs](../Assets/Scripts/Runtime/Gomoku/GomokuInput.cs)、[GomokuBoardGeometry.cs](../Assets/Scripts/Runtime/Gomoku/GomokuBoardGeometry.cs)、[GomokuPointer.cs](../Assets/Scripts/Runtime/Gomoku/GomokuPointer.cs) | 手势状态、交点映射、完整触摸帧消费 |
| 棋盘渲染 | [GomokuBoardView.cs](../Assets/Scripts/Runtime/Gomoku/GomokuBoardView.cs)、[GomokuBoardGraphic.cs](../Assets/Scripts/Runtime/Gomoku/GomokuBoardGraphic.cs) | 棋盘网格、棋子、坐标、预选和胜线；不决定规则 |
| 页面 | [GomokuPage.cs](../Assets/Scripts/Runtime/Gomoku/GomokuPage.cs) 及 Layout／Dialogs／Search 分文件 | 主线程协调 UI、会话、后台搜索、设置草稿、存档与宿主回调 |
| 存储 | [GomokuSaveData.cs](../Assets/Scripts/Runtime/Gomoku/GomokuSaveData.cs)、[GomokuSaveCodec.cs](../Assets/Scripts/Runtime/Gomoku/GomokuSaveCodec.cs)、[GomokuSaveStore.cs](../Assets/Scripts/Runtime/Gomoku/GomokuSaveStore.cs) | 严格协议验证、历史重放、原子写入及主备份恢复 |
| 应用宿主 | [AppController.cs](../Assets/Scripts/Runtime/AppController.cs) | 大厅、导航、统一设置／反馈、前后台、保存与错误聚合 |

禁止页面和后台任务各维护一份可变棋盘。`Game`、棋谱及获胜线等对外接口使用防御性副本；后台任务不得触碰 Unity 对象或正在变化的 Session。规则属于 Core，会话策略属于 Session，显示和宿主适配留在 Page，不因共同使用“暂停”概念就把不同游戏强行塞进一个基类。

## 4. 规则、坐标与棋盘几何

- 15×15，自由五子棋，黑先白后；横、竖、两条斜线任意方向 **连续至少五子**获胜，无三三／四四／长连禁手。225 点占满且无人胜才是和棋。
- 索引按画面从上到下、每行从左到右排列：`index = row * 15 + column`；列号 A–O，显示行号 `15 - row`。
- `A15 = 0`、`H10 = 82`、`H8 = 112`、`O1 = 224`；中心是 H8，不能凭视觉把 H10 当中心。
- `GomokuCoordinates.TryParse` 接受 ASCII 小写列字母，拒绝空白、前导零和越界格式。界面和存档工具复用同一转换规则。
- Core 拒绝越界、重复占点及终局后继续落子；恢复存档逐手重放，不能只检验最后一张棋盘。`ToPlay` 在终局仍按手数奇偶推导，赢家必须看 `State`。
- `WinningLine` 是首个获胜方向的完整连续线，可超过五点。Core 的 Undo 可撤销终局；产品不允许终局悔棋，由 Session 的活跃状态门控制。

渲染与命中共用 648 单位棋盘，边距 52，间距 `(648 - 2×52) / 14 = 544/14`。触点先转换到棋盘左上原点的设计坐标，再吸附最近交点；外缘允许半格容差，更远返回无效。不能用屏幕固定像素直接计算棋谱索引。

棋盘用自定义 UGUI Graphic 绘制，并配四边共 60 个坐标标签。输入表面必须有可射线命中的 Graphic，标签和装饰不拦截输入；不创建 225 个彼此竞争事件的落点按钮。

## 5. AI 方案、预算及局限

采用本地纯 C# 有界搜索，没有运行时联网、外部服务、神经网络权重或第三方棋力引擎。这样可直接共享规则与测试环境，预算和取消行为也由项目控制；难度是计算与搜索策略的区别，尚无 ELO 标定或最优性保证。

搜索流程：

1. 先校验取消；终局返回 `Index = -1`，空盘直接选择中心 H8。
2. 全盘扫描己方立即成五，再识别对方立即成五的威胁；这些战术不能被邻域或候选数裁剪漏掉。
3. 从已有棋子附近生成候选，根据活／眠／跳跃形态和攻防价值排序。
4. 困难档先用一部分共享预算尝试有限连续冲四（VCF）证明；证明成功可直接返回落点。
5. 迭代加深 Negamax，使用 alpha-beta 剪枝和有界置换表，并在需要时延伸强制防守叶节点；Zobrist 键含执子方，杀棋分值按搜索层数归一化。
6. 时间／节点预算耗尽时返回最后完整迭代或合法兜底，不提交未完成根节点迭代的中间结果；取消则抛 `OperationCanceledException`，不能当作普通超时应用兜底。

| 难度 | 时间目标 | 节点上限 | 迭代深度目标 | 非紧急候选上限 | 置换表容量 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 简单 | 100 ms | 12,000 | 2 | 8 | 4,096 |
| 普通 | 450 ms | 60,000 | 4 | 12 | 16,384 |
| 困难 | 1,200 ms | 250,000 | 8 | 16 | 32,768 |

表中时间不是设备上的硬实时上限：有限的根节点全盘即时胜防扫描在可选搜索预算外，调度和设备性能也会影响总耗时。即时胜点／必防点不受 8／12／16 个候选的裁剪。2／4／8 是迭代目标，不是所有分支的最大层数；强制防守延伸受 24 层及共享预算限制。测试搜索稳定性优先使用固定节点预算，不能要求不同主机恰好得到相同墙钟耗时或节点数。

困难档 VCF 默认深度 7，最多分配共享时间／节点预算的四分之一。攻击落子须产生立即胜点，同时检查防守方反击成五；两个不同胜点可证明下一步必胜，一个胜点则强制防守后递归。有限深度、候选选择和预算都限制完整性，不能把“没找到证明”解释成“没有杀棋”。`CompletedDepth = 0` 也可能代表中心开局、即时胜防、VCF 证明或兜底，并不等于算法未执行。

主机基准方法与历史数值见 [Tests/Gomoku/README.md](../Tests/Gomoku/README.md)。这些是 .NET 主机数据，不能作为 Android 的延迟、分配、功耗或发热结论。以后提升棋力，先补具体反例棋谱及战术测试，再调整候选、评分或预算，保留取消与主线程验收契约。

## 6. 异步请求与生命周期

`Active` 的前提是前台、未 blocked、未暂停且棋局进行中。`NeedsAi` 表示此刻应由 AI 下棋，即使已有 pending 请求也可能为真；`BeginAi` 另行拒绝重复启动。

请求在主线程创建，捕获棋盘副本与身份。应用结果时必须同时满足：

- Owner 是当前 Session，且请求对象就是当前 pending 对象；
- `SessionId`、`Revision`、`Generation` 全部一致；
- 当前仍处于活跃 AI 回合；
- 返回索引合法、为空且规则层可落子。

棋盘变化递增 Revision，清预选并使旧请求失效；取消递增 Generation 并清空 pending。即使任务已计算完但尚未被 `Update` 消费，暂停、悔棋、新局等操作仍必须令它失效。单靠取消令牌无法解决这个窗口。

Page 的 `Task.Run` 仅捕获纯规则副本、难度和令牌，`Update` 在主线程观察结果与异常。取消时先解除页面任务引用和会话请求，再取消 CTS；退休任务的续体只观察异常并在结束后释放资源，不再调用页面，不在主线程 `Wait()`。搜索故障或非法返回由 `SearchFailed` 暂停会话并提示“电脑暂时无法落子，点击下方重试”，等待用户重试，不自动循环重启。

| 事件 | 请求／预选 | 恢复策略 |
| --- | --- | --- |
| 悔棋、新局、换模式 | 旧请求失效，清预选 | 依据新会话状态决定下一次搜索 |
| 打开临时弹窗／设置 | 暂停并取消 | 只在原会话与先前活动状态仍有效时恢复 |
| App 失焦／后台 | 暂停、取消、关闭临时 UI | 回前台不自动继续 |
| 离开页面、Disable／Destroy | 取消并清理页面引用 | 重新进入按恢复状态处理 |
| 从存档恢复 | 不恢复任务或预选 | 进行局显式继续，终局保持只读 |

`SetBlocked(false)`、`SetForeground(true)` 仅解除条件，不隐含 Resume。设置期间进后台会使设置恢复上下文失效；迟到的“完成设置”回调不能复活旧对局。使用真实宿主调用链测试这些行为，不能仅手工构造 Session 状态代替。

## 7. 输入契约与触摸陷阱

输入组件拥有一次物理手势的完整生命周期。只有合法单指／鼠标点击才能预选；跨交点拖动、多指、系统取消、交互禁用和旧手势都不能落子。发生多指或取消后，等待所有触点释放，再接收新手势。

UGUI 的 `PointerUp` 不直接提交触摸结果。`GomokuPointer.LateUpdate` 收集当帧完整的原生触摸集合，包括棋盘外的触点、最终 Ended 坐标以及系统 Canceled，统一交给纯输入状态机。UGUI Down 标记棋盘对该手势的归属，不能忽略未命中棋盘的第二根手指。鼠标抬起也在统一阶段处理，并抑制触摸后的模拟鼠标事件。

编辑器鼠标点击、纯输入状态机与 Android 真正触摸分发是不同验证层。UI 自动化脚本需要根据当前布局识别按钮；人机设置和双人设置的“取消”并非固定屏幕坐标。测试点错按钮应先修夹具并重跑，不能据此改变产品代码。

## 8. 持久化协议与故障恢复

文件为 `gomoku-v1.json`，备份 `.bak`，临时文件 `.tmp`，位于本应用持久化目录。只保存模式、偏好和落子历史，胜负、执子回合、胜线由规则重放得到；不保存任务、预选、焦点或临时暂停状态。

合法示例（人机一手，本地尚未开局）：

```json
{
  "schemaVersion": 1,
  "gameId": "gomoku",
  "selectedMode": "human-vs-ai",
  "preferredDifficulty": "normal",
  "preferredHumanStone": "black",
  "preferredBlackPlayer": 1,
  "slots": [
    {
      "sessionId": "a4be38f0-b4bc-43b0-92df-8c81d8b26e72",
      "mode": "human-vs-ai",
      "difficulty": "normal",
      "humanStone": "black",
      "blackPlayer": 1,
      "moves": [112]
    },
    null
  ]
}
```

`slots[0]` 固定人机，`slots[1]` 固定本地；每槽可为空，非空模式必须匹配槽位。SessionId 必须是规范格式的非空 GUID，两槽不能重复。同机玩家归属 `blackPlayer` 为 1 或 2，不能把该偏好与人机 `humanStone` 混为一项。

解码要求：

- 严格 JSON，先做语法检查，再用真实 Json.NET 解析；拒绝其默认可接受的 JavaScript 扩展语法。
- 最大 32 KiB，同时检查字符串长度与 UTF-8 字节数，最大嵌套深度 8。
- 根与会话字段必须完整且无多余／重复字段；拒绝错误类型、字符串转整数、小数、越界数、未知枚举、非法 null 和尾随内容。
- 棋谱至多 225 手，逐手重放拒绝越界、占点及终局后续手；不能用“字段能反序列化”替代业务合法性。

写入先验证，再写 `.tmp`、`Flush(true)`，最后 Replace／Move。只有当前主档有效才将它更新为备份，不能用损坏主档覆盖好备份。**备份是前一次有效写入，不保证落后恰好一手**，因为设置、离开或重复保存也会产生写入。

读取区分 Missing／Loaded／Recovered／Invalid。不能只依赖 `File.Exists` 判定“不存在”，它可能屏蔽权限或路径错误；目录祖先实际为普通文件也不是“新用户无存档”。主档失败后尝试备份，双档失败需明确提示。

双坏档保护由 Page 的 `invalidSave` 门实现：启动、进入、离开、修改设置或取消新局都不得覆盖，只有确认新局才解除。Store 的 `TrySave` 自身并不代替这项产品确认门。写入失败保留内存棋局和失败状态，提供重试及退出提示；不改其他游戏的协议或借用其他游戏的文件名。

## 9. 宿主整合与并行工作区规范

新增游戏不只需要大厅卡片。审查 `AppController` 的 Build／Enter／Leave、返回键、`CheckEndState`、`Save`、`StopAnimation`、Pause／Focus、设置完成回调、共享错误优先级与大厅刷新整条链路。

此次 master 已有祖玛与小恐龙，整合时保留：

- 祖玛独立会话宿主、`SetZumaHostInput`、`PresentationReady`，等待横屏稳定后才展示；退出等待竖屏完成后回大厅。
- `ShowLobby` 对祖玛异步退出的保护，不能用通用 `SetActive(false)` 抢先截断。
- 小恐龙独立页面与方向选择，以及 `660fd3c` 的方向过渡修复；从五子棋进入其他游戏会先 Leave，从其他入口进入五子棋清理原页。
- 构建设置中的 `LandscapeLeft = true`、既有构建入口和全部其他游戏；五子棋本身不主动写屏幕方向。
- 大厅十款顺序及底部说明位于最后一张卡片之后，不能沿用历史“八款／最后一张小恐龙”断言。

只在本任务工作树编译和提交。其他工作树的 master／功能分支、未提交文件、Unity 进程和手机操作不能顺手调整。`Library`、`Temp`、Gradle 缓存不链接到别处；同一工作树 Unity 检查与构建串行。

master 若在其他工作树检出，在本功能分支 fetch／merge 最新 `origin/master`，处理冲突并验证后，普通 `git push origin HEAD:refs/heads/master`；不切换另一个工作树，不强推，不越过 Git 修改它正在检出的本地 master。推送后独立检查远端引用与本地 HEAD 相同。

## 10. 实际问题、修复与回归落点

| 问题 | 原因／处理 | 后续必须守住 |
| --- | --- | --- |
| 第一次取消设置后无法再开始 | 尚无 Session 时主按钮被统一禁用；区分“未开局”与“当前不可操作” | 初次进入 → 取消 → 再开始 |
| 棋盘可见却点不到 | 自绘网格不等于有 UGUI raycast 接收层；补输入表面 Graphic | 真实射线与指针事件，不只测 HitTest |
| 玩法说明文字和按钮重叠 | 对话框正文高度不足；按实际内容调整 | 短屏／长屏截图检查 |
| 保存错误时难度消失 | 状态信息相互覆盖；难度和失败提示各有位置 | 失败页仍能识别当前难度 |
| 双人换边污染人机偏好 | 人类棋色和玩家执黑归属混用 | 两模式设置交替、取消不改旧局 |
| 空盘 AI 落 H10 | 候选评分先选了非中心点；显式空盘 H8 | 三档中心索引均为 112 |
| 目录祖先是文件却判“无档” | 仅依据缺失异常会吞掉错误路径 | 实际文件系统异常测试 |
| 旧 AI 结果可能落进新局 | 取消时机和主线程消费有窗口 | 请求多重身份验收；任务已完成但未 Update 时也取消 |
| 测试看似 AI 通过却未真正应手 | 只看任务或构造状态，没等待真实主线程消费 | Task.Run → Update → 棋谱增一手 |
| 抬手／多指误预选风险 | UGUI 回调不能代表完整原生触摸帧 | LateUpdate 汇总 Ended／Canceled／棋盘外触点 |
| 模拟器流式安装中断 | ADB streaming 连接失败；改 `--no-streaming` 成功 | 显式目标，不重启共享 ADB 服务 |
| 双坏档取消测试误点确认 | 复用了另一种设置弹窗的坐标 | 根据截图修正测试，重新从坏档状态完整验证 |
| Unity QuickSearch 启动异常 | 栈位于编辑器索引内部，专项检查仍完成 | 保留原日志、区分编辑器与产品异常，不隐藏异常或谎称修复 |

最后一项历史日志为 `ArgumentOutOfRangeException`，栈含 `SearchDatabase.EnumerateAll → SearchInit.IndexationOnStartup`，无游戏代码帧。历史报告以检查器明确输出和退出码判断通过，同时保留该观察项。

## 11. 分层验证与复现方法

使用 Unity `6000.3.27f1`，Android 工具链来自同一 Editor 的 AndroidPlayer 目录；纯 C# 测试使用本机 .NET，项目保持 C# 7.3 兼容。持久化测试使用真实 Newtonsoft.Json，而非自制替代解析器。

| 验证层 | 命令／入口 | 历史结果与含义 |
| --- | --- | --- |
| 规则／算法 | `scripts/test-gomoku.sh` | 22 组；规则、合法重放、胜防、预算、VCF、取消和输入不变性 |
| 会话 | `scripts/test-gomoku-session.sh` | 12 组；回合、悔棋、暂停、重复／过期请求、终局 |
| 存档 | `scripts/test-gomoku-persistence.sh` | 14 组；严格协议、真实主备份、损坏与文件失败 |
| 输入 | `scripts/test-gomoku-input.sh` | 19 组；225 点、边角、拖动、多指、取消及禁用 |
| 五子棋 UGUI | `scripts/test-gomoku-ui.sh` | 1,064 断言；生产页面、真实相机／射线、宿主生命周期、异步 AI |
| 合集 UGUI | `scripts/test-minigame-ui.sh` | 整合前八款版本为 213 断言；十款版本以本次报告为准 |
| Android 构建 | `scripts/build-gomoku-validation.sh` | 独立 ARM64 验证包成功；编译成功不等于交互验收 |
| Android 模拟器 | [专项记录](verification-gomoku-emulator.md) | 65 条操作／存档断言、36 图；计数包含逐手检查 |
| OPPO 真机 | 用户要求安装后已交付 | 仅安装与包信息校验，无人工验收反馈 |

在仓库根目录运行四个纯逻辑脚本即可分别复现。Unity 检查需先结束本工作树已有 Editor／构建；专项脚本自动加载隔离环境，通用合集脚本应显式加载：

```bash
PROJECT_ROOT="$PWD"
source scripts/gomoku-unity-environment.sh
scripts/test-minigame-ui.sh
scripts/test-gomoku-ui.sh
```

`gomoku-unity-environment.sh` 设置本工作树 `Library/GomokuGradle` 和临时私有 ADB 监听端口，并关闭 mDNS 自动连接；这是每次 Unity 工作流的局部环境，不是全局 ADB 配置。不要为了跑测试执行 `adb kill-server` 或停止其他任务。

祖玛／小恐龙导航脚本当前内置 5039 端口；与其他工作区同时运行时，不能以为外层 source 会覆盖它。可在相同隔离环境下直接运行其 `-executeMethod`（`ZumaNavigationChecks.Run`、`DinoPageValidation.Run`），保留 graphics、不要加 `-quit`，检查退出码和对应 PASS 报告。同一工作树仍需串行，并遵守脚本的锁语义。

源码整合后的验证结果记录于本文末尾。历史专项记录里的“尚未合入其他任务／不推送／未操作真机”仅描述当时阶段，后续交付以本文为准。

## 12. Android 产物、设备与证据边界

历史验证包对应功能源码 `8ef6c74`，随后仅追加文档提交。包名 `com.duanyao.xanaduyonder.gomokutest`，应用名“Xanadu 五子棋测试”，版本 `0.4.0 / 4`、minSdk 26、targetSdk 36、ARM64。

- 历史 APK 已保留到 `artifacts/builds/gomoku-8ef6c74/XanaduYonder-0.4.0-gomoku-validation.apk`，62,858,456 字节；原构建输出路径供本次整合构建使用。
- SHA-256：`fc8e9e3e32083dffd2b7b5131cac5fbd93c49bf1867acea2428c180a8d7ea510`。
- 构建方法 `ProjectBuild.BuildAndroidGomokuValidation` 以 try/finally 恢复包名和产品名，构建后检查 `ProjectSettings/ProjectSettings.asset` 未留下修改。
- 重新构建同名文件前保留旧产物或另记新散列；不能让旧报告中的 hash 指向未经说明的新 APK。`artifacts/` 是本地证据，不作为源码提交。

用户最初明确真机由另一任务占用，故使用专用 AVD `XanaduGomokuValidationApi36`：API 36 ARM64、720×1440、2 CPU／2 GiB、SwiftShader、无窗口，数据在 `.local/gomoku-avds/`。显式指定 `emulator-5590` 并核对 qemu 和 AVD 身份，未触碰真机。5590 曾有超出推荐端口范围的警告，但本次发现／安装／运行均成功，复现可选空闲推荐端口。

模拟器验证包括真实三档 AI 和黑白先手、思考期间暂停后等待不落子、继续仅应一手、双人悔棋同意／拒绝、黑白双方胜局、换边、模式独立续局、Home／前后台、冷进程恢复，以及保存失败、备份恢复和双坏档保护。满盘和棋通过合法 225 手存档夹具恢复，未声称手点 225 次。文件故障结束后恢复原有效主备份并校验字节一致，最终局来自真实点击。证据在 `artifacts/validation/gomoku-emulator/`，含环境、事件、截图、存档和日志。

之后用户明确要求安装到已连接的 OPPO，已向 PKM110（序列号 `3B6F4NE8A66ZXTRL`）执行显式目标的 `install --no-streaming -r`，并核对 `pm path` 与 `dumpsys package` 的包名／版本。此次只安装交付供用户测试，没有代替用户进行真机游玩验收，尚未收到反馈。模拟器结果不能证明真机多指、系统取消、触感、长时功耗、发热或全机型适配。

后续设备操作先核对连接列表和目标身份，再对每条命令加 `adb -s <serial>`；使用测试包名，不能套用现有固定正式包且拒绝模拟器的 `scripts/device-fixture.py`。不得清正式包数据或重启全局 ADB。当前源码交付不自动再次安装或启动手机应用。

## 13. 后续维护与扩展检查

1. 先定位责任层及完整调用链，再改规则、会话、页面或宿主；共享改动要回归其他游戏，避免为一个 UI 状态调整全局行为。
2. 新规则（例如禁手、恰好五子）会影响合法历史、AI 胜防判断、存档重放和玩法说明，必须设计版本／迁移策略，不能只改画面提示。
3. AI 优化补反例棋谱，分别验证战术、预算、取消、过期结果与真机耗时；不引入主线程阻塞或共享可变棋盘。
4. 更改输入要同时覆盖完整触摸帧、最终坐标、第二指在棋盘外、失焦／Disable 及模拟鼠标，不只测试一次普通 tap。
5. 更改存档需审查编码、解码、合法性、原子写入、恢复和 Page 坏档门的整条链；新增字段不能绕过严格版本契约。
6. UI 同时看人机三档／黑白、本地双人、首局取消、进行局、终局、存档失败，至少覆盖短屏和长屏；顶部保持统一组件。
7. 交付区分源码测试、Unity 运行时、APK 构建、模拟器操作、真机安装及人工验收。报告记录源码状态、包名、散列和目标，不把一次成功扩大成所有层通过。
8. 推送前重新 fetch，确认远端 master 为当前提交祖先；遇到并行更新先整合及验证，正常推送，不强推。保留其他工作树和用户正在测试的应用。

进一步阅读：[总体架构](architecture.md)、[开发与构建](development.md)、[五子棋初次实现验证](verification-gomoku.md)、[模拟器验证](verification-gomoku-emulator.md)、[核心算法测试说明](../Tests/Gomoku/README.md)。

## 14. 本次 master 整合验证

本节仅记录合入 `b6493ca` 后的检查，避免与上面的历史 APK／模拟器结果混淆。

| 本次检查 | 结果 | 证据（本地，不提交产物） |
| --- | --- | --- |
| 五子棋规则／算法、会话、存档、输入 | 22／12／14／19 组全部通过，共 67 组 | `artifacts/logs/delivery-merged-test-gomoku*.log` |
| 十款合集 UI | 两个屏幕高度，共 235 项断言通过 | `artifacts/validation/minigame-ui/report.txt`、`artifacts/logs/delivery-minigame-ui.out` |
| 五子棋 UI | 两个屏幕高度及真实异步检查，共 1,062 项断言通过 | `artifacts/validation/gomoku-ui/report.txt`、`artifacts/logs/delivery-gomoku-ui.out` |
| 祖玛导航 | 9 个场景、39 项断言、0 失败 | `artifacts/logs/zuma-navigation-checks.txt`、`artifacts/logs/delivery-zuma-navigation-checks.log` |
| 小恐龙页面 | 24 个场景、543 项断言、0 失败 | `artifacts/logs/dino-page-checks.txt`、`artifacts/logs/delivery-dino-page-checks.log` |
| Android ARM64 整合构建 | `Build Finished, Result: Success.`，退出码 0，包信息校验通过 | `artifacts/logs/delivery-build-gomoku-validation.log` |
| 独立复审 | 共享宿主合并与文档事实核对完成，无阻塞发现 | 对比远端 master 保留已有链路；检查本文件源码与文档链接 |

专项 UI 的断言总数含等待 AI 的逐帧 `Require`，会随任务完成时机变化。本次 1,062 与历史 1,064 的差异对应少两次等待检查；检查器与 `87abde0` 的源码相同，两个尺寸的同步组和异步完成／取消组均输出 PASS，不能仅凭计数变化断定少了用例。

各 Unity 检查返回 0 并生成新的 PASS 报告，但日志并非无异常：仍观察到上述 QuickSearch 编辑器启动索引异常。祖玛导航额外出现 9 条 `MissingComponentException`，完整栈全部由 `ZumaNavigationChecks.Fixture.Dispose:254` 的 `DestroyImmediate` 进入 UGUI `MaskableGraphic.OnDisable → RectMask2D.RemoveClippable → CanvasRenderer.DisableRectClipping`。该检查器及祖玛生产代码与 `origin/master` 一致，未因五子棋整合修改；此处记录为本次观察到的原有测试夹具销毁期间异常，未修复并保留日志。历史报告未记录这些异常，不声称它们已被证明无害，不声称测试日志零异常，也不外推为真机行为结论。

祖玛／小恐龙验证在本工作树临时私有 ADB 环境下直接执行相同检查方法，遵守 `Temp/UnityLockfile` 检查与 `artifacts/locks/dino-build` 独占锁，串行运行。方向就绪由夹具控制，不代替 Android 真实旋转、物理按键或音频验收。本轮未向 OPPO 或模拟器发送操作。

本次整合 APK 为 `artifacts/builds/XanaduYonder-0.4.0-gomoku-validation.apk`，81,660,038 字节，SHA-256 为 `3427d56de411f7e17d6470bac19d834ff173e2273059c193348b003c12453dee`。包名仍为 `com.duanyao.xanaduyonder.gomokutest`，版本 `0.4.0 / 4`、minSdk 26、targetSdk 36、ARM64。构建后 ProjectSettings 无未提交差异。此新包只完成构建与元数据校验，未重新安装到设备；OPPO 上仍是第 12 节记录的旧功能验证包。
