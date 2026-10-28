# 黄金矿工实现与验证记录

日期：2026-10-27。代码、构建、Unity 界面与 Android 模拟器证据分别记录；尚未完成的物理设备验收不计为通过。

## 1. 工作树与交付范围

- 工作树：`/Users/bytedance/.codex/worktrees/0053/XanaduYonder`，分支 `p/duanyao.0516/codex/gold-miner`。
- 本轮确认远端 master 后，从 `ca50db0` 快进合并至 `b793fe8b43f3bafe6380a32464dfe7b45993437e`；保留本树已有设计文档与素材。实现目前为该基线上的未提交改动，不使用其他游戏工作树。
- 新增大厅第 07 卡片 → 模式选择；单人竖屏、合作横屏全屏、左右半屏独立 Down 出钩。两种模式各 10 关，独立续局与记录，共享合作资金。
- 已实现移动动物与携钻动物、连续扫掠抓取和唯一归属、截止等待回收、炸药、五种商店补给、检查点重试、最终通关、暂停/帮助/确认、公共声音触感、存档与坏档恢复。
- Core 拥有规则与状态；Unity 层负责展示、输入、方向和文件适配。没有更改六款旧游戏的专属规则或存档格式。
- 本次没有提交、推送、改正式版本号、创建标签或发布 Release。
- 源码、资源及设置 958 个文件的散列清单为 `artifacts/gold-miner-development/final-source-manifest.json`，绑定最终交付 APK；临时构建副本已在保留报告后清理。

## 2. 独立测试与 Unity 检查

| 实际执行入口 | 当前通过结果 | 证据范围 |
| --- | --- | --- |
| `scripts/test-gold-miner.sh` | 47 tests | 规则、连续碰撞、独占、计时、炸药、商店、会话、非法快照与截止回收预算 |
| `scripts/test-gold-miner-input.sh` | 54 tests | 同一生产指针状态机；双指、跨半屏、UI 消费、取消、代际隔离与方向作用域 |
| `scripts/test-gold-miner-persistence.sh` | 28 tests | 真实 Newtonsoft.Json、磁盘、原子替换故障、备份、显式恢复；边界读取与分配回归 |
| `scripts/test-gold-miner-assets.sh` | 347 checks | 44 PNG 原字节、清单、43 切片、导入与音频绑定 |
| `scripts/test-gold-miner-levels.sh` | 9579 checks；240 张成功见证、30 组完整生成复现 | 真实 Core 重放，最紧资金、袋子最低价值、无增益/炸药；有界候选失败拒绝输出 |
| `scripts/test-gold-miner-catalog.sh` | 22 groups；240 图；2000 种子样本 | 目录/分片严格校验、运行时稳定选择、图案不重复、袋子一致与失败不消耗种子 |
| `scripts/test-gold-miner-campaign.sh` | 960 checks；20 关、80 次落盘事务、8 次冷加载 | 两模式无道具完整十关；另测五种全购、真实失败与付费检查点重试 |
| `GoldMinerAssetImporter.Prepare` | Unity 退出码 0 | 实际导入与复读：44 PNG、43 作者切片、Sprite 矩形/锚点、ETC2、GUID |
| `scripts/test-gold-miner-ui.sh` | **15449 assertions**，Unity 退出码 0 | 真实 Play Mode/UGUI/页面/Store；两模式、准备/暂停/帮助/确认、见证胜利、商店、下一关、失败重试、截止输入与恢复 |
| `GoldMinerBuildSettingsScope.RunValidation` | Unity 退出码 0 | 正常返回和主动异常路径均恢复内存中的 Player/Quality/Editor 设置、场景和构建标记 |
| `scripts/test-minigame-ui.sh` | 205 assertions，Unity 退出码 0 | 原大厅/公共页面及相关旧游戏界面检查；不是六游戏真机全玩法验收 |
| `scripts/test-core.sh` / `test-input.sh` / `test-persistence.sh` | 43 / 10 / 13 tests | 本轮运行的公共/2048 原有回归 |
| `scripts/prepare-gold-miner-touch.sh` | 编译、D8 退出码 0 | Android 36/JDK17 生成真实 MotionEvent shell 驱动；后续原生注入另列 |

Core 另比较单人 3651 tick、合作 3723 tick 的完整状态、检查点、事件和累计入账 SHA-256。普通/四叶草各 100 万固定抽样的分布分别为 60.0591%/29.9585%/9.9824% 与 20.0223%/40.0368%/39.9409%；值域和每袋仅推进一次 RNG 正确。暂停、冷恢复、回收及重试不重抽。日志位于 `artifacts/gold-miner-development/core-final.txt`。

UI 新增故障用例经过实际 Page 调用：运行中写盘失败保留内存进度、dirty 和错误，恢复后精确保存并清错；Shop 写盘失败保留资金、库存、交易序列和旧文件，随后成功重试只提交一次。注入 250ms 帧间隔验证输入时钟不会吞掉显示帧耗时，该用例不是设备性能测量。

布局夹具为 720×1320、720×1582、1320×720、1582×720，加竖屏上下与横屏非对称安全区；真实 Unity 截图覆盖四种尺寸，长屏商店额外检查所有文本区域不相交、完整中文行高和按钮安全区，两模式还截取了实际保存/购买失败状态。Android 模拟器另覆盖 1080×2373 与 2373×1080。

证据目录：`artifacts/gold-miner-development/ui/report.txt`、同目录 PNG；`artifacts/validation/minigame-ui/report.txt`；`artifacts/logs/gold-miner-ui-checks.log`、`gold-miner-import.log`、`gold-miner-build-settings.log`、`minigame-ui-checks.log`。

Unity 启动曾输出 QuickSearch 的 SearchDatabase 索引异常，相关检查最终退出码为 0；未把此编辑器索引日志归为游戏交互通过的依据。实际游戏源码编译、素材导入与上述断言均完成。

## 3. 关卡与资源

- 目录 `Assets/Resources/GoldMiner/Levels/` 包含 catalog + 20 个分片，共 240 图、21 个 JSON、1,209,339 字节。
- catalog SHA-256：`241e349ea6907ad12ceedb9b15bb41ac1d910a8f060d7cdef5fe65781c3732ec`。
- `artifacts/validation/gold-miner/levels.json` 保存每张图的 seed、见证、收益与实际验证结果。生成统计共 240 条：228 条原始生成日志；首关 12 条以独立目录重新生成补齐，标记 `verified-reproduction`，没有冒充历史计时。
- 首关复现 0.567 秒，生成分片与生产文件逐字节一致；SHA-256 `0af8aa41fedfd56948c553287240dae9379887928bcde8ce2bba1e5b333638e1`。统计来源见 `generation-archive.json` 与 `generation/`。
- 指定种子 `1、7、42、20261027、uint.MaxValue` × 两模式 × 首关三图案，共 30 组完整 Generate 各运行两次，完整布局、种子、候选/展开数和见证一致；60 条新见证均成功。范围是首关重复生成，十关全部图另由 240 条见证覆盖。报告 `generation-reproducibility.json`。
- 完整挑战通过真实 Catalog/Session/Store 完成两模式各十关，无手动注入 Won：单人最终 28840≥18300，合作 39590≥29280，记录各一次 Completed。付费分支覆盖全部五道具购买 670、激活、第二关真实失败、重试相同检查点；不宣称带全部增益十关通关。报告 `campaign.json`。
- 44 张生产 PNG 合计 51,711,510 字节；原图、Alpha 与留白保留。整页 UI 稿、参考图和用户 ZIP 中未采用的脚本/音频不进入生产资源。

Unity Editor 实测的视图纹理数据：

| 视图 | 缓存纹理数 | 纹理字节 |
| --- | ---: | ---: |
| 单人矿场 | 17 | 83,934,892 |
| 单人商店 | 8 | 30,449,628 |
| 合作矿场 | 19 | 89,321,689 |
| 合作商店 | 10 | 37,153,116 |

20 次真实进入/离开后执行 `Resources.UnloadUnusedAssets` 和完整 GC：Unity 已分配内存 239,434,722 → 237,393,014 字节，Texture2D 对象 664 → 664。以上是编辑器数据，不能当作 Android ETC2 占用或物理设备内存结论。

## 4. Android 安装包与模拟器

`scripts/build-gold-miner-validation.sh` 的构建入口已成功输出独立 ARM64 IL2CPP/OpenGLES3 Development APK。最终交付包在本树 artifacts 下的干净项目副本中使用相同入口重建，源码/资源/编辑器 920 个文件与开发树逐字节一致；构建结束后开发树 ProjectSettings 无 Git 差异。

- 文件：`artifacts/builds/XanaduYonder-0.4.0-gold-miner-validation.apk`
- 包名：`com.duanyao.xanaduyonder.goldminertest`；名称：`Xanadu 矿工测试`。
- 版本：0.4.0 / code 4；minSdk 26、targetSdk 36；这是隔离验证包，不是正式发布包。
- 大小：98,662,338 字节（94.09 MiB）。
- SHA-256：`e3c930856b858766f9527c37c92eb9b3d59b2daabb57d45ba41177e2033f45e1`。
- 最终构建日志：`artifacts/logs/build-gold-miner-final-clean.log`。
- 同基线、同配置的无 Gold Miner 对照包：62,677,420 字节，SHA-256 `b4b64edb5b85a887b1c98d8eefb6d9f8b36892714d680c587677847a14e57cf5`。黄金矿工实测 APK 增量为 **35,984,918 字节（34.32 MiB）**，低于方案 40 MiB 目标；这不是安装后磁盘占用。

对照方法：从 `b793fe8` 导出 Assets/Packages/ProjectSettings，复用最终 Editor 构建入口与设置作用域，仅省略 Gold 资源导入；最终项目复制当前完整源码、资源及生产素材清单。两边使用同一 Unity、相同 `EditorUserBuildSettings.asset`，从空 Library 和空输出目录串行构建，包名、版本、SDK/架构的 aapt 输出及签名证书一致。两包 CRC、唯一条目、活动 ZIP 记录检查通过；对照包同时没有 Gold 类型和关卡资源，最终包两者均存在。首次克隆 Library 的对照因陈旧 Gradle 资源而作废，旧增量包的未引用 ZIP 条目也未带入新交付包。有效报告为 `artifacts/gold-miner-development/apk-size-comparison/comparison-clean.json`，保留大小、有效压缩/解压总量及逐条目差异；不将此差值冒称为位级确定性或 BuildReport 的逐纹理归因。

本任务创建专用 AVD `XanaduGoldMiner0053Api36`（`emulator-5560`）：Android 16/API36 ARM64，1080×2373，SwiftShader 软件图形；路径位于本树 `artifacts/gold-miner-development/avd/`。没有使用另一个任务的 `emulator-5554`。

已在专用 AVD 安装上述最终 APK。下列完整操作先在同一实现的修复前包执行，最终包另外复验了启动、模式选择、长屏商店、真实写盘失败及第二关原生双指；没有把之前的全部操作冒称为在最终包上重新执行：

- 第 07 卡片可滚动到达，模式选择保持竖屏；单人开局、采集入账和暂停正常。
- 合作进入 2373×1080 横屏全屏，HUD、P1/P2、两侧炸药和局部弹窗完整显示。
- shell 驱动注入实际 Android `SOURCE_TOUCHSCREEN` 双指事件，两个 Down 相隔 1ms；保存的 `lastCommandSeq=2`。截图同时显示 P1 出钩和 P2 收获，两个玩家各接受一次命令。
- 左指跨屏移动再 Up，命令序列仅 2→3，P2 未被跨屏 Up 发射；同侧双指序列仅 3→4。
- 上述合作操作后，单人完整槽位与进入合作前相同。
- Home 切后台后两次存档完全相同，合作 tick 保持 466；回到应用显示暂停层。
- 终止测试包进程并重启，再进入合作时完整存档与后台前相同，继续前仍暂停；两模式资金及在途钩状态保留。
- 合作首关以原生连续双指实际游玩完成：资金 3150，P1/P2 贡献 1360/1790，0 tick 后 Won。进入商店给 P2 买一份炸药，再重复点击力量药水，实际只完成两笔交易，资金 3150→2940，炸药只加给 P2；第二关 Ready 正确激活力量增益并保留另一模式。
- 对独立测试包的主档和备份注入同一 17 字节截断 JSON，冷启动后原文件仍完整保留；开局弹出明确的归档确认。确认后两个原文件均带 `invalid-…` 后缀保留原字节，新局合法 Ready。随后恢复注入前的测试进度。证据 `invalid-archive-evidence.txt`，没有改正式应用数据。
- 原六游戏在模拟器中均完成入口打开/返回的冒烟检查，2048 额外实际滑动；截图 `23-old-2048.png`、`24-old-jump.png`、`25-old-sheep.png`、`29-old-watermelon.png`、`31-old-idiom-loaded.png`、`32-old-sudoku.png`。未将入口检查扩大声称为全部旧玩法及存档兼容的设备验收。

SwiftShader 模拟器记录的实际前台帧数据：单人 220 帧，平均 24.70ms、p95 33.46ms、p99 36.63ms；合作 2078 帧，平均 25.58ms、p95 34.41ms、p99 41.44ms。合作暂停/商店 PSS 分别为 259831/266617 KiB，RSS 为 387472/394880 KiB。这些是软件图形模拟器及 Development 包测量，不是物理手机性能结论。日志和 `meminfo-coop-*.txt` 位于同证据目录。

模拟器首次商店截图发现 2373×1080 横屏的说明与价格重叠。新增真实 1582×720 GameView 检查先复现失败（重叠 49.4 像素，报告 `shop-long-screen-before-fix.txt`）；随后按卡片剩余高度分配插图、标题、完整中文行盒、价格与按钮。另为非空保存错误提示留出不与底部控件重叠的独立行，使用米白底和深绿字保持对比，消息消失后隐藏。上述检查均已通过。

最终包复验：`35-final-coop-shop.png` 在实际 2373×1080 画面中完整显示五种商品的标题、双行说明、价格和购买按钮，未重叠。向独立测试包的 `gold-miner-v1.json.tmp` 路径创建空目录，触发真实文件写入失败；购买后磁盘存档逐字节不变，画面资金 3150、炸药库存与剩余数量不变，米白错误条清晰可读。移除该空目录后重试同一购买，txnSeq 4→5、资金 3150→3080、P2 炸药 1→2，P1 与单人完整槽位不变，错误提示消失。报告为 `final-shop-write-failure-report.json`，对应 `final-shop-before/failed/retry.json` 与 `36-final-shop-write-failure.png`、`37-final-shop-retry.png`。所装 APK 的逐字节校验记录见 `final-installed-apk.json`。

随后进入第二关 Ready 并开始游戏，原生双指的两个 Down 本次相隔 10ms，命令序列 60→62；截图显示 P1 已采集 500、P2 正在回收，单人完整槽位不变。证据为 `38-final-coop-ready.png`、`39-final-native-dual.png`、`final-native-dual.log` 和 `final-native-before/after.json`。所装 APK 与交付文件逐字节一致，签名验证通过；专用模拟器在验证后已停止。

文本检查按实际 UGUI `preferredHeight` 测量，并验证生成器行高、字形顶点和 CanvasRenderer 网格的一次单位换算一致；避免把已缩放的 best-fit 像素字号再作为设计字号而重复缩放。标准屏作者字号 26/生成字号 26，1582×720 的 scale 为 1.198485、生成字号 31；实际网格高度与换算后生成几何完全一致，没有放宽重叠或完整文本断言。

证据位于 `artifacts/gold-miner-development/emulator/`：`native-dual.log`、`native-cross.log`、`native-same-half.log`、成组 JSON、PNG 与 logcat。此类 shell 原生注入区别于编辑器合成 PointerEventData，但仍不能替代物理手机双指体验。

## 5. 尚未关闭的设备验收

物理手机 PKM110 / Android 16 / ARM64，序列号 `3B6F4NE8A66ZXTRL` 已连接。安装时设备锁屏，第一次安装未完成；已请用户解锁并允许独立测试包安装，尚未获得完成回复。未覆盖安装正式包，正式应用 7 份存档前后哈希相同，证据为 `artifacts/gold-miner-development/device/formal-saves-before.sha256` 与 `formal-saves-after.sha256`。

仍需在物理手机完成双指、系统旋转/安全区、锁屏与后台恢复、声音/触感、实际帧时间/内存、两模式完整人工十关试玩及旧六游戏的设备回归。自动规划器成功见证和 .NET/Unity 流程测试不计为人工试玩。测试包已可交付，但不能将整套物理设备验收标为完成。

## 6. 独立审查与修正

本轮实施后由独立 agent 检查规则、数值、状态、输入、UI、持久化和构建设置。修正并复测了：迟到输入先补算旧时间；暂停前最后一 tick 的结算；纯输入 gate 不产生重入；独立显示帧时钟保留保存耗时；胜利音效在结果视图重建后播放；合法 Draining 的总时长及剩余回收预算；离开视图清资源；非 dirty 不重复保存；小存档不按 1MiB 分配；正常/异常退出均完整恢复 Unity 构建设置。独立审查不能替代上节尚未执行的设备项目。

## 7. 2026-10-28 回收平衡修订（规则 v2）

本节记录本轮纯 Core / 文件持久化验证。前文首版 APK、截图、设备计时和目录哈希属于规则 v1 的历史证据，不能用于证明本轮画面或设备体验。

### 7.1 原因与新节奏

回收速度原本直接在固定 60 Hz Core 中按 `6 / 重量` 推进；运行时世界坐标等比映射，没有多乘帧率或 UI 像素换算错误。数值本身过快，较大物件的碰撞半径还使抓取点离绞盘更近。带载基速改为 `2.4 / max(重量, 0.5)`，空钩从 8 改为 5 世界单位/秒。重量、半径、摆速 60°/秒、伸出 7.2、力量倍率 1.5、关卡 3600 tick、目标/价值/价格/物件数量全部保留。

同一竖直方向、矿物中心深度 3、没有增益的真实 `GoldMinerGame.Step` 测时如下。起点是首次观察到已抓取的 tick，终点是钩恢复 Swinging 的 tick，因此有最多 1/60 秒的采样差；不含摆动等待、伸出或视觉动画。

| 物件 | v1 回收秒数 | v2 回收秒数 |
| --- | ---: | ---: |
| 小金块 | 0.417 | 1.050 |
| 中金块 | 0.800 | 2.017 |
| 大金块 | 1.517 | 3.817 |
| 小石头 | 1.250 | 3.133 |
| 大石头 | 1.917 | 4.800 |
| 钻石 | 0.250 | 0.633 |

深度 6.6 的大石头实测回收 **12.300 秒**；力量药水后 **8.200 秒**；抓到后立即使用炸药，剩余空钩回收 **1.183 秒**。大金与小金在同深度产生约 2.77 秒差异，大石与大金相差约 0.98 秒，昂贵轻物/大金/低价值重石之间有明确的时间取舍。

新增计时回归先在原规则下出现预期失败，再修改参数通过；日志为 `artifacts/validation/gold-miner/balance-core.log`。`MaximumSimulationTicks` 由新最慢速度 0.48 自动推导为 Solo **4853**、Coop **5346**；60 秒后仍禁止新出钩，仅允许已有动作完成。最后一 tick 发射大石、存档剩余回收预算、越界拒绝与全量逐 tick 重放均纳入 Core 回归。

### 7.2 已有进度兼容

`schemaVersion=1` 和文件名 `gold-miner-v1.json` 保持不变，当前 `rulesVersion=2`。严格接受且完整校验旧 rules v1 后，Session 只在私有副本上升级版本并标记 dirty；双槽、记录、金额、道具、tick、在途钩、命令序号、检查点及原布局哈希不重置，也不额外增加事务序号。继续游戏后使用 v2 的慢速回收。未知版本仍拒绝，首次正常持久化将原 v1 字节保留为 `.bak`。

从前轮 Android 验证原始文件逐字节复制了五份 v1 夹具到 `Tests/GoldMinerPersistence/Fixtures/`：商店、第二关 Ready、运行中双人、带载回收和空钩回收。校验升级前后整个 JSON 字段树仅 `rulesVersion` 发生变化；文件回读、v2 再次加载幂等、旧备份字节一致均通过。两份在途夹具实际继续运行到空闲，带载只按原物件身份入账一次，空钩不入账。原始来源为 `artifacts/gold-miner-development/emulator/coop-shop-before.json`、`coop-second-ready.json`、`coop-native-dual.json`、`coop-native-cross.json`、`coop-native-same-half.json`。日志为 `balance-persistence.log`。

### 7.3 240 图与完整挑战复验

全部 240 图重新生成 v2 成功见证和 SHA-256 目录，数量、原有三个图案和布局分布未变。对比生成前 v1 备份：**240 张图的全部实体坐标、类型、袋子保底值和初始状态逐字段一致，0 张换图**。因此旧存档保留的检查点几何同样有新规则下的成功路径，无须换图或清除进度。见证保留无炸药、无增益、袋子只取 100 的约束，收入预算仍覆盖前一商店最大消费。全部见证 `endTick=3600`，没有依靠截止后的额外回收时间达到过关目标。

- 生产目录：21 个 JSON、1,210,447 字节；catalog SHA-256 `185ca19d7c24f250922d55b275ccb2c48c74bbef627672be65a207a10dc36f56`。每个分片均为 rules v2，20 个分片哈希与目录逐项一致。
- Core：**50 项通过**；Persistence：**29 项通过**，其中五个真实 v1 文件用于完整无损升级和备份检验。
- Levels：**9600 项检查通过**，240 条生产 Core 见证、30 对完整生成复现；Catalog：**22 项通过**。`levels.json` 与 `generation-reproducibility.json` 为本轮实际输出。
- Campaign：**976 项检查通过**，单人/合作各 10 关、全部道具购入/真实失败/重试两分支、81 次持久事务、8 次重载。单人最终余额 **28520≥18300**，合作 **39490≥29280**。新增真实 v1 Shop→v2 第二关 Ready 分支，验证原现金/消费/另一槽不变，新关使用新目录哈希，旧档仍为原字节 `.bak`。报告 `campaign.json`。
- 生成记录来自本轮实际搜索，240 条均有记录。`balance-v2-generation.log` 完成前 19 个分片；合作第 10 关并行记录为 `balance-v2-coop10-generation.log`。前批在第 9 关文件完整落盘后停止，避免重复第 10 关；最终全部验证针对合并完成的稳定生产目录。

复验命令均成功退出：`bash scripts/test-gold-miner.sh`、`bash scripts/test-gold-miner-persistence.sh`、`bash scripts/test-gold-miner-levels.sh`、`bash scripts/test-gold-miner-catalog.sh`、`bash scripts/test-gold-miner-campaign.sh`。日志为同一 artifacts 目录下的 `balance-core/persistence/levels/catalog/campaign.log`。这些结果证明 Core 数值、经济可达性及存档兼容；本节不将自动见证冒称为人工试玩或物理手机动画/手感验收。

## 8. 2026-10-28 UI 与机械呈现修复

依据批准的 `artifacts/gold-miner-ui-v1/screens/01..11` 对照实际运行截图，重排模式选择、单人/合作 HUD、商店、预告、暂停、帮助、成功/失败、离开确认，恢复暖白/赭金描边、深绿文字、圆角按钮与玩家金/青区分。模式页手绘字、Ready 半身拇指矿工、失败空木牌矿工依据原图补绘；动态数字和木牌标题仍由 UGUI 绘制。大厅卡片使用既有 success 庆祝矿工与金块，不压缩卡片。

### 8.1 实际界面复核

- 最终 `scripts/test-gold-miner-ui.sh`：**16248 assertions PASS**，真实 Play Mode/UGUI/Store/状态机；数字不代替视觉验收。
- 补充真实 `720×1280` 短屏商店与 `1582×720` 长屏暂停检查，修正钻石卡说明压价格、帮助按钮压设置开关的问题。
- 修正图集透明边距导致的旗帜/时钟/暂停图标过小；Ready 隐藏底层实体，成功页隐藏 HUD/底控，帮助页隐藏原操作区，CloseModal 恢复。新失败标题验证完全位于空白木牌内，避开手和脸。
- 已逐图检查模式选择、两模式游戏/商店、Ready、暂停、帮助、成功/失败、确认及长短屏。最终图位于 `artifacts/gold-miner-development/ui/`。小装饰仍采用运行时控件：帮助页触点环、设置文字与开关、无铆钉的纸面商店卡；不称为逐像素复制。
- 20 次真实进入/退出后执行资源回收与 GC，记录分配量 242510126→240465186 字节，Texture2D 对象 667→667；这是编辑器样本，不是手机性能阈值证明。
- APK 横屏复核追加发现 2373×1080 时失败矿工帽子被裁切，成功徽章也有同根因：固定负顶部偏移假定了原稿画布高度。改为按真实安全区和下一条文案之间的空间等比适配顶部美术，木牌标题同步缩放。新增源 PNG 逐像素 alpha 四边检查与真实 GameView 的 Ready/成功/失败截图，覆盖 1582×720、2373×1080、2400×1080、720×1280/1582。最终 `ui-final-responsive.log` 已通过，主 agent 查看两张 2373×1080 成功/失败页，顶部完整且不压文案。

### 8.2 机械根因与回归

红测试实际复现两人钩轴与绳方向点积只有 0.7960838，整张侧视鼓轮在 0.25 秒内绕屏幕 Z 转 90°。修复后由实际绳长变化驱动绕倾斜横轴的三维摇柄；支架和鼓轮外形固定，转动由摇柄与鼓面纹理体现。已有拆分手臂采用肩肘两段几何，手掌跟随握柄投影，两人独立、暂停冻结，重物摇得更慢。木握柄独立采样原 crank 的 UV 保持侧视厚度。

长绳和引绳使用同一麻绳网格，含暗边、暖麻色、亮边与绞股；实际收放驱动纹理移动。钩顶挂环连接绳端，开闭保持作者宽高比并随绳转向。新 Graphic 最初缺 CanvasRenderer，生成器断言未发现但截图显示绳消失，已补组件及真实 renderer mesh/material 检查。独立审查另发现原纹理循环把长度和偏移抵消，已改从固定上端生成。

`GoldMinerMechanicsChecks.Run` 最终 **70/70 PASS**，覆盖实际上传网格、开闭钩/挂环、固定横轴、两人各 0/90/180/270° 截图、手掌握点、暂停、双人隔离、绳纹移动及 HUD 避让。3.50→3.49 世界单位时，长绳/引绳实际上移 0.8653872/0.8653777 UI，暂停不动；两人头部实际网格距 HUD 下沿各 8.416687 UI。近景可见作者拆分袖口描边，正常游戏尺寸下不再出现悬浮大管或缺失手臂。证据位于 `artifacts/gold-miner-development/mechanics/`；红/绿日志留在 `artifacts/gold-miner-refinement-20261028/`。

### 8.3 资源与旁路回归

补充导入保持 v1 的 44 PNG/43 slices/37 Alpha 固定契约，v2 清单仅允许三个指定 ID→group；覆盖字节、尺寸、哈希、GUID、正式目标无原图时复用、冲突不覆盖与幂等。资源测试 **398 checks PASS**，真实 Unity 导入 **44 原 PNG + 43 切片 + 3 v2 sprites PASS**。输入状态机 **54 项**，公共/2048 Core **43 项**、输入 **10 项**、持久化 **13 项**本轮复跑通过。独立审查提出的短屏商店、长屏暂停和纹理偏移三项已修复并加入运行回归。

本轮干净构建使用专属 `artifacts/gold-miner-refinement-20261028/build-project/`，首次从空 Library 导入，不复用其他工程缓存。`source-manifest.json` 记录 469 个生产源码/资源/配置文件（不计 meta），构建副本逐项匹配；实际导入器另复读 PNG、GUID、切片和平台设置。

## 9. 2026-10-28 Android 实际运行与进度保护

本轮证据集中于 `artifacts/gold-miner-refinement-20261028/`。物理手机为 PKM110 / Android 16 / ARM64，竖屏 1080×2373；专属模拟器为 `emulator-5560`，双人横屏 2373×1080。使用独立 `com.duanyao.xanaduyonder.goldminertest`，没有覆盖正式应用或其他任务测试包。

### 9.1 物理手机

- 实际检查大厅庆祝矿工、模式选择、第 2 关成功结算、商店、第 3 关 Ready、游戏、暂停和返回确认。截图位于 `device/01..09`；模式页完整截图为 `09-selection.png`，`02-selection.png` 实际拍到已进入的成功结算。
- 两次实际出钩及连续四帧大金块回收：`07-solo-motion-0..3.png`。手臂和握柄位置随回收变化，木框固定；绳纹、斜向钩爪及挂环可见。存档实体 4 为大金块，146 tick 内绳长 4.0863695469→2.6263695469，实算 **0.6 世界单位/秒**；随后回收到顶，现金 3930→4430。截图与每帧持久化快照均保留。
- 测试手机后来被另一项任务切换至 `com.duanyao.xanaduyonder.dinotest`，前台包名日志已确认；立即停止触摸，剩余双人操作使用专属模拟器。误拍到的其他应用截图已删除，不计作黄金矿工证据。本轮不声称物理手机已完成双人同时操作或人工十关通关。
- 本任务可控操作前的 v2 快照为单人第 2 关已成功、现金 3680，合作第 1 关失败、现金 860。操作后只停止矿工测试包并恢复这一快照及原 `.bak`，主文件与备份 SHA-256 逐项相同，记录于 `device/restored-sha256.txt`。保留更早 v1 备份供兼容验证，没有倒退覆盖已获得的第 2 关成功进度。

### 9.2 模拟器兼容与双指输入

- 将真实 v1 存档副本放入专属模拟器，打开单人继续后停在暂停页；逐字段比较升级后的实际磁盘文件，**仅 rulesVersion 1→2**，现金 2360、剩余 2221 tick、正在回收的石头和另一模式槽均不变。证据为 `emulator/migrated-before-play.json` 与 `04-old-save-paused.png`。
- 继续合作失败档，重试至 Ready，使用 shell `NativeTouch dual` 注入真正的 `ACTION_DOWN → POINTER_DOWN → POINTER_UP → UP`，两指相隔 2 ms。`07-dual-hooks.png` 同帧显示两人独立回收，持久化后 P1/P2 各收入 100、团队现金 200；未通过私有测试入口伪造游戏操作。
- 设备结果与恢复哈希的自动核对记录于 `device-validation.json`。声音、触感主观体验及长期性能没有在本轮重新验收；不沿用旧版数据冒称当前版本结果。

### 9.3 最终安装包

- 文件：`artifacts/builds/XanaduYonder-0.4.0-gold-miner-refined.apk`；101,121,552 字节，约 96.4 MiB；版本 0.4.0 / code 4，独立测试包名，APK v2 签名验证通过。
- SHA-256：`5cf1b36db12fc8a49527385d90473bbcd0fe274365bc58a1c800c781d9be954f`。手机最终安装成功后，直接计算其 `/data/app/.../base.apk` 哈希，与本地交付文件一致；再次验证主存档及备份未变化。安装仅更新矿工测试包，没有切换手机当前前台任务。
- 最后一次 UI 修正后的增量 APK 被结构检查拒绝：检测到旧 ZIP 数据间隙，报告 `apk-incremental-rejected.json`。清除专属构建副本的 Library/Temp/obj/旧 APK 后重新导入及构建，`final-clean-import.log` 和 `final-clean-build.log` 均成功退出。最终结构检查 **PASS**，相对同基线无矿工包增量 38,444,132 字节（36.663181 MiB），在 40 MiB 门槛内；废弃中间包不用于交付。
- 最终 469 项源码/资源/配置哈希与构建副本逐项相同，记录于 `source-manifest.json`、`delivery.json`。最终 APK 在专属模拟器再次打开原有合作失败存档，`emulator/08-final-coop-failed.png` 证明 2373×1080 下矿工帽子、木牌标题与全部控件完整可见。模拟器验证结束后退出，临时构建副本清理，日志/哈希/截图/APK 保留。


## 10. 2026-10-28 累计曲线与完整抓持图

本轮仅在 `0053/XanaduYonder` 隔离工作树实施，没有提交、推送或发布。经济、素材、显示和独立复核分工完成，最终 Unity/Android 验证统一由主 agent 执行。

### 10.1 行为及边界

- 保留累计钱包与商店扣款，规则版本升为 3。新单人挑战目标为 `1200,3000,5100,7600,10400,13600,17200,21200,25600,30400`；合作为独立双倍。第三关为 5100，不再沿用 2550。
- 旧挑战本关、重试和后续关卡全部保留旧曲线；新开挑战才应用新曲线。独立审查发现旧第9关以15150进入新第10关30400会超过全场可得金额，已经修复并以54个版本/模式/跨关组合回归。不会提高已有挑战的目标或伪造补偿资金。
- 240张矿图全部重新见证，0张改动实体/几何；仍证明全部购买、无增益/炸药、袋子100的保守预算。26条见证使用已有合法倒计时后收线，最长70.5秒，所有出钩都在60秒前。
- 难度测量是2400个固定策略局与960条真实Session挑战，不是玩家胜率。新曲线改善前期，熟练策略仍可能积攒大量盈余；不宣称后期难度全面解决。逐关收入、剩余压力和兼容记录见 `gold-miner/economy-v3.md`。
- 九类完整抓钩+物品PNG替换原运行时叠图，隐藏Held物品的独立Image。所有图按实测吊环外径归一并连接同一绳端；保留绳角度旋转。抱钻动物改为双爪与钻石画在同一张图中，行走/抓持均无独立悬浮钻石。
- 抓取后立即用炸药的特效中心从命令前快照计算，覆盖尚未Render的新负载及旧显示位置；动物翻转时按实际朝向/旋转补偿可见中心。
- 矿工手臂本轮没有改动，`GoldMinerWinchView.cs` 与上一验收快照 SHA 一致。此前握点几何通过不能证明袖子连贯。建议同母版6帧完整矿工+手臂+摇柄+机器循环，固定轴心/底座/绳出口，尚未实施。

### 10.2 实际验证

| 层级 | 本轮结果 | 证据 |
| --- | --- | --- |
| Core / Persistence | 50 / 31 组通过；含54个旧挑战跨关组合，红测试先复现晚期旧档必败 | `artifacts/gold-miner-followup-20261028/economy-compat-review/` |
| Levels / Catalog / Campaign | 9600 / 22 / 1257 检查通过，240真实重放、20完整关、85持久事务、8重载 | 同目录 `economy/` 与 `economy-compat-review/` |
| 输入路由 | 54检查通过 | `input.log` |
| 素材 | 501检查通过；10/10新图源与正式PNG字节一致，v1/v2约束保留 | `assets-final.log`、`production-assets-v3-caught.json` |
| Unity实际导入 | 44原图+3补充+10新图、43切片、ETC2设置/pivot通过 | `unity-import-green.log` |
| Unity表现 | 926断言通过；九类×三角度×双玩家、暂停/重建、模型不变、空钩环径/绳端、未Render即炸 | `caught-mechanics-final.log`、`mechanics-final/` |
| Unity完整页面 | 16286断言通过；20次实际进出无纹理数量增长 | `ui-final.log`、`ui-report-final.txt` |
| 最终Android模拟器 | Android16 ARM64，专用5560。新单人目标1200，实际抓住中金并收回入账250；合作目标2400，NativeTouch真实双指使两钩同时出钩，P1抓到大金并最终入账500 | `emulator/05-solo-ready.png`、`10-solo-loaded-0.png`、`11-pause.png`、`14-coop-ready.png`、`15-dual-hooks.png`、`dual-touch.log` |
| 物理手机 | PKM110独立包覆盖安装成功，已装base.apk与最终APK SHA相同；主存档与.bak前后哈希一致 | `phone-install.log`、`phone-installed.json`、`device-before/` |

Unity早期导入曾出现JsonUtility把无环null数组读为空数组，纯C#测试未覆盖；现明确允许无环的null/空数组并重新实际导入通过。一次编辑器启动记录QuickSearch索引自身异常，不在运行时代码栈，完整UI测试仍正常退出并产出PASS。最终模拟器Unity/AndroidRuntime日志没有应用异常。

27张真实抓持渲染已逐组视觉检查，预览页位于 `artifacts/gold-miner-followup-20261028/caught-art/preview.html`。九类组合由内置 image_gen 分别绘制，原始RGBA不经程序改图；全部提示、SHA及实测标定保存在正式v3清单。行走抱钻图目前用完整图起伏，并非新四帧序列。

物理手机前台由另一任务的 `happyballtest` 使用，本轮没有切换前台或进行物理触屏游戏回归；安装前后仍是同一前台任务。模拟器证明Android运行与原生触摸路由，不能代替物理手机手感、长时帧率、听感及完整10关难度验收。

### 10.3 最终独立测试APK

- 文件：`artifacts/builds/XanaduYonder-0.4.0-gold-miner-caught-v3.apk`
- 包名：`com.duanyao.xanaduyonder.goldminertest`，versionName `0.4.0` / code `4`，Development、ARM64、Android SDK26+；v2签名校验通过。
- 字节：**101,679,272**，**96.969 MiB**；SHA-256：`c2b32cd5f30b650efab371655dbd0f0e47cb759afc8574e8930045b7ecc6c31b`。
- 从独立空Library副本导入并构建，最终C#源哈希与验证源一致。对同配置b793fe8无黄金矿工基线，APK差 **39,001,852字节 / 37.195 MiB**，活跃压缩条目差38,988,433字节，ZIP间隙检查通过，低于40 MiB预算。
- 不是安装后空间、内存或运行帧率测量。`apk-size.json`、`clean-import.log`、`clean-build.log` 与 `source-manifest.json` 保留；临时构建副本和本任务专用模拟器已清理/停止。


## 11. 完整矿工六帧动画（2026-10-28）

用户授权后已将旧拆分手臂、独立摇柄及鼓面组件替换为 P1/P2 各六帧完整矿工与绞盘图。绳末端按源像素 pivot 注册，双人独立播放；轻载约 0.60–0.617 秒一圈、重载约 0.95–0.967 秒一圈，30/60/120Hz 均覆盖。暂停、不动、伸出与首次重建不补播。素材与提示见 `gold-miner/production-assets-v4-reel.json`，开发问题和规范见 `gold-miner-development-notes.md`。

旧实现红测试为 3/929 失败；新实现 Unity Mechanics 1096 项通过、Play Mode UI 16278 项通过、资产契约最终 595 项通过。Unity importer 实际导入 59 张 PNG、55 个多图集切片。单人全部六帧与合作双方十二帧实际引擎截图经过人工逐张检查，袖口/手套/握柄连贯，机座固定。六帧仍是卡通离散动画；不能据此声称连续机械运动或任意设备性能达标。

干净构建的独立开发包 `XanaduYonder-0.4.0-gold-miner-whole-animation.apk` 为 104614908 字节，SHA-256 `0e051d57fcb2e489e2fed0c2efe2e366c6865a5746006d886682c7428ed3143a`，签名验证通过。相对不含本游戏的同基线干净 APK 增量 39.994705 MiB。已安装 OPPO PKM110 独立测试包，设备 APK 与本地 SHA 一致；安装前该测试包已不存在，未恢复旧备份。模拟器只完成安装和启动，后续交互检查按用户明确要求停止，不将其记为完整设备回归。

日志、18 张动画截图、UI截图与 APK 记录位于忽略目录 `artifacts/gold-miner-whole-animation-20261028/`。用户已确认当前体验没有问题，随后要求停止全部检验、合并 master 并发布 v0.6.0；此后仅执行 Git 交付和必要构建/签名/发布完整性操作，不继续测试或试玩。
