# 07 黄金矿工实施与验收计划

> **For agentic workers:** 使用 `superpowers:subagent-driven-development` 或 `superpowers:executing-plans` 逐任务实施；勾选项仅代表该项所写范围已有源码与执行证据；未勾选项保留尚未执行或证据不足的验收边界。

**Goal:** 在 Xanadu 接入单人竖屏与双人横屏合作的 10 关黄金矿工，复用已交付美术，实现可靠的实时输入、商店、续局和 Android 验证。

**Architecture:** 纯 C# Core 拥有固定步模拟、连续碰撞、关卡、经济和状态快照；Unity 页面只处理布局、输入、表现和生命周期。存档独立、交易先持久化再提交，黄金矿工使用独立屏幕方向作用域和局部弹窗，不改变其他六款游戏的布局规则。

**Tech Stack:** Unity 6000.3.27f1、C# 7.3 兼容源码、.NET 10.0.403 独立测试、项目现有 Newtonsoft.Json、UGUI、Android ARM64/OpenGLES3。

## 0. 文档与执行边界

依据：[玩法与数值](../specs/2026-10-27-gold-miner-design.md)、[技术契约](../../gold-miner-technical.md)、[资源接入](../../gold-miner-assets.md)。冲突按“玩法规则→技术字段/调用顺序→资源显示”处理，修订时同步文档和测试。

截至 2026-10-27，Core、Runtime 页面、资源导入、240 张矿图与见证、独立测试和隔离 APK 已实现。当前分支已合并 `origin/master`，HEAD 为 `b793fe8`，版本仍为 0.4.0。本轮没有提交、推送或发布 Release。PKM110 物理手机仍锁屏，尚未安装本次 APK；专用 `emulator-5560` 已安装并开展集成检查，不能替代物理双指、系统生命周期和人工难度验收。

已执行摘要：Core 47、Input 54、Persistence 28、Assets 347、Levels 9579、Catalog 22 组（两模式十关共 2000 个固定种子样本）；全部 240 张矿图通过生产 Core 见证重放；Campaign 960 个检查通过，两模式各十关经真实 Session/Store 到达 Completed（无购买基线），另含全道具购买、真实失败和付费检查点重试分支，合计 80 次落盘事务、8 次冷加载。生成统计的 228 条来自历史控制台记录，12 条来自字节一致的重新生成，后者耗时不冒充历史耗时。Unity UI 最新 15,449 个断言通过，公共 minigame UI 205 个断言通过；构建设置的正常/异常恢复已在 Unity 中通过，包含最新长屏修复的最终源码经干净构建得到 98,662,338 bytes（94.09 MiB）APK，相同配置干净基线的增量为 35,984,918 bytes（34.317892 MiB），低于 40 MiB 目标；产物 SHA-256 与构建方法见任务 H，构建后 ProjectSettings 无 Git 差异。该干净 APK 已在 emulator-5560 通过 2373×1080 商店和真实 IO 失败购买原子性复验，具体证据见任务 H。

证据入口：[验证记录](../../verification-gold-miner.md)、`artifacts/validation/gold-miner/levels.json`、`generation-archive.json`、`artifacts/gold-miner-development/ui/report.txt` 与 `artifacts/logs/`。下列未覆盖项不因已有实现、自动见证或构建成功而勾选。

继续使用 `p/duanyao.0516/codex/gold-miner`。已核对工作区与真实 master 并合并到 `b793fe8`；后续远端变化仍先评估，不覆盖他人工作。实施完成后交付本地代码和隔离验证包；提交、推送、正式版本号和 Release 按后续明确请求处理。

## 1. 文件与责任

以下为**已落地的文件与责任边界**；修改现有文件限定在对应任务所列位置。

| 位置 | 文件 | 唯一职责 |
| --- | --- | --- |
| `Assets/Scripts/Core/GoldMiner/` | `GoldMinerRules.cs`, `GoldMinerTypes.cs` | 数值表、枚举、轻量数值结构；不依赖 Unity |
| 同上 | `GoldMinerGame.cs`, `GoldMinerCollision.cs` | 固定步、摆钩/伸出/回收、连续首次接触和唯一归属 |
| 同上 | `GoldMinerLevelGenerator.cs`, `GoldMinerPlanner.cs` | 离线有界生成与见证重放；不在手机运行规划 |
| 同上 | `GoldMinerEconomy.cs`, `GoldMinerSession.cs`, `GoldMinerSnapshot.cs` | 商店、两模式会话、事务候选、检查点和快照合法性 |
| 同上 | `GoldMinerInputState.cs` | 不依赖 Unity 的指针归属/去重状态，供独立输入测试编译同一源码 |
| `Assets/Scripts/Runtime/GoldMiner/` | `GoldMinerPage.cs` 及 Layout/Dialogs 分部、`GoldMinerLayout.cs`, `GoldMinerOrientationScope.cs` | 页面状态、横竖屏布局、方向/全屏资源作用域 |
| 同上 | `GoldMinerInputRouter.cs`, `GoldMinerSceneView.cs` | 双人指针分发、模拟结果到画面映射 |
| 同上 | `GoldMinerArtwork.cs`, `GoldMinerAudio.cs` | 按需加载图像、切片/动画及确定性短音 |
| 同上 | `GoldMinerLevelCatalog.cs` | 校验并加载当前关卡的已验证目录，按种子选图 |
| 同上 | `GoldMinerSaveCodec.cs`, `GoldMinerSaveStore.cs` | 严格 JSON 解析、原子写入、合法备份与失败提示 |
| `Assets/Editor/` | `GoldMinerAssetImporter.cs`, `GoldMinerUiChecks.cs`, `GoldMinerBuildSettingsScope.cs` | 清单驱动导入、真实 Unity UI/布局验证与临时构建设置恢复 |
| `Assets/Resources/GoldMiner/` | 清单中 44 张 PNG、`Levels/` 参考矿图与见证 | 图片原字节保留；导入设置写 `.meta`；只打包生产素材 |
| `Tests/` | `GoldMiner/`, `GoldMinerInput/`, `GoldMinerPersistence/`, `GoldMinerLevels/`, `GoldMinerCatalog/`, `GoldMinerAssets/`, `GoldMinerCampaign/`, `GoldMinerDevice/` | 同一生产源码的规则、指针、真实解析/磁盘、生成验证 |
| `scripts/` | 规则/输入/存档/关卡/目录/资源/UI/构建脚本与设备触控工具 | 独立测试、Unity UI 检查、隔离 APK 构建 |

测试项目沿用 `Tests/Watermelon/WatermelonTests.csproj` 的显式 `Compile Include`、`LangVersion=7.3`、警告视作错误配置。持久化测试使用现有同版本真实 Newtonsoft.Json，不能用另一套简化解析器模拟。

## 2. 任务 A：冻结数值和纯模型

创建 Core 的 `GoldMinerRules.cs`、`GoldMinerTypes.cs`、`GoldMinerSnapshot.cs`，以及 `Tests/GoldMiner/Program.cs`、`GoldMinerTests.csproj`、`scripts/test-gold-miner.sh`。

- [x] 将玩法稿表格逐项转为只读规则表；规则版本固定 1、10 关、3600 tick，快照不持有 Unity 对象。
- [x] 已覆盖数值断言：单人第 1/10 关目标为 650/18300，合作为 1040/29280；店 L=1 全购费用 670，L=9 为 1390；携钻动物受油加成为 930；石头书不改变重量。
- [x] `scripts/test-gold-miner.sh` 当前 47 项通过；首轮实现前的 red-run 不在现有证据中，不能追溯宣称已记录。
- [x] 为非法 mode、关卡 0/11、负数钱、NaN/Infinity 坐标、重复 ID 和两个钩同时持有同一对象增加独立拒绝用例。拒绝时原模型不能被部分修改。

所有后续模块共用以下操作契约，方法细节以技术稿为准：

```csharp
// Game：只在允许模拟时由页面调用，每次恰好推进 1/60 秒。
session.StartLevel(); // 仅 Ready -> Running；之后才接受新的矿区 Down。
game.TryLaunch(playerId, commandSeq);
game.TryUseDynamite(playerId, commandSeq);
game.Step();
var snapshot = game.GetSnapshot();
// Session：生成完整候选；持久化失败不得 Commit。
var candidate = session.PreparePurchase(itemId, recipientPlayerId);
if (store.TrySave(candidate.Snapshot, out error)) session.Commit(candidate);
```

这是已实现调用链的顺序示意；实际保存事务传入 `candidate.Snapshot`，`Commit(candidate)` 仍验证事务和页面版本。

## 3. 任务 B：钩子、动物和结算模拟

创建 `GoldMinerGame.cs`、`GoldMinerCollision.cs`，扩展 `Tests/GoldMiner/Program.cs`。

- [x] 用“空钩触边回收”“金块重量影响耗时”“两钩同物件争抢”“两物件同路径取最近”“运动动物与钩尖相向穿过”五组几何夹具先固定预期；连续检测必须覆盖一帧内穿透，不依赖端点重叠。
- [x] 实现固定步次序：处理本步合法命令→动物/钩子轨迹→统一接触排序/认领→回收入账→扣 tick→必要时进入等待回收/结算。触边反向或本步到达钩轴要切分该步剩余时间，不能丢失或重复运动。
- [x] 验证最后 1 tick 发射可以完成本次动作，0 tick 新发射被拒；到时仍伸出的钩可命中并回收；两钩全部结束才产生一次结算。
- [x] 验证带载炸药扣 1、负载无收入、切空钩回收；空钩/摆动/无库存时调用无效；同一 `commandSeq` 重复不能连扣。
- [x] 暂停 gate 阻止 Step；Core 对相同快照逐步比较钩状态/金额/序号，Persistence 对合作伸出和带载状态恢复后继续真实模拟，比较完整编码快照。
- [x] 已补完整快照、Step 事件和累计入账账本的逐 tick SHA-256 对比：Solo 3651 tick、Coop 3723 tick，全字段序列化精确一致（无浮点容差放宽），并输出最终散列；Core 当前 47 项通过。
- [x] 运行 `scripts/test-gold-miner.sh`，只有失败原因解决后进入下一任务。

## 4. 任务 C：关卡生成、参考图和难度见证

创建 `GoldMinerLevelGenerator.cs`、`GoldMinerPlanner.cs`、`Tests/GoldMinerLevels/Program.cs`、`GoldMinerLevelsTests.csproj`、`scripts/test-gold-miner-levels.sh` 和 `Assets/Resources/GoldMiner/Levels/`。

- [x] 将玩法稿的组成表、格点公式、图案、抖动、xorshift32 和袋子抽取顺序落实；记录每个参考图的 mode/level/rulesVersion/种子/完整坐标及输入 tick 序列。
- [x] 已用 240 张已保存候选的实际种子重复构造并逐坐标比较；运行时固定种子、独立袋子流和检查点重试均有测试。
- [x] 已用 `1、7、42、20261027、uint.MaxValue` × 两模式 × 首关三图案完成 30 对完整生产 Generate 调用，完整矿图/见证及候选/展开次数一致，60 次新见证通过；报告为 `artifacts/validation/gold-miner/generation-reproducibility.json`。本项范围为首关，第 2～10 关沿用全部生产图重放，不宣称重复全量生成。
- [x] 按玩法稿的最紧资金缺口离线运行规划器，导出两模式×10 关×三图案×4 变体共 240 张参考图及成功见证；袋子按 100、无增益/无炸药重放。任何模式/关卡/图案缺图即为阻塞，不以放宽断言或简单提高矿物总值放行。
- [x] 创建 `GoldMinerLevelCatalog.cs`，运行时按稳定 ID 排序并排除前关图案，首关从 12 张、其余从 8 张中选；测试目录坏档拒绝开局且不消耗种子。目录读取与 CPU 规划分开，手机不执行搜索。
- [x] 固定运行时样本为每模式每关 100 个由固定公式派生的种子，共 2000 局，验证图案去重、数量/边界、相同种子选择和袋子金额一致；离线另强制制造 32 次候选失败，要求构建报错且不得输出未验证资源。
- [x] 四叶草继承、固定袋子 RNG、检查点重试和恢复不重抽已有 Core/Catalog/Persistence 覆盖。
- [x] 普通/四叶草各 100 万次固定种子抽样通过：仅取 100/250/500、每袋消耗一次 RNG，频率分别符合 60/30/10 与 20/40/40，各桶误差小于 0.4 个百分点；暂停、冷恢复、采集和检查点重试不重抽已覆盖。
- [x] 执行 `scripts/test-gold-miner-levels.sh`，输出 `artifacts/validation/gold-miner/levels.json`，含图 ID、seed、候选次数、生成失败原因和见证收益；历史记录未单独测量的 planner-only 耗时保留缺失，12 张重现图记录本轮耗时。生成统计来源见 `generation-archive.json`。

## 5. 任务 D：商店、两模式会话与持久化

创建 `GoldMinerEconomy.cs`、`GoldMinerSession.cs`，及 Runtime 的 `GoldMinerSaveCodec.cs`、`GoldMinerSaveStore.cs`；创建 `Tests/GoldMinerPersistence/Program.cs`、`GoldMinerPersistenceTests.csproj`、`scripts/test-gold-miner-persistence.sh`。

- [x] 先写真实磁盘夹具覆盖两模式快照、钩子在途、截止等待回收、商店已购、失败重试检查点和终关；编码/解码后使用同一 Core 继续动作，结果一致。
- [x] 完成 Prepare→保存完整候选→Commit 流程；包含 `PrepareEnterShop` 的 Won→Shop 初始化和 `PreparePurchase` 的扣款/发货。各交易只在本状态有效，旧 UI 回调和重复点击不得再扣款/发货/计通关。注入写盘失败后比较内存与磁盘，二者仍是交易前状态。
- [x] 对模拟中的自动保存失败保留内存局和 dirty 提示，定期重试；切后台同步尝试，返回大厅不销毁唯一内存局；不能声称已落盘。
- [x] 非法文件测试逐项包含：缺失字段、null、数字类型错误、重复键、未知版本、超深/超长、负值和溢出、断裂实体引用、重复归属、错误检查点、伪造商店库存、终局与计数不一致。
- [x] 原子替换故障注入覆盖写临时文件、flush、replace；坏主档回退合法备份；两者都坏时保留原文件并要求显式开始新挑战，不能自动覆写。独立存档不能改写六款旧游戏和公共设置文件。
- [x] 运行 `scripts/test-gold-miner.sh`、`scripts/test-gold-miner-persistence.sh` 和 `scripts/test-gold-miner-levels.sh`；失败用例和实际报告归入验证目录。
- [x] `scripts/test-gold-miner-campaign.sh` 960 个检查通过：无购买基线的两模式各十关真实完成；独立全道具分支验证购买、次关增益、真实失败、冷加载和付费检查点重试，80 次落盘事务、8 次冷加载。证据为 `artifacts/validation/gold-miner/campaign.json`；不宣称带全增益十关完成，也不代替人工试玩。

## 6. 任务 E：资源导入、动画与音效

创建 `GoldMinerAssetImporter.cs`、`GoldMinerArtwork.cs`、`GoldMinerAudio.cs`；正式资源目标见 [44 项清单](../../gold-miner/production-assets-v1.json)。

- [x] 核验源 ZIP 与 44 个 PNG 的 SHA-256；按清单复制原字节并生成 `.meta`。角色和 UI 图集用清单矩形切片，不均分，不导入整页界面稿和原 ZIP 的未选音频。
- [x] 按资源稿配置 Sprite 类型、PPU、网格、过滤、Alpha、纹理尺寸和 ETC2；记录九宫格边界及关键锚点。清单是预设，导入后以 Unity 实际 Sprite.bounds/像素预览验证。
- [x] 拼装矿工、绞盘、钩爪、动物携钻，按状态驱动姿态和程序动效；动态数字、文字、绳索由运行时绘制，不能把截图当游戏背景。
- [x] 实现资源稿的独立短音/噪声生成与音量上限，共用声音/触感开关；生成 clip 的释放成对完成。关闭声音、暂停、离开时立刻停止当前音效。
- [x] 已测 Unity 编辑器单人/合作/商店资源样本和 20 次进出释放：完整 GC 后 allocated 237,221,437 → 235,299,749 bytes，Texture2D 数量 664 → 664；44 PNG 源文件共 51,711,510 bytes。该样本只证明本次编辑器检查范围。
- [x] 相同配置的干净 APK 对照通过：基线 62,677,420 bytes，最终包 98,662,338 bytes，增量 35,984,918 bytes（34.317892 MiB），低于 40 MiB 目标；方法与 ZIP 活动条目核验见任务 H。
- [ ] 实测 Android 帧时间、内存/纹理驻留；APK 包体结果和编辑器样本不代替 Android 性能验收。

## 7. 任务 F：横竖屏、双人输入和 UI

创建 `GoldMinerLayout.cs`、`GoldMinerOrientationScope.cs`、`GoldMinerInputRouter.cs`、`GoldMinerSceneView.cs`、`GoldMinerPage.cs`，以及纯 C# 的 `GoldMinerInputState.cs`；测试为 `Tests/GoldMinerInput/Program.cs`、`GoldMinerInputTests.csproj`、`scripts/test-gold-miner-input.sh`。Runtime Router 把 Unity 事件转换为 InputState 的数值事件，独立测试直接编译 InputState，不用 Unity 假实现替代真正的分发逻辑。

- [x] 为指针路由先建立以下事件表，逐条比较命令列表：左右同时 Down 各发射一次；左 Down 后移到右 Up 不再发射；同侧第二指不重复发射；UI Down 后移入矿区不发射；暂停前已按住的指针不能在恢复时发射；Canceled 只清理归属。
- [x] Runtime 有效 Down 分配玩家，UI/取消/恢复隔离与键盘辅助均进入相同 Core 命令入口；Input 54 项和 Unity 真实 UGUI raycast 检查已通过。
- [ ] 物理 Android 原生双指同时操作、UI 不穿透和取消恢复验收；构造 PointerEventData、鼠标或模拟器输入不替代本项。
- [x] 方向作用域进入合作锁定 LandscapeLeft，等尺寸和安全区稳定后显示准备页；期间无输入无计时。模式选择保持 Portrait。退出/销毁/失败恢复捕获的旧配置，不给其他游戏遗留横屏。
- [x] 黄金矿工建立独立根布局和局部弹窗；背景覆盖可绘制区，HUD/炸药/确认按钮在安全区内，世界等比完整呈现。不能直接把公共竖屏 modal 或 720×1320 框缩成横屏。
- [x] 已实现设计稿对应的模式选择、单/双玩法、商店、成功失败、暂停、准备、帮助和确认状态；Unity 截图与状态检查覆盖这些页面，结算等待保留必要回收动画。
- [ ] 在物理手机完成视觉与动作体验验收；编辑器状态覆盖不等于全部设计稿的设备视觉签收。
- [x] 运行 `scripts/test-gold-miner-input.sh`，再与任务 B–D 测试联跑，证明指针只产生预期 Core 命令且不会绕过暂停/截止 gate。

## 8. 任务 G：大厅与公共生命周期接入

修改 `Assets/Scripts/Runtime/AppController.cs` 的 Page、固定高度大厅卡片、页面进入/退出、Save、Pause/Focus、返回键路由；修改 `Assets/Editor/ProjectBuild.cs` 增加独立验证入口；不将具体 Gold 规则塞入 `UiFactory` 或现有游戏模型。

- [x] 第 07 卡片进入模式选择，卡高和原六张顺序保持，滚动可到达全部卡片；仅在首次进入时加载黄金矿工资源。
- [x] OnApplicationPause/Focus 先关闭输入 gate、清指针、暂停音效，再结清事件前已累计的完整模拟步、关闭模拟 gate并保存；不丢前台完整步、不补跑后台时间，恢复先呈现暂停层。返回键优先关闭帮助/确认，再暂停，最后返回模式选择/大厅。
- [x] 公共设置仍由原 AppController 存储，黄金矿工订阅读取，不另存第二份设置；横屏使用自身局部面板发出相同设置操作。
- [x] 构建新增 `ProjectBuild.BuildAndroidGoldMinerValidation`，以 `GoldMinerBuildSettingsScope` 的 using/finally 还原 Player/Quality/Editor settings（含图标、启动页、方向、graphics、keystore）、5 个构建用户标志与构建场景。真实 Unity `RunValidation` 已验证正常和模拟异常两条恢复路径。隔离包名 `com.duanyao.xanaduyonder.goldminertest`，输出 `artifacts/builds/XanaduYonder-0.4.0-gold-miner-validation.apk`；保留当前正式 Version，不在此任务中发布新版。
- [x] 新建 `scripts/build-gold-miner-validation.sh`，继承现有脚本的 Unity 精确路径、退出码处理、非空产物检查与独立日志 `artifacts/logs/build-gold-miner-validation.log`。

## 9. 任务 H：Unity、Android 与回归验收

创建 `Assets/Editor/GoldMinerUiChecks.cs`、`scripts/test-gold-miner-ui.sh`、`docs/verification-gold-miner.md`。后者只能填写实际执行结果，失败和未执行必须保留。

- [x] Unity 检查共 15,449 个断言通过：四种尺寸的纯布局/安全区夹具，标准横竖屏真实 UGUI 截图与 raycast，双人控件、坐标映射、弹窗隔离、方向等待、前台积压补算、真实保存/商店写盘失败及重试；Running/Shop 非空错误提示完整可见、在安全区内且不覆盖按钮和贡献文字。
- [x] 生产长屏商店布局已修复；Unity 720×1582、1582×720 的真实 Shop 截图、完整文本生成、文字不重叠及安全区按钮检查通过。
- [x] 最终干净 APK 的 Android 模拟器 2373×1080 商店复验通过，已刷新 `35-final-coop-shop.png`，确认文字完整、无重叠。
- [ ] 物理刘海/系统导航栏变化、设备视觉和触控验收仍未关闭。
- [x] 执行下列新增命令，均要求退出码 0；任一失败阻止进入设备交付。Unity 日志不得含编译异常或缺图/丢失字体错误。

```bash
scripts/test-gold-miner.sh
scripts/test-gold-miner-input.sh
scripts/test-gold-miner-persistence.sh
scripts/test-gold-miner-campaign.sh
scripts/test-gold-miner-levels.sh
scripts/test-gold-miner-catalog.sh
scripts/test-gold-miner-assets.sh
scripts/test-gold-miner-ui.sh
scripts/build-gold-miner-validation.sh
```

- [x] 专用 emulator-5560 已完成 Android 原生双指、跨中线/同侧双指去重、后台冻结、进程冷启动；合作首关 Won（3150），商店两笔购买及重复点击去重后进入第二关 Ready。证据在 `artifacts/gold-miner-development/emulator/`；模拟器输入不代替物理触控体验。
- [x] 模拟器实际坏主档+坏备份恢复已验证：确认前保留原文件，明确确认后归档两个 17 字节原文件且字节完整，再进入新 Ready；测试结束已恢复原测试存档。
- [ ] 显式选择 `adb devices -l` 中物理设备，再安装上述隔离 APK 并启动 `.goldminertest/com.unity3d.player.UnityPlayerActivity`；不能用普通安装脚本覆盖正式应用。验证包独立数据目录不迁移正式存档。
- [ ] 按技术稿验收矩阵逐项记录双指同时操作、争抢、到时回收、连续出钩、炸药按钮不穿透、横屏进出、暂停/锁屏/后台/进程重启、商店重复点击和坏档恢复；至少各完整通关一次单人/合作，人工试玩记录独立于规划器见证。
- [x] 最终源码 APK（含长屏修复）干净构建成功：98,662,338 bytes（94.09 MiB），SHA-256 `e3c930856b858766f9527c37c92eb9b3d59b2daabb57d45ba41177e2033f45e1`；`artifacts/logs/build-gold-miner-final-clean.log` 记录 `Build Finished, Result: Success` 和 `Exiting batchmode successfully`，构建后 ProjectSettings 无 Git 差异。
- [x] APK 增量使用两个独立干净副本串行构建：基线来自 `git archive b793fe8`，使用最终构建入口/设置作用域，仅移除 Gold 资源导入调用；最终副本复制当前 `Assets/Packages/ProjectSettings` 及 `docs/gold-miner/production-assets-v1.json` 清单。双方仅保留相同 `Library/EditorUserBuildSettings.asset`，不带 Bee/Gradle 缓存或旧输出 APK。`aapt badging` 与签名证书 SHA-256 相同；`comparison-clean.json` 的 CRC、无重复条目、Gold 类型/矿图标记正反检查与活动 local header 间隙核验通过，最大 4096 bytes 间隙均为正常签名块，非签名间隙为 0。基线 SHA-256 为 `b4b64edb5b85a887b1c98d8eefb6d9f8b36892714d680c587677847a14e57cf5`；对照工具与报告位于 `artifacts/gold-miner-development/apk-size-comparison/`。此前克隆 Library 的对照受陈旧资源/ZIP 数据污染，`comparison.json` 已标为 `INVALIDATED`，不采用其增量。
- [x] 最终干净 APK 已安装 emulator-5560，商店真实 IO 失败原子性再次通过：以 `.tmp` 空目录阻断写入，购买 P2 炸药时磁盘/资金 3150/库存余 3 不变且错误清晰；移除目录后重试，txn 4→5、资金 3150→3080、P2 炸药 1→2，P1/单人槽未变，错误消失。证据为 `artifacts/gold-miner-development/emulator/final-shop-write-failure-report.json`、`final-shop-before/failed/retry.json` 与 `35/36/37-final*.png`；报告已绑定上述新 SHA-256，截图已刷新为干净包结果。
- [x] 最终干净 APK 从 Shop 进入第二关 Ready 后开始，Android 原生双指（两次 Down 相隔 10 ms）使命令 seq 60→62，单人槽完全不变；`39-final-native-dual.png` 显示 P1 已收获 500、P2 正在回收，矿场完整渲染。快照与输入日志为 `final-native-before/after.json`、`final-native-dual.log`。
- [ ] 完成物理设备型号/系统、所装 APK SHA-256、源码状态、操作与截图/日志的完整对应记录；Android 帧时间、内存仍须实测。
- [x] `test-minigame-ui.sh` 公共 shell 编辑器回归 205 个断言通过；覆盖大厅顺序及 Jump/Sudoku/Sheep 相关真实 UI 回调和显示。
- [x] `test-core.sh`、`test-input.sh`、`test-persistence.sh` 本轮实际运行分别 43/10/13 项通过，已在验证记录列明。
- [x] 专用模拟器已完成旧六游戏入口打开/返回冒烟，另执行 2048 滑动和 Sudoku 开局；不是旧六游戏完整玩法验收。
- [ ] 六款旧游戏的物理设备进入/返回、暂停继续、声音触感和原存档续局验收。
- [x] 已完成实现独立审查并修正输入 gate 重入、帧时间采样、无变化重复保存/超额读取分配、Draining 恢复上限和验证构建设置泄漏；受影响自动检查已重跑，README、architecture/development 和第三方资源说明已更新。
- [ ] 关闭尚缺的设备与人工验收项，并在验证记录中保留每一层证据和限制后完成整体验收。

## 10. 可以开始与可以交付的标准

**当前是实现与自动验证已落地、设备/人工验收尚未全部关闭的状态。** 参考矿图生成、生产 Core 见证、资源导入、游戏闭环、Unity 检查、独立 APK 与相同配置干净包体增量已有证据；未勾选的 Android 性能、物理输入/系统生命周期、两模式人工十关体验仍未完成。规划器可达性证明不代替难度体验。

实现交付要求任务 A–H 的适用项全部有证据：两种模式完整闭环，全部参考图和抽样种子通过真实 Core 重放，商店/存档失败无重复收益，退出方向正确，六款原游戏回归通过，独立 APK 实机验收完成。不自动提交远端、创建标签或发布 Release。
