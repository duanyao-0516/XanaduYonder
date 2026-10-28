# 扫雷开发经验与维护指南

整理日期：2026-10-28。依据已合入并推送 master 的源码提交 `8d64b5f72c0f45c3c16e29844ee010608ab81b9e`，记录这次开发中可以复用的实现约定、问题根因与验证方法。后续修改以实际源码和最新需求为准。

完整产品规则见[扫雷设计](superpowers/specs/2026-10-27-minesweeper-design.md)，逐轮执行结果见[验证记录](verification-minesweeper.md)，工程环境见[开发说明](development.md)。本文用于帮助维护者找到正确的修改位置，不重复整份需求或测试日志。

## 1. 修改前先确定责任边界

扫雷沿用现有应用的大厅、设置、主题、安全区与页面切换方式，规则、输入数学和存档仍独立拥有。新游戏接入不需要重写公共框架。

| 需求或问题 | 首先阅读的入口 | 责任边界 |
| --- | --- | --- |
| 布雷、翻格、数字展开、胜负 | [MinesweeperGame](../Assets/Scripts/Core/Minesweeper/MinesweeperGame.cs)、[Rules](../Assets/Scripts/Core/Minesweeper/MinesweeperRules.cs)、[Snapshot](../Assets/Scripts/Core/Minesweeper/MinesweeperSnapshot.cs) | 纯规则，不依赖 Unity，不负责声音或磁盘 |
| 计时、暂停、前后台、重复结算 | [Session](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSession.cs)、[Profile](../Assets/Scripts/Runtime/Minesweeper/MinesweeperProfile.cs) | Session 控制当前局；Profile 管理三档对局与累计纪录 |
| 点击、长按、多指冲突 | [Pointer](../Assets/Scripts/Runtime/Minesweeper/MinesweeperPointer.cs)、[Gesture](../Assets/Scripts/Runtime/Minesweeper/MinesweeperGesture.cs) | Pointer 收集真实输入；Gesture 统一仲裁，不直接修改棋盘 |
| 缩放、平移、命中、总览定位 | [Viewport](../Assets/Scripts/Runtime/Minesweeper/MinesweeperViewport.cs) | 负责坐标与渲染变换，不负责游戏规则 |
| 按钮尺寸与换行 | [ControlLayout](../Assets/Scripts/Runtime/Minesweeper/MinesweeperControlLayout.cs)、[Page.Layout](../Assets/Scripts/Runtime/Minesweeper/MinesweeperPage.Layout.cs) | 数学布局独立测试；Unity 层应用布局、主题与状态 |
| 读档、类型校验、备份、写入失败 | [SaveData](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSaveData.cs)、[SaveCodec](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSaveCodec.cs)、[SaveStore](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSaveStore.cs) | 模型不变量、JSON 边界、文件操作分开 |
| 玩法页面、模态框、输入结果与反馈 | [Page](../Assets/Scripts/Runtime/Minesweeper/MinesweeperPage.cs)、[Page.Dialogs](../Assets/Scripts/Runtime/Minesweeper/MinesweeperPage.Dialogs.cs)、[Feedback](../Assets/Scripts/Runtime/Minesweeper/MinesweeperFeedback.cs) | 编排生产模型与 UI，不复制一套规则 |
| 大厅接入、公共设置、共享保存错误 | [AppController](../Assets/Scripts/Runtime/AppController.cs) | 保持其他游戏行为兼容；公共反馈只提供通用入口 |

独立 .NET 测试直接编译其中无 Unity 依赖的规则、Session/Profile、Gesture/Viewport/ControlLayout 和存档生产文件；Page、Pointer 与宿主接线由 Unity 检查覆盖。测试中注入随机源、单调时钟与临时存储目录，不维护一份只供测试使用的游戏实现。

## 2. 已确定的玩法与交互约定

| 项目 | 当前约定及原因 |
| --- | --- |
| 三档规则 | 9×9 / 10 雷、16×16 / 40 雷、30×16 / 99 雷；首次有效翻开才布雷，首格及邻域安全，不承诺全盘无猜 |
| 点击与长按 | 点击按当前翻开/插旗模式操作；长按达到 400 ms 即标记并反馈，松手不重复提交，不需要先选中格子再点确认 |
| 缩放 | 正常视图支持 50%～200%；减/加每次 25 个百分点，点击倍率恢复 100%。低于 100% 仍能操作棋盘 |
| 总览 | 四个小箭头向内的图标；适应整盘，只观察与定位，不能翻格或插旗，进行局继续计时 |
| 全屏 | 仅重排扫雷页面，隐藏普通导航与大标题；不修改 Android 系统栏、屏幕方向或全局沉浸策略 |
| 初级入口 | 9×9 的全屏与总览保留位置但禁用；放大后也不解锁。旧初级全屏存档恢复为普通布局，保留对局与正常视口信息 |
| 底栏 | 全屏/退出、翻开、插旗、连续缩放组、总览。缩放三段共用圆角框，仍是三个独立点击目标 |
| 持续提示 | 保留操作提示，删除底部选择难度及保存成功常驻文案；真实读取/保存错误仍必须可见 |

全屏、总览、暂停是三种不同状态：全屏只改变布局；总览禁止格子操作但不停表；暂停遮盘并停表。不要通过一个布尔值或同一套恢复逻辑混用三者。

## 3. 长按、多指与视口：最容易出错的地方

### 3.1 用整帧触点做仲裁

`MinesweeperPointer.LateUpdate` 收集本帧全部触点，包括 Ended 和 Canceled，再交给 `MinesweeperGesture.ProcessFrame`。真实触摸期间抑制合成鼠标输入，编辑器鼠标仍通过相同状态机处理。

先验证整帧是否合法，再判断长按是否到时或松手是否构成点击。第二指、取消、第三指和越界等情况必须先于当前帧尚未提交的格子操作处理；不能按逐个回调的到达顺序决定结果。已经提交的长按标记不会因后来加入第二指而撤销，但同一轮不会再次触发格子操作。

长按判断在按住期间执行，不等 UP：满足 400 ms、未超过移动阈值、仍命中原格且允许标记时，只提交一次。当前移动阈值为 12 个设计单位。提交后先消耗点击资格，再交付结果；后续允许平移或加入双指，松手不能再翻格或拔旗。

一旦离开原格或超过移动阈值，本轮格子操作资格永久失效，移回原位也不恢复。编辑器右键仍在松手时标记，不套用触摸的长按即时提交。

取消后等待所有触点释放再解锁。切页、弹窗、失焦、重布局、模式切换和新局都要取消当前输入；请求还携带 SessionId，避免旧手势作用于新对局。

**实际教训：** 只有终态正确还不够。此前长按到松手才标记，用户无法知道是否已经按够时间；后来还发现同步写盘可能阻挡及时反馈。因此标记成功后先发出声音/触感反馈，再保存、刷新与播放棋盘动画。验证既检查按住时已提交，也检查继续持有和松手不会重复反馈。

### 3.2 命中必须使用实际渲染变换

棋盘小于视口时居中，大于视口时夹紧边界。渲染和 `HitTest` 共同使用 `OffsetX/OffsetY/CellSize`；不要用未经夹紧的期望中心单独算命中。视口内、棋盘外的空白返回无命中，不能强行夹到最外侧格子。

双指缩放以此前两指中点对应的棋盘坐标为锚点，再结合当前中点计算新的中心，因此可以同时缩放和平移。缩放到上下限时仍需正确处理两指的平移分量。

保存的 `CenterColumn/CenterRow` 是正常视图的期望中心。纯布局变化只改变实际渲染偏移，不回写这个期望值，否则普通/全屏往返会逐次丢失用户的位置。真正拖动、缩放或总览点位返回才更新视图意图。

总览的适应倍率是临时渲染值，不覆盖正常倍率，也不写入正常视图存档。总览并非简单地把 `ZoomScale` 调小；这是保留用户 50%～200% 视图状态的关键。

## 4. 时间、存档与错误显示要按真实调用链检查

### 4.1 生命周期与一次结算

Session 使用可注入的单调毫秒时钟，不用系统日期累计游戏时间。只在 Active、可见、前台、未暂停、未阻塞时累计；总览不排除在计时条件之外。离页、失焦或弹窗阻塞前先结算时间，Active 恢复后要求明确继续。

关闭弹窗不等于用户完成操作。打开共享设置时页面先取消输入、退出总览、遮盘并设为 Blocked；用户点完成才执行对应关闭回调。失焦或后台由宿主直接清理公共弹窗，再传递前台状态，不能因此自动 Resume。Ready 保留已有的手动暂停语义，终局不重新启动计时。

`Resume` 等重复调用需要幂等。本次曾发现重复 Resume 重置时间锚点、丢失一段计时的问题；修复是识别状态是否实际发生变化，而不是在 UI 层假设回调只会来一次。

终局棋盘、冻结时间、`resultRecorded` 和本档纪录一起捕获。保存失败后的重试只重写当前状态，不重新记胜负或最佳时间。普通/全屏切换只取消当前手势和重布局，不借用会暂停整局的宿主流程。

### 4.2 真实 JSON 与真实文件系统

独立文件为 `minesweeper-v1.json`，配套 `.bak/.tmp`。解码先验证整个 Profile，再恢复；主档和备份不能各取一部分拼成一局。现有 Codec 限制 256 KiB、深度 12，并检查严格 JSON 语法、重复键、字段/token 类型、范围及跨字段不变量。字段默认值不能代替磁盘输入校验。

协议中的毫秒数、累计胜负和最佳值按非负十进制字符串编码，不能无意间改成 JSON 数字。缺字段、未知字段都会被拒绝；以后增加字段或修改类型，要同步考虑 schema 和旧存档兼容，不能假设宽松反序列化会自动迁移。

写入顺序是校验/编码 → 临时文件写入并 Flush → 原子替换。只有原主档有效时才将其作为备份；主档损坏后恢复成功，不能再次把坏主档覆盖到合法备份上。失败时保留内存对局，向页面返回错误。

**实际教训：** 不要用 `File.Exists == false` 一概表示首次使用。它可能吞掉访问异常。本实现通过真实打开文件区分 FileNotFound/DirectoryNotFound 与权限、I/O 等失败；测试使用真实临时目录、阻断路径和真实 Newtonsoft.Json，避免 mock 掩盖序列化或文件系统行为。

### 4.3 错误状态不能绑定于某一个可见控件

普通页使用宿主状态区，全屏使用扫雷紧凑状态区，两处必须遵循同一优先级：公共存档错误优先，扫雷错误其次；公共恢复后仍显示未解决的扫雷错误，两者恢复后清空。

本次最终审查发现：普通状态栏隐藏后，紧凑区只读取扫雷自身错误，导致公共保存失败在全屏不可见。现在 `AppController.SetStatus` 通过 `SetHostSaveWarning` 同步公共错误，`RefreshCompactStatus` 统一呈现；页面 `Save` 仍只在可见时发布宿主状态，避免后台扫雷保存覆盖其他游戏提示。

相应回归必须经过真实宿主保存入口，只阻断公共存储，让扫雷存储成功，才能验证两个来源的组合。单测“普通页公共错误”和“全屏扫雷错误”各自通过，不能覆盖这个缺口。保存恢复也要检查两处文字是否真正清空，而非只检查返回值为成功。

## 5. UI 一致性要落实到主题、坐标和触控目标

主题由 [UiFactory](../Assets/Scripts/Runtime/UiFactory.cs) 提供：纸色 `Paper #F6F4EC`、正文深绿 `Ink #173D35`、次要色 `Muted #718179`、强调绿 `Accent #276653`。沿用现有 Noto 中文字体和公共导航图标；扫雷图形用本地 UGUI 绘制，短音由模块生成，未引入外部素材包或 SDK。

- 总览、全屏入口及棋盘范围条使用主题绿，禁用态使用次要色。棋盘视口背景局部使用 `#F0F3EC`，不为一款游戏修改全局颜色。
- 普通返回按钮沿用居中 720 设计列的 `(56,34,58,58)`。比较的是同一宿主安全区下的实际屏幕边界；不能因为肉眼觉得偏移，就只对新游戏加补偿。
- 本次比较发现扫雷与数独本来同位，差异在成语页头。经用户确认，仅统一成语七处页头坐标，保持键盘适配、聊天区、游戏逻辑和存档不变。导航一致不代表所有游戏标题字号和主体布局必须相同。
- 主操作目标按 Android 逻辑密度和实际设计单位缩放计算至少 44 dp，且不小于 88 设计单位；100% 基准格按至少 32 dp 适配。主动缩到 50% 后格子会更小，不能继续宣称它保持 32 dp。
- 工具栏单行至少需要 `7 × minimumTarget + 32` 宽度；不足时上排四个操作，下排居中缩放组。优先换行，不靠缩小触控区域硬挤。
- 每行按钮高度为 `minimumTarget + 16`。常规 104 高按钮内，34 高图标、6 间隔、32 高文字居中，上下各留 16。暂停只缩小背景和图标，透明点击目标保持原尺寸。
- 底栏底边距 96，操作提示顶部距底 84、高 36，底部错误区顶部距底 42、高 34，提示与错误区留 6 单位。移动其中一项时同时检查普通/全屏、单行/双行、错误可见及窄屏状态。

这些数值属于当前扫雷布局，不是新增的全应用标准。以后调整时从布局计算和真实渲染结果验证，不只改构建控件时的初始坐标；`Layout` 会覆盖那些初始值。

## 6. 工作树、Unity 和手机数据都要隔离

Git 工作树只隔离检出文件，不自动隔离 Unity、Gradle、ADB 和手机应用数据。

1. 开始前确认实际 cwd、分支、目标基线和工作区状态。需要新增工作树时，从用户指定的基线创建；后续优先继续使用本任务工作树。本次扫雷按要求从 master 隔离开发，不因主目录有其他游戏开发就读取或迁入其未提交文件。
2. `Library/Temp` 保持本树独有，不与其他工作树链接共享。对同一工作树，Unity 编辑器、批处理 UI 检查和构建依次运行。
3. 扫雷的 UI/构建脚本加载 [minesweeper-unity-environment.sh](../scripts/minesweeper-unity-environment.sh)，使用本树 `Library/MinesweeperGradle` 和本进程独立 ADB 端口，减少批处理退出影响其他任务的风险；不修改全局设置。
4. 本次曾出现 IL2CPP 已完成、Gradle daemon 收到 stop 导致 APK 未生成的情况。不能把前一阶段成功当作完整构建成功，也不通过查找其他工作树来猜测是谁终止了进程。
5. 独立测试包为 `com.duanyao.xanaduyonder.minesweepertest`，显示名“Xanadu 扫雷测试”。构建入口临时改包名/产品名并恢复，构建后检查 `ProjectSettings` 无残留。
6. 安装时选择明确的物理设备序列号，使用覆盖安装保留数据，不卸载/清数据来绕过问题。等待系统确认不等于安装成功；成功后校验设备 APK 哈希与本次产物一致。
7. 合并前同步实际目标远端并复核增量；master 新增的已提交内容与其他工作树未提交内容是两回事。只暂存明确文件，禁止从脏主目录整体搬运、统一 stash 或清理。

## 7. 按修改面选择验证，不用一种证据替代另一种

命令从本仓库工作树根目录执行。纯规则、输入或存档修改先跑对应生产源码检查；页面/宿主修改增加真实 Unity 检查。文档修改核对事实、路径、链接和 diff 即可，无需为了文档重复编译游戏。

```bash
# 规则、会话、手势/视口/布局、实际 JSON 与文件存储
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-input.sh
scripts/test-minesweeper-persistence.sh

# 保留图形上下文：Camera.Render、EventSystem 射线和截图需要它
scripts/test-minesweeper-ui.sh

# 修改大厅或导航时追加；子 shell 限定环境变量作用域
(
  PROJECT_ROOT="$PWD"
  source scripts/minesweeper-unity-environment.sh
  scripts/test-minigame-ui.sh
  scripts/test-idiom-ui.sh
)

# 所有 Unity 任务结束后构建独立测试包
scripts/build-minesweeper-validation.sh
```

UI 检查不加 `-nographics`。测试注入内存 Profile 或真实临时目录，不读取普通玩家存档。观察失败时保存原报告，再修复并运行完整相关套件；同时查看渲染截图，断言数不能替代中文字形、配色和遮挡检查。

| 证据层 | 能确认什么 | 仍不能代替什么 |
| --- | --- | --- |
| 独立生产源码检查 | 规则、不变量、状态转换、输入数学、真实 JSON/文件边界 | Unity 接线、屏幕坐标、设备分发 |
| Unity UGUI 渲染与操作 | 真实控件尺寸、射线命中、布局、宿主组合状态及截图 | Android 多指取消、实际 dp、原生键盘、振动体感 |
| Android IL2CPP/APK 构建与签名校验 | 编译、产物存在、包标识/架构与签名 | 安装成功、启动成功或玩法通过 |
| 指定设备安装和 APK 哈希 | 手机装的是本次构建且安装成功 | 真人触控、声音、暂停恢复等交互验收 |
| 明确步骤的真机操作 | 这些步骤下的设备行为与体验 | 未执行的场景和其他设备 |

2026-10-28 合并前记录为 391 组独立检查、11015 条 UI 断言及 Android 构建/签名通过；这是当次证据，不是后续修改的自动保证。本次知识文档整理不重新执行这些测试。设备反馈已用于修订长按和 UI，但未逐项执行的完整真机矩阵仍不能标记通过。

UI 报告/截图位于 `artifacts/validation/minesweeper-ui/`，最终回归日志位于 `artifacts/validation/minesweeper-final-review/`，构建日志位于 `artifacts/logs/build-minesweeper-validation.log`。这些是本地忽略目录，不随 Git 推送；新检出应自行运行脚本生成证据，历史结论查看已提交的验证记录。

## 8. 后续修改的交付复核

提交前核对以下问题，按实际改动选择回归，不扩散到无关游戏：

- 改的是规则、会话、视口还是表现？是否把同一状态维护了两份，或把业务判断塞进通用组件？
- 长按与第二指同帧、取消后未全松手、视口空白、倍率上下限、全屏往返是否仍正确？
- Active/Ready/终局在后台、弹窗、离页与恢复时的语义是否保留，重复回调是否安全？
- 存档错误是否在普通/全屏均可见，公共/扫雷双错误和恢复是否覆盖，不可见页面是否影响当前页？
- 禁用态、触控目标、单/双行重排、提示和错误区是否都经过实际布局检查？
- 提交差异是否仅包含本次授权文件，构建设置是否恢复，合并后的源码树是否与验证对象相同？
- 推送后是否读取远端 master 验证提交一致？测试包构建、仓库推送和正式 Release 是否分别陈述？

本次主要经验是：复用已经明确的产品规范，把容易受时序影响的逻辑放到可独立验证的层，并通过真实宿主、实际存储和渲染结果补齐组合场景。每次交付只声明已经取得证据的验证层级。
