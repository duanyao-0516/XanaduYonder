# 小恐龙 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 在当前独立工作树交付可离线运行的小恐龙，横竖屏共用规则，以真实视野限制速度及障碍间距。

**Architecture:** Core/Dino 独占固定步运动、碰撞、出怪和计分；Runtime/Dino 管理方向选择、布局、输入、音画与独立纪录。AppController 只接入路由、方向请求/恢复和公共设置。选择页保持竖屏，游戏使用独立安全区根节点，不压缩公共竖屏框架来模拟横屏。

**Tech Stack:** Unity 6000.3.27f1、C#、UGUI、Newtonsoft.Json 13.0.2、.NET 10 独立测试、Android ARM64。

## 隔离与执行规则

- 所有操作显式使用 `/Users/bytedance/.codex/worktrees/chrome-dino/XanaduYonder`；分支 `p/duanyao.0516/feature/chrome-dino`，起点 `b88694c`。
- 不切换、清理、修改其他工作树，不共享 Library/Temp/obj/bin，不推送或合并 master。
- Android 验证包使用 `com.duanyao.xanaduyonder.dinotest`；日志/APK/截图在本工作树 `artifacts/`。不安装覆盖正式包或其他工作树测试包。
- 独立任务按文件归属并行；只有主代理改 AppController、ProjectBuild、公共 UI、项目文档及执行 git 提交。运行 Unity 构建前检查是否已有相同 projectPath 的进程。

## 1. 固定共享 Core 契约

文件：`Assets/Scripts/Core/Dino/{DinoRules,DinoGame,DinoObstacle}.cs`，`Tests/Dino/`，`scripts/test-dino.sh`。

公开契约（数值使用 double，得分 long；适配层转换 float）：

```csharp
var rules = new DinoRules(viewportWidth);
var game = new DinoGame(rules, seed);
game.Jump();              // 准备状态开始并跳跃；空中不缓存输入
game.ReleaseJump();       // 主动松手才截断上升
game.SetDuck(true);       // 空中加速下降；地面切蹲跑
game.Step();              // 固定 1/60 秒；未开始/终局不推进
```

只读状态：`Rules`、`PlayerY`（脚底距地面）、`PlayerVelocity`、`Ducking`、`Jumping`、`Speed`、`Distance`、`Elapsed`、`Score`、`IsStarted`、`IsGameOver`、`Obstacles`。障碍包含 Id、Kind、X、Y（底部）、Width、Height、GroupSize；四类为 SmallCactus/LargeCactus/LowBird/MidBird。规则公开 ViewportWidth、PlayerX、PlayerWidth、PlayerHeight、DuckWidth、DuckHeight、MaxSpeed、StepSeconds。

- [x] 先写规则测试，运行脚本证明缺失实现时失败，再实现固定步状态与跳跃。
- [x] 覆盖短/长跳、不连跳、下蹲与空中下落、终局只结算一次、读状态不消耗随机数。
- [x] 出怪使用可重放随机状态，按时间开放种类；前 3 秒无危险。
- [x] 使用实际运动/碰撞检查可解组合；安全速度按完整可见至最晚动作的时间计算，扣 100ms 预算仍至少 1.2s，下一动作间隔至少0.25s。拒绝无法跨越的障碍宽度组合，不能只验证不重叠。
- [x] 对视野 420/600/900/1200 及固定种子、多速度执行轨迹重放；同视野相同输入产生相同结果。渲染驱动30/60/120Hz不改变模拟。

验证命令：`scripts/test-dino.sh`；预计所有测试通过，记录实际数量与边界验证结果。

## 2. 独立纪录与素材

文件：`Assets/Scripts/Runtime/Dino/{DinoSaveData,DinoSaveCodec,DinoSaveStore}.cs`，`Tests/DinoPersistence/`，`scripts/test-dino-persistence.sh`，`Assets/Resources/Dino/`。

存档契约：`schemaVersion=1`、`gameId=dino`、两个非负十进制字符串纪录 `bestPortraitScore`/`bestLandscapeScore`，方向 `preferredOrientation=portrait|landscape`。保存对象独立拥有两项纪录与偏好，不写实时局面。`Load(out warning)` 返回合法对象或默认值；`TrySave(data,out error)` 原子保存。

- [x] 先测试编码/解码、非法token、缺失/null/溢出/重复键/未知版本、备份恢复及写失败不损坏旧档。
- [x] 实现真实 Newtonsoft 解析，文件大小/深度限制；文件仅访问 `dino-v1.json/.bak/.tmp`。
- [x] 将已固定哈希的官方2x图集与3个音效导入独立资源目录；音频以真实 Ogg 格式命名，保留原始字节和完整许可，记录来源及哈希。
- [x] 图集 Point 过滤、无mipmap、无有损压缩；不复制 Chromium 游戏实现代码。

验证命令：`scripts/test-dino-persistence.sh`；素材逐文件 SHA-256 与来源一致。

## 3. 横竖屏布局与输入

文件：`Assets/Scripts/Runtime/Dino/{DinoPage,DinoViewport,DinoTrackView,DinoTouchControl,DinoAudio}.cs`，必要的纯C#输入所有权/会话模型及其测试。

- [x] 先测试动作单owner：Jump按钮/跑道/空格/上键不互相抢占；Duck独立。前台取消释放，生命周期取消保留当前跳跃与蹲姿。
- [x] 进入先显示方向选择，默认竖屏并记住上次成功进入方向。每次大厅进入仍选，重开沿用本局方向。
- [x] 建立独立安全区排版，短边作为相同角色尺寸基准；横屏展开更多逻辑宽度。两种方向跑道全宽、按钮在跑道下，不遮挡危险。
- [x] 方向切换连续两帧布局稳定后开放准备，3秒超时可回选择。取消/退出使过期回调失效；局中锁定方向。
- [x] 固定步累加器驱动Core，>100ms卡顿暂停不追赶；暂停/失焦/设置/后台全部冻结，继续3秒倒数。恢复不截断原长跳、不自动站起或带出新跳。
- [x] 完整渲染精灵帧、可循环地面、云、昼夜、分数、结果、帮助及许可。图集坐标按2x表，世界与像素实体映射一致。
- [x] 加载Ogg音效，遵循公共开关；页面离开与销毁停止播放并释放动态资源。

## 4. 合集接入与验证工具

文件：`AppController.cs`、必要的公共弹窗布局参数、`ProjectBuild.cs`、`scripts/build-dino.sh`，独立 Unity 检查入口。

- [x] 大厅新增同规格卡片，显示双纪录；更新滚动内容高度，保留已有卡片和返回滚动位置。
- [x] 所有路由离开Dino时释放输入/音频、保存纪录、恢复竖屏；系统返回与首页统一确认未完成局面。
- [x] 公共设置数据仍由shell持有；Dino横屏设置/确认/帮助可见且不套竖屏窄框，旧游戏默认布局保持不变。
- [x] 构建独立dinotest包，PlayerSettings临时覆盖后恢复；不修改版本号/正式发行身份。
- [x] 实际 Unity 编译与 PlayMode/场景集成检查：开局选择、双方向布局、输入、暂停、纪录、退出。

## 5. 审查、回归、交付

- [x] 独立规格审查，再做代码质量审查，修复有证据的问题。
- [x] 执行 Dino 规则/输入/持久化、已有 Core/Input/Persistence 与受影响集成检查；变更后按影响范围复跑。
- [x] Unity Android构建。设备仅在未被其他工作树使用时，以明确序列号安装独立包，验证触控、旋转、后台/返回和旧游戏；无法验证的层次如实记录。
- [x] 更新 README、architecture、THIRD_PARTY_NOTICES、dino-game 和 verification-dino；记录实际命令、测试数、包路径和设备验证边界。
- [x] 仅提交本功能文件，保留此工作树供后续开发，不合并、不推送。

## 执行说明

已按本计划完成实现与验证，实际数字、命令、模拟器操作及未覆盖的实体机范围统一见 [verification-dino](../../verification-dino.md)。页面 Unity 检查采用真实对象／回调和受控方向输入；Android 旋转、后台和返回流程另在专用模拟器验证。没有将模拟器与状态检查视为实体机体验验收。

安全区变化时保留原局完整逻辑世界、障碍时间戳和动画状态，只等比重映射；方向、角色可读尺寸或竖向净空不足则继续暂停等待恢复。准备和重开入口也使用同一窗口校验。
