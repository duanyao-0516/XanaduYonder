# 贪吃蛇实现与验证

日期：2026-10-28。这是当前工作树中的 Unity 开发实现；正式发行版本仍为 v0.4.0。设计和 UI 已获用户确认，等级选择按用户补充要求参考数独。

## 实现范围

- 工作树 `/Users/bytedance/.codex/worktrees/snake-design/XanaduYonder`，分支 `p/duanyao.0516/feature/snake`，基线 `8d64b5f`。本地 master 的后续 `41a235d` 只增加扫雷开发说明，本次未混入其他分支游戏。
- 大厅追加当前第八款贪吃蛇，保留既有卡片顺序、350 高度与 24 间距，顶部导航和安全区沿用公共 shell。
- 经典无限：20×24、三档速度、四边相通或围墙、增长与加速、独立最高分及满盘用时；六种规则组合分别续局。
- 障碍闯关：12 关原始配置正式由 Resources 加载，固定边界/速度/目标，地图预览、顺序解锁、单独续局、目标结算及下一关。
- 界面固定纸色、墨绿、米色地格、青灰障碍；右侧方向键，左侧暂停/重来/四边相通。没有全屏模式、缩放或底部常驻提示。等级选项的背景、选中带、行内边距和滚动行为参考数独，未修改数独自身实现。
- 按键按下、棋盘连续滑动与方向键/WASD共用会话队列；暂停、帮助、设置、离开、失焦与后台均停时清输入；明确继续后倒计时 3 秒。
- `snake-v1.json` 独立原子保存，包含完整规则版本、随机状态、续局、纪录与解锁。坏档尝试备份，不兼容内容保留原文件及有效纪录。没有修改其他七款游戏的存档协议。
- 图形由原创 UGUI 几何绘制，声音/触感使用现有 GameFeedback 与共用开关；调研候选素材没有直接导入生产资源。

## 实机反馈后的界面调整

2026-10-28，按用户在 OPPO 试用后的六项反馈调整，仅涉及贪吃蛇表现层：

- 玩法入口使用共用圆角按钮底色；经典难度下拉宽度从 260 收到 164，障碍关卡保留 260 以容纳关卡名。
- 「蛇长」改为「长度」，得分、长度和果实进度的数字改为 32 号粗体，标签保持 25 号。
- 删除方向键中心圆点；上键下移 48、下键上移 48。左侧工具同步排布，使上键与「重来」、下键与「四边相通」中心线分别对齐；四个命中区域仍为 96×96。
- 相通标识改为锚定实际棋盘四边中点，旋转以图标中心为轴。棋盘与选关预览增加圆角裁切，边框沿圆角路径绘制。
- 棋盘从 608×729.6 扩为 648×777.6，宽度增加约 6.6%，20×24 格数量和玩法不变；短屏基准位置改为 x36/y252。

本轮双屏 UI 检查通过（3,584 条断言），覆盖真实圆角像素、四边图标中心、数字排版、按键对齐和模式往返后的下拉宽度；APK 更新结果见下方。旧 HTML 作为设计阶段示例保留，实际 Unity 截图反映当前布局。独立源码审查未发现阻塞问题；未改变规则、存档协议或其他小游戏样式。

## 整体操作动线调整

随后依据用户要求，从代码入口和返回链路统一梳理，不仅修改“再来一局”等按钮。完整路由表见[操作动线](interaction-flow.md)。

- 明确进入 / 开始 / 继续直接倒计时；经典难度选择恢复独立槽或新开，无活动闯关时仍预览，有活动闯关直接续局；下一关直接开始。
- 帮助、设置、未改变选项的下拉和玩法取消按来源恢复；关卡预览取消返回对应选择界面。破坏性变更取消与系统中断仍保留暂停。
- 已结束的经典槽在重新选择玩法时新开并保留纪录；从大厅回看终局仍显示原结果。声音 / 触感只在实际终局转换播放。
- 下拉关闭回调使用来源状态、sessionId 与生命周期代次防止误恢复。真实 UI 回归发现旧 Hide 协程会误删重开列表，修复为在旧列表销毁时收口该组件的延迟任务；Back 优先处理当前弹窗。
- 棋盘进一步下移到基准 top268 / 高度分配0.45，使统计栏至棋盘与棋盘至操作区的留白更均衡；大小与按钮位置保持上一轮配置。
- 两屏最终完整操作回归通过 6,878 条断言。真实按钮路径覆盖开始 / 继续、难度直接切换、活动闯关续局、取消 / Back 的返回层级、生命周期后的迟到回调、终局槽纪录保留与快速重新打开选关。

## 已执行的验证

| 层次 | 命令 / 证据 | 结果与范围 |
| --- | --- | --- |
| 纯规则与会话 | `scripts/test-snake.sh` | 37 tests passed；四向 wrap/solid、出口碰撞、尾格释放、满盘优先、不再抽食物、确定性 RNG、速度、两输入队列、倒计时、卡顿整帧暂停、终局及快照不变量 |
| JSON 与真实磁盘 | `scripts/test-snake-persistence.sh` | 194 assertions passed；真实 Newtonsoft 解析、缺字段/null/错误类型/重复键、六组槽、幂等结算、12关逐级解锁、备份恢复、原子替换、损坏 UTF-8/超长文件及不兼容文件归档 |
| 关卡几何 | `python3 docs/snake/validate_levels.py --self-test` | 12 关通过，23 个错误配置反例正确拒绝；生产资源与批准 JSON 完全一致 |
| 真实 Unity 界面 | `scripts/test-snake-ui.sh` | 6,878 assertions passed；Unity 6000.3.27f1 Play Mode、实际 UGUI / Camera.Render / EventSystem，720×1320 与 720×1582 |
| 合集 UI 回归 | `scripts/test-minigame-ui.sh` | 213 assertions passed；两种比例的八卡顺序、数独下拉/计数/弹窗、跳一跳回调、共用图标及羊了个羊表现 |
| 2048 规则/输入 | `scripts/test-core.sh`、`scripts/test-input.sh` | 43 / 10 passed，旧游戏逻辑未改变 |
| 数独选择/存档 | `scripts/test-sudoku-selection.sh`、`scripts/test-sudoku-profile.sh` | 7 / 28 passed，既有选关及四档独立续局保持 |
| 共用存档回归 | `scripts/test-persistence.sh` | 13 passed，真实 JSON 往返、旧存档、备份与写入错误回归通过 |
| Android 编译 | `scripts/build-snake-validation.sh` | Unity exit 0；ARM64 IL2CPP / Gradle assembleDebug 成功，独立 `.snaketest` 包；APK v2 签名校验通过 |

Unity 记录位于 `artifacts/validation/snake-ui/report.txt`、`artifacts/validation/minigame-ui/report.txt`。PNG 与日志位于忽略的 `artifacts/`，不是网页示意截图。UI 夹具使用独立临时目录，并通过生产 codec 回读实际写盘的满盘/目标结算；没有访问玩家的正式存档。

## Android 验证产物

- [XanaduYonder-0.4.0-snake-validation.apk](../../artifacts/builds/XanaduYonder-0.4.0-snake-validation.apk)，77,963,933 字节。
- 包名 `com.duanyao.xanaduyonder.snaketest`，显示名「Xanadu 贪吃蛇测试」，versionName `0.4.0` / versionCode `4`，minSdk 26 / targetSdk 36，ABI `arm64-v8a`。
- SHA-256：`9ea4b40cc3d247fcbc8d9565c6485827e1b24615530cdaaf21c688ef68e67cc5`。
- 本地 Debug 签名、Development APK；生成的 Unity manifest 含 `INTERNET` 权限。该包供开发验证，已按用户要求在 OPPO PKM110 安装并启动，不等同正式发布包。
- UI 修订包于 2026-10-28 16:44（Asia/Shanghai）覆盖更新到同一 OPPO PKM110：`install -r` 返回 `Success`，启动 `Status: ok`，进程存在；首次安装时间仍为 16:24:07，最后更新时间为 16:44:37。没有卸载或清数据。
- 整体动线修订包于 2026-10-28 17:34（Asia/Shanghai）覆盖更新 OPPO PKM110：安装 `Success`、启动 `Status: ok`、进程存在；本次查询 lastUpdateTime 17:34:03，firstInstallTime 16:48:08。安装命令使用 `-r`，未执行卸载或清数据。
- 构建日志 `artifacts/logs/build-snake-validation.log`；包名/权限/签名原始结果 `artifacts/validation/snake-ui/apk-verification.txt`。构建后 ProjectSettings 与场景没有残留改动。

## 定向回归与修复

1. 单帧推进两步时曾被单格插值误判为穿边。定向测试先失败，修复为仅单 tick 插值，多 tick 显示最终快照；同时验证单步动画仍存在。
2. 损坏存档恢复提示曾在再次进入时重复。真实损坏文件回归先失败，修复为将一次性读取提示与持续写入失败分离，用户确认后清除读取提示。
3. 多指/取消原先依赖另一个 MonoBehaviour.Update 清队列，可能晚于同帧 tick。二指与 Canceled 定向测试均先得到错误的 Up/head386/queue1，修复后在页面消费方向和推进前同步采样取消，两种情况均为 Right/head407/queue0。
4. 独立审查补齐非终局朝向与头颈一致、失败必须清食物、闯关不得越过早已达成的目标、终局已记账标志与纪录一致性；对应核心/持久化测试均经历失败再通过。
5. 测试夹具最初在 UGUI Start 前同步打开 Dropdown，以及混合手动时钟与真实 302ms 帧耗时。分别改为真实等待 Start、显式驱动同一生产帧路径；没有为测试绕过卡顿暂停或修改数独 Dropdown。
6. 完整动线调整先取得 1,633 断言处的 RED：实际点击满盘“再来一局”后新 session 已创建，却仍出现“已暂停”。改为按用户意图区分直接开局、临时页面返回和强制暂停，覆盖全部相关入口。
7. 快速关卡预览取消在 2,378 断言处再次 RED。核对本地 UGUI：每次 Hide 都排入延迟销毁，旧任务可能作用到重开列表。SnakeDropdown 在旧列表销毁前及 Closed 回调收口后取消本组件残余任务；页面 Back 先处理当前弹窗，重开列表等待旧列表真正销毁。两屏 Cancel / Back 最终均为 dialog=False、picker=True、paused=True、countdown=0。
8. 测试另有一次在固定 .2 秒等待后手动完成倒计时失败（690 断言）；当时没有 expanded 等诊断，不能追认具体根因。只增加诊断重跑同一生产代码即通过 6,694。最终夹具改用 2 秒超时内等待实际关闭状态，避免假定真实 UGUI 动画在固定等待后已完成；重开后仍保留存活观察以捕获迟到 Hide，最终通过 6,878。

## 实际界面截图

- [经典模式](../../artifacts/validation/snake-ui/classic-running-720x1320.png)
- [数独样式的难度下拉](../../artifacts/validation/snake-ui/classic-difficulty-list-720x1320.png)
- [障碍第08关](../../artifacts/validation/snake-ui/campaign-running-720x1320.png)
- 结算、暂停、倒计时、长屏和大厅截图见同目录。

## 验证边界

- 已运行的是生产 C#、Unity Play Mode 与真实文件路径；触摸取消使用确定性输入帧夹具，不冒充 Android 触摸驱动或真机操作。
- 2026-10-28 16:24（Asia/Shanghai）已在 OPPO PKM110 安装并启动：ADB install 返回 Success，am start -W 返回 Status: ok，包管理器确认 versionName 0.4.0 / versionCode 4，启动后进程存在。尚未验证 Android 真机连续触摸、48dp 物理命中范围、帧率、触感、强杀与断电文件系统表现。96 设计单位按钮在 360dp 内容宽度对应 48dp，但其他屏幕仍需实机换算；两张设计分辨率截图不证明所有设备通过。
- 关卡通过静态几何检查和规则驱动解锁验证；尚未进行每关多随机种子的人工试玩与难度调优，也没有宣称任意长蛇/食物序列必可解。
- Unity 日志中既存 QuickSearch 编辑器索引异常与测试失败原因分别记录；应用脚本的编译及检查结果以各自报告为准。
- 未提交、推送、合并或发布；没有覆盖正式安装包，也未操作其他任务的模拟器。

## 合并主线补记（2026-10-28）

贪吃蛇实现已提交为 `2a8d955`，本轮合并目标为 `origin/master` 的 `0f6e4fd`。该主线已有 12 款游戏和 0.5.0 源码版本；贪吃蛇保留原有游戏顺序，追加为第 13 款（index 12）。已发布的 v0.5.0 仍是前 10 款，新增源码不代表已经重新发布。

本文前述测试、截图、APK 哈希及 OPPO 安装启动均属于合并前证据；其中第 8 款、0.4.0、旧基线和当时未提交的描述保留历史含义，不改写成已经验证 13 款的结果。既有 APK 不作为本次合并产物。

本轮保留 master 的全部 12 款游戏及宿主导航、前后台和保存接线，补入贪吃蛇。合并后重新验证：

- `scripts/test-snake.sh`：37 项通过。
- `scripts/test-snake-persistence.sh`：194 项断言通过。
- `python3 docs/snake/validate_levels.py --self-test`：12 张地图和 23 项拒绝用例通过。
- `scripts/test-snake-ui.sh`：Unity 退出码 0，`SNAKE_UI_CHECKS_PASS: 6918 assertions`，覆盖 720×1320、720×1582、13 款大厅顺序及编号、实际下拉点击、倒计时和生命周期。
- `scripts/test-minigame-ui.sh`：Unity 退出码 0，`MINIGAME_UI_CHECKS_PASS: 247 assertions`，两种屏幕尺寸通过。
- 743 个 Unity 资源 GUID 无重复；相对 `origin/master` 的差异空白检查通过。全量合并差异的空白提示来自 master 已有第三方文件，原样保留。

本轮没有重新构建 Android APK、安装设备或发布版本；上述 Unity 回归不替代合并后的 Android 真机验收。
