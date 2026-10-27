# 扫雷开发准备核查

日期：2026-10-27。本文是开发入口与验证清单，配合[完整玩法与接入设计](superpowers/specs/2026-10-27-minesweeper-design.md)使用，不是已实施的编码计划。规则、交互、存档和素材决策已足以开工；扫雷生产代码、测试入口及验证包仍待实现。

工作树为 `/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`，分支为 `p/duanyao.0516/feature/minesweeper`；建树时核对的开发基线是 `master` / `origin/master` 的 `ca50db0f381b96c7a024586792bdc60eb3f0fd3c`。不将该历史核对描述为远端始终未变。

## 1. 已齐备、待实施、待验证

| 状态 | 当前结论 |
| --- | --- |
| 已齐备 | 三档规则、首击安全、终局统计、单/双指仲裁、50%～200% 正常缩放、全屏、只读总览、恢复策略和独立存档契约；宿主、测试与构建模板有真实源码支撑 |
| 待实施 | 扫雷模块、宿主接线、通用触感薄入口、各层测试、隔离验证构建入口；后文新增路径和接口名称均属此类 |
| 待验证 | Unity 导入/编译与授权、真实 UGUI 分发、Android IL2CPP 构建、真机多指/尺寸/生命周期/存档、已有六款游戏回归 |

「已有工具路径」不等于 Unity 已可成功构建；「已有基线测试通过」不等于扫雷测试通过。详细玩法以主设计为准，本清单不另设一套规则。

## 2. 已核实的环境与工程配置

本轮只读核查确认下列路径存在，Unity 可执行文件可执行，`.NET --version` 返回 `10.0.403`；未启动 Unity、验证 Unity 授权或运行设备命令。

| 项目 | 路径或配置 |
| --- | --- |
| Unity | `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity` |
| Android 模块根目录 | `/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer`，与 `Unity.app` 同级 |
| IL2CPP / SDK / NDK / JDK | 上述模块目录下的 `Variations/il2cpp`、`SDK/platforms`、`SDK/build-tools`、`NDK/source.properties`、`OpenJDK/bin/java` |
| adb | 上述模块目录下的 `SDK/platform-tools/adb` |
| .NET | `/Users/bytedance/.dotnet/dotnet`，本轮只读版本输出 `10.0.403` |
| 应用配置 | `0.4.0`、Android versionCode `4`；ARM64、IL2CPP、竖屏、minSdk `26`、targetSdk `36`、旧 Input Manager |
| 现有包 | Unity Newtonsoft JSON `3.2.2`、UGUI `2.0.0`、Unity Test Framework `1.6.0`；独立持久化测试引用 Newtonsoft.Json `13.0.2` |

源码依据：[ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt)、[ProjectBuild](../Assets/Editor/ProjectBuild.cs)、[Packages/manifest.json](../Packages/manifest.json)、[独立持久化测试项目](../Tests/WatermelonPersistence/WatermelonPersistenceTests.csproj)。安装布局说明见[开发说明](development.md)。现有 [check-environment.sh](../scripts/check-environment.sh) 还会调用 adb 并读取已连接设备；本轮没有将路径检查冒充该脚本完整运行通过。

## 3. 模块目录与宿主接线

以下目录和扫雷文件均待新建；仅已有参考源码使用链接。

| 拟新增位置 | 责任 |
| --- | --- |
| `Assets/Scripts/Core/Minesweeper/` | `MinesweeperGame.cs`、`MinesweeperSnapshot.cs` 及规则类型；三档规格、随机源注入、邻域、翻格/插旗/快速展开、胜负与快照不变量；不引用 Unity |
| `Assets/Scripts/Runtime/Minesweeper/` 的普通 C# | `MinesweeperSession.cs`、`MinesweeperProfile.cs`、`MinesweeperGesture.cs`、`MinesweeperViewport.cs`、`MinesweeperSaveCodec.cs`、`MinesweeperSaveStore.cs`；计时/暂停、记录、手势、视口和磁盘边界，可由独立测试直接编译 |
| 同目录的 Unity 适配 | `MinesweeperPage.cs`、`MinesweeperPointer.cs`、`MinesweeperBoardView.cs`、`MinesweeperIconGraphic.cs`、`MinesweeperFeedback.cs`；页面可按 Layout/Dialogs 分文件，不把胜负或存档解析放进 UI |
| `Tests/Minesweeper/`、`Tests/MinesweeperSession/`、`Tests/MinesweeperInput/`、`Tests/MinesweeperPersistence/` | 直接编译同一份生产源码的 .NET 测试项目；视口数学纳入 Input/Session，不另写仅供测试的实现 |

接入沿用 [AppController](../Assets/Scripts/Runtime/AppController.cs) 的现有 Page 模式，实施时落实以下边界：

- **初始化与大厅**：追加第七张固定高度卡，维护 `Page` 枚举、页面引用和大厅记录刷新。可在初始化时读取 Profile 供大厅显示，但只有用户开始游戏才创建局，不能因启动、预览或统一 Save 生成一局。输入许可、声音/触感开关、返回大厅、设置、保存状态由宿主注入。
- **页面接口**：沿用 `Initialize`、`Enter`、`Leave`、`Suspend`、`SetForeground`、`Back`、`Save`、`CheckEndState` 的职责；通过 `Changed` 刷新大厅、`SaveError` 暴露保存错误。这些是拟实现接线契约，不是已经存在的扫雷 API；具体参数在实施计划中固定。
- **切页**：进入扫雷先让其他页面离开；进入任一旧游戏或大厅时让扫雷离开。`Leave/Suspend/Save` 幂等，取消输入、停止模块音频并捕获当前模型，不能重复统计或恢复计时。
- **暂停与设置**：`OnApplicationPause/Focus` 更新前台状态；扫雷为当前页时由宿主直接 `CloseModal()` 清理公共设置，不执行用户主动关闭回调。页面关闭自己的临时弹窗，并按原规则/暂停状态恢复：Active 需继续，Ready 保留原手动暂停语义，Won/Lost 回终局查看。
- **返回与全屏**：公共弹窗优先，随后扫雷内部按总览→全屏→难度页→大厅处理。全屏切换只取消扫雷当前输入和重排视图，不能调用会暂停整局的宿主 `StopAnimation`。
- **状态与反馈**：统一 Save/退出通知覆盖扫雷；`SetStatus` 与大厅错误汇总纳入扫雷。全屏隐藏宿主状态区时，模块内仍展示同一保存错误。`GameFeedback` 只新增通用 `Pulse(bool enabled)` 薄入口，不能加入扫雷音色或页面类型判断。

参考：[SudokuPage](../Assets/Scripts/Runtime/Sudoku/SudokuPage.cs) 的页面注入与前台处理、[WatermelonPointer](../Assets/Scripts/Runtime/Watermelon/WatermelonPointer.cs) 的原触点取消核查、[GameFeedback](../Assets/Scripts/Runtime/GameFeedback.cs) 的 Android 触感桥。扫雷还需整帧双指仲裁与触摸合成鼠标去重，不能把单指参考直接复制后视为完成。

## 4. 状态、存档与素材的固定决定

- 正常缩放为 `0.5..2`，低于 100% 仍可操作；100% 基准格目标至少 32 dp，用户缩至 50% 可约 16 dp。命中使用实际已夹紧的渲染变换，视口空白不映射到边缘格。
- 全屏按局保存；总览只观察和定位，Active 继续计时。`centerColumn/centerRow` 保存期望中心，纯布局夹紧不覆盖它；总览倍率和临时返回快照不进入正常视图存档。
- 独立文件为 `minesweeper-v1.json` 及其 `.bak/.tmp`。最终棋盘、冻结时间、`resultRecorded` 和本档记录同次捕获；失败重试只重写，不重新结算。解码必须先验证完整 Profile，读主档失败再读备份，禁止拼接两份版本的记录和棋盘。
- 文件读写参考 [WatermelonSaveCodec](../Assets/Scripts/Runtime/Watermelon/WatermelonSaveCodec.cs) / [WatermelonSaveStore](../Assets/Scripts/Runtime/Watermelon/WatermelonSaveStore.cs)：限制字节/深度、严格 UTF-8 与 token、先编码校验再 flush 临时文件、原子替换、损坏主档不能覆盖合法备份。扫雷具体字段与不变量以主设计第 11 节为准，不改其他游戏协议。
- 首版无待下载素材：原创 UGUI 棋盘与旗/雷/错旗图形，复用 [UiFactory](../Assets/Scripts/Runtime/UiFactory.cs)、[UiIconGraphic](../Assets/Scripts/Runtime/UiIconGraphic.cs) 与现有 Noto 字体。Kenney 候选不引入。`MinesweeperFeedback` 按主设计第 8 节自行合成并管理短音，不复用会读取 Jump 资源的 `Miss/Land`；暂停、离开、关闭声音时停止，销毁时释放音频。

## 5. 依赖实施顺序

这是依赖顺序，不是完成状态或逐函数编码计划：

1. 固定规则/快照与随机源、单调时钟及数据边界，建立三档规则测试；手势和视口数学可在同一契约下并行实现。
2. 接上 Session/Profile 与真实 Codec/Store，验证暂停恢复、终局一次结算、低倍率视图往返和损坏备份；此时无需 Unity 页面。
3. 实现 Pointer、BoardView、Feedback 与普通/全屏/总览页面，再落实宿主全部接线；页面只消费前面已定义的模型和结果。
4. 扩展真实 UGUI 检查和既有大厅断言；完成 Unity 导入/编译与 Play Mode 后再构建隔离 APK。
5. 在独立包完成 Android 验收与已有游戏回归，记录证据；只有验证完成后更新“已实现/已通过”，不以此清单替代验收记录。

## 6. 测试入口

命令从本工作树根目录执行。以下现有入口已核对存在；除第 8 节注明的历史基线外，本轮未执行：

| 现有入口 | 用途 |
| --- | --- |
| `scripts/test-core.sh`、`scripts/test-input.sh`、`scripts/test-persistence.sh` | 2048/共享输入/公共存档基线 |
| `scripts/test-jump.sh`、`scripts/test-jump-persistence.sh` | 跳一跳规则与存档回归 |
| `scripts/test-sheep.sh`、`scripts/test-sheep-persistence.sh`、`scripts/test-sheep-layouts.sh`、`scripts/test-sheep-session.sh`、`scripts/test-sheep-presentation.sh`、`scripts/test-sheep-difficulty.sh` | 旸了个旸已有规则、存档、会话、布局与表现检查 |
| `scripts/test-idiom.sh`、`scripts/test-idiom-persistence.sh`、`scripts/test-idiom-resources.sh`、`scripts/test-idiom-ui.sh` | 成语已有规则、存档、资源与 UI 检查 |
| `scripts/test-sudoku.sh`、`scripts/test-sudoku-content.sh --bank`、`scripts/test-sudoku-persistence.sh`、`scripts/test-sudoku-session.sh`、`scripts/test-sudoku-profile.sh`、`scripts/test-sudoku-selection.sh` | 数独已有规则、题库、存档、会话与选择策略检查 |
| `scripts/test-watermelon.sh`、`scripts/test-watermelon-persistence.sh`、`scripts/test-watermelon-physics.sh` | 合成大西瓜已有规则、存档与 Unity 物理检查 |
| `scripts/test-minigame-ui.sh` | 公共 UI/大厅检查；待加入扫雷用例并更新六款顺序断言，保留原用例 |

拟新增命令如下，创建前不可执行、不可声称已通过：

```bash
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-input.sh
scripts/test-minesweeper-persistence.sh
```

独立测试沿用 `net10.0`、C# 7.3、直接 Compile 生产文件、warnings-as-errors。脚本模板见 [test-watermelon.sh](../scripts/test-watermelon.sh)、[test-watermelon-persistence.sh](../scripts/test-watermelon-persistence.sh)；项目模板见 [SudokuSessionTests.csproj](../Tests/SudokuSession/SudokuSessionTests.csproj)、[WatermelonPersistenceTests.csproj](../Tests/WatermelonPersistence/WatermelonPersistenceTests.csproj)。持久化测试使用真实 Newtonsoft.Json `13.0.2` 和临时目录。

[MiniGameUiChecks](../Assets/Editor/MiniGameUiChecks.cs) 已提供内存 fixture、真实 raycast、720×1320/1582 渲染及截图。扫雷 UI/Play Mode 用内存 Profile、无 Store 或显式临时目录，不能读取或清空日常玩家存档。[test-minigame-ui.sh](../scripts/test-minigame-ui.sh) 需要图形渲染，不加 `-nographics`；不要和同一工作树的 Unity 编辑器/构建并行占用工程。它不能证明 Android 原生取消、多指分发、物理 dp 或 IL2CPP 行为。

## 7. 独立验证 APK 与文件要求

以下构建链已确定，仍待实现：

```text
scripts/build-minesweeper-validation.sh
  → XanaduYonder.EditorTools.ProjectBuild.BuildAndroidMinesweeperValidation
  → com.duanyao.xanaduyonder.minesweepertest
  → artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk
```

复用 [build-watermelon-validation.sh](../scripts/build-watermelon-validation.sh) 与 [ProjectBuild.BuildAndroidWatermelonValidation](../Assets/Editor/ProjectBuild.cs) 的真实结构：脚本指定 Unity 和本树 projectPath，检查退出码及非空 APK，日志写 `artifacts/logs/build-minesweeper-validation.log`；Editor 入口在 `Prepare()` 后临时设置独立包名/产品名，以 `Development | StrictMode` 构建，在 `finally` 恢复原包名与产品名并保存。保持 `0.4.0`/code `4`，不改发布签名、不创建 Release、不覆盖正式应用。

安装启动必须指定实际设备序列号和隔离 APK。下面是产物生成后的待执行命令，本轮没有运行：

```bash
ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk
"$ADB" -s DEVICE_SERIAL shell am start -n com.duanyao.xanaduyonder.minesweepertest/com.unity3d.player.UnityPlayerActivity
```

现有 [install-android.sh](../scripts/install-android.sh) 虽接受 APK 参数，但启动包名固定为正式应用，不能直接充当上述隔离包的安装启动流程；此处无需改造该脚本。

沿用 [Bootstrap.unity](../Assets/Scenes/Bootstrap.unity) 与现有 [Core](../Assets/Scripts/Core/XanaduYonder.Core.asmdef)、[Runtime](../Assets/Scripts/Runtime/XanaduYonder.Runtime.asmdef)、[Editor](../Assets/Editor/XanaduYonder.Editor.asmdef) 程序集。Core 保持 `noEngineReferences=true`；Editor 已通过 [AssemblyInfo](../Assets/Scripts/Runtime/AssemblyInfo.cs) 获得 Runtime internal 访问，不需新场景或 asmdef。新增 Assets 文件/目录提交唯一 `.meta`，不改既有 GUID；独立 Tests 工程文件在 `Tests/` 下维护。构建、截图、报告和测试 bin/obj 遵循 [.gitignore](../.gitignore)，不提交 APK、Library、临时玩家数据或授权文件。

## 8. 验证证据与交付判断

| 已有证据 | 能证明的范围 |
| --- | --- |
| 之前轮次 `scripts/test-core.sh` 43/43、`scripts/test-input.sh` 10/10、`scripts/test-persistence.sh` 13/13，退出 0 | 已有生产代码共 66 项基线检查；不是扫雷测试，也不是六款完整回归，本轮没有重新运行 |
| 本轮工具路径只读核查与 `.NET --version = 10.0.403` | 工具安装路径及 .NET 版本；不证明 Unity 授权、导入、编译或 APK 构建可用 |
| 真实宿主/输入/存档/测试/构建源码审计及设计交叉审阅 | 接入边界与方案可实施；不证明运行时效果 |
| 本轮网页交互原型：JS 语法检查通过，浏览器无 error；实际点击 100%→75%→50%，50% 禁用缩小，小格插旗/拔旗正常，点击倍率回 100%；75% 进入全屏及总览往返保留倍率和模式；320 px 窄屏 50% 全屏布局检查无控件重叠 | 仅证明网页原型的上述交互与布局；不是 Unity UI、Android 输入分发或真机验收 |

待实施后按主设计验收矩阵分别记录：独立测试命令/退出码、Unity 编译与 Play Mode 报告、APK 路径/构建日志、设备型号/系统/包名与实际操作结果。重点覆盖 50% 小格命中、同帧多指竞争、双指转单指、边界缩放、全屏中心往返、总览计时、公共设置后台关闭、终局不复活及备份恢复不重复统计。

本轮没有扫雷 Unity 编译结果、Unity UI 截图验收、APK 或真机结果；尚待完成的这些实现和验证项不表示设计条件缺失。实现时如发现新的接口或设备约束，先更新主设计与本清单对应项，再据实记录验证范围。
