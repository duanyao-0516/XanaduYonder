# 扫雷开发准备核查

日期：2026-10-27。本文维护实现入口与剩余验证事项，配合[完整玩法与接入设计](superpowers/specs/2026-10-27-minesweeper-design.md)、[实施计划](superpowers/plans/2026-10-27-minesweeper-implementation.md)及[验证记录](verification-minesweeper.md)使用。扫雷生产模块、宿主接线、独立测试、UI 检查和隔离构建入口已实现；代码存在与测试入口可执行不等于设备验收完成。

工作树为 `/Users/bytedance/.codex/worktrees/minesweeper/XanaduYonder`，分支为 `p/duanyao.0516/feature/minesweeper`；建树时核对的开发基线是 `master` / `origin/master` 的 `ca50db0f381b96c7a024586792bdc60eb3f0fd3c`。不将该历史核对描述为远端始终未变。

## 1. 已实现与待验证

| 状态 | 当前结论 |
| --- | --- |
| 已实现 | 三档规则与快照、首击安全、Session/Profile、终局统计、严格 JSON 与原子存档、整帧手势、50%～200% 视口、全屏/总览、独立反馈与宿主接线 |
| 已有入口 | 四套直接编译生产源码的独立测试、扫雷 UGUI 检查、七款大厅断言、隔离 Android 构建；新增按钮布局按 44 dp 换算并在需要时采用两行 |
| 待交付确认 | 最新修改后的 Unity/UI 与宿主集成结果、Android IL2CPP 构建、真机多指/实际尺寸/生命周期/存档、已有六款游戏回归及最终审查；具体完成项只以验证记录为准 |

「已有工具路径」不等于 Unity 已可成功构建；「已有基线测试通过」不等于扫雷测试通过。详细玩法以主设计为准，本清单不另设一套规则。

## 2. 已核实的环境与工程配置

准备阶段的只读核查确认下列工具路径，`.NET --version` 当时返回 `10.0.403`。此表记录安装基线，不承诺后续 Unity 许可、构建或设备验证已经通过；实施阶段的运行结果单列在验证记录。

| 项目 | 路径或配置 |
| --- | --- |
| Unity | `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity` |
| Android 模块根目录 | `/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer`，与 `Unity.app` 同级 |
| IL2CPP / SDK / NDK / JDK | 上述模块目录下的 `Variations/il2cpp`、`SDK/platforms`、`SDK/build-tools`、`NDK/source.properties`、`OpenJDK/bin/java` |
| adb | 上述模块目录下的 `SDK/platform-tools/adb` |
| .NET | `/Users/bytedance/.dotnet/dotnet`，准备阶段只读版本输出 `10.0.403` |
| 应用配置 | `0.4.0`、Android versionCode `4`；ARM64、IL2CPP、竖屏、minSdk `26`、targetSdk `36`、旧 Input Manager |
| 现有包 | Unity Newtonsoft JSON `3.2.2`、UGUI `2.0.0`、Unity Test Framework `1.6.0`；独立持久化测试引用 Newtonsoft.Json `13.0.2` |

源码依据：[ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt)、[ProjectBuild](../Assets/Editor/ProjectBuild.cs)、[Packages/manifest.json](../Packages/manifest.json)、[扫雷独立持久化测试项目](../Tests/MinesweeperPersistence/MinesweeperPersistenceTests.csproj)。安装布局说明见[开发说明](development.md)。现有 [check-environment.sh](../scripts/check-environment.sh) 还会调用 adb 并读取已连接设备；工具路径核查与该脚本完整运行结果分别记录。

## 3. 模块目录与宿主接线

以下生产目录与独立测试项目已存在，均位于当前扫雷工作树。

| 实现位置 | 责任 |
| --- | --- |
| `Assets/Scripts/Core/Minesweeper/` | `MinesweeperGame.cs`、`MinesweeperSnapshot.cs` 及规则类型；三档规格、随机源注入、邻域、翻格/插旗/快速展开、胜负与快照不变量；不引用 Unity |
| `Assets/Scripts/Runtime/Minesweeper/` 的普通 C# | `MinesweeperSession.cs`、`MinesweeperProfile.cs`、`MinesweeperSaveData.cs`、`MinesweeperGesture.cs`、`MinesweeperInputTypes.cs`、`MinesweeperViewport.cs`、`MinesweeperControlLayout.cs`、`MinesweeperSaveCodec.cs`、`MinesweeperSaveStore.cs`；计时/暂停、记录、手势、视口和磁盘边界，可由独立测试直接编译 |
| 同目录的 Unity 适配 | `MinesweeperPage.cs`、`MinesweeperPointer.cs`、`MinesweeperBoardView.cs`、`MinesweeperIconGraphic.cs`、`MinesweeperFeedback.cs`；页面已按 Layout/Dialogs 分文件，不把胜负或存档解析放进 UI |
| `Tests/Minesweeper/`、`Tests/MinesweeperSession/`、`Tests/MinesweeperInput/`、`Tests/MinesweeperPersistence/` | 直接编译同一份生产源码的 .NET 测试项目；视口与按钮布局数学纳入 Input/Session，不另写仅供测试的实现 |

接入沿用 [AppController](../Assets/Scripts/Runtime/AppController.cs) 的现有 Page 模式，已按以下边界接入，仍需逐项集成验证：

- **初始化与大厅**：追加第七张固定高度卡，维护 `Page` 枚举、页面引用和大厅记录刷新。可在初始化时读取 Profile 供大厅显示，但只有用户开始游戏才创建局，不能因启动、预览或统一 Save 生成一局。输入许可、声音/触感开关、返回大厅、设置、保存状态由宿主注入。
- **页面接口**：沿用 `Initialize`、`Enter`、`Leave`、`Suspend`、`SetForeground`、`Back`、`Save`、`CheckEndState` 的职责；通过 `Changed` 刷新大厅、`SaveError` 暴露保存错误。这些接口已由扫雷页面实现；宿主负责公共设置，页面负责自己的对局与临时弹窗。
- **切页**：进入扫雷先让其他页面离开；进入任一旧游戏或大厅时让扫雷离开。`Leave/Suspend/Save` 幂等，取消输入、停止模块音频并捕获当前模型，不能重复统计或恢复计时。
- **暂停与设置**：`OnApplicationPause/Focus` 更新前台状态；扫雷为当前页时由宿主直接 `CloseModal()` 清理公共设置，不执行用户主动关闭回调。页面关闭自己的临时弹窗，并按原规则/暂停状态恢复：Active 需继续，Ready 保留原手动暂停语义，Won/Lost 回终局查看。
- **返回与全屏**：公共弹窗优先，随后扫雷内部按总览→全屏→难度页→大厅处理。全屏切换只取消扫雷当前输入和重排视图，不能调用会暂停整局的宿主 `StopAnimation`。
- **状态与反馈**：统一 Save/退出通知覆盖扫雷；`SetStatus` 与大厅错误汇总纳入扫雷。全屏隐藏宿主状态区时，模块内仍展示同一保存错误。`GameFeedback` 只新增通用 `Pulse(bool enabled)` 薄入口，不能加入扫雷音色或页面类型判断。

参考：[SudokuPage](../Assets/Scripts/Runtime/Sudoku/SudokuPage.cs) 的页面注入与前台处理、[WatermelonPointer](../Assets/Scripts/Runtime/Watermelon/WatermelonPointer.cs) 的原触点取消核查、[GameFeedback](../Assets/Scripts/Runtime/GameFeedback.cs) 的 Android 触感桥。扫雷现由 [MinesweeperPointer](../Assets/Scripts/Runtime/Minesweeper/MinesweeperPointer.cs) 收集整帧触点并交给纯状态机仲裁，同时抑制真实触摸期间的合成鼠标；真实设备分发仍需验收。

## 4. 状态、存档与素材的固定决定

- 颜色、字体、标题与导航沿用现有 Xanadu 样式；总览图标固定为四个向内箭头。主要操作按钮由 `MinesweeperControlLayout` 换算 44 dp 目标并按空间单/双行排布，不改变全应用 `UiFactory`。
- 正常缩放为 `0.5..2`，低于 100% 仍可操作；100% 基准格目标至少 32 dp，用户缩至 50% 可约 16 dp。命中使用实际已夹紧的渲染变换，视口空白不映射到边缘格。
- 9×9 的总览/全屏按钮保留位置但禁用，旧初级全屏档恢复为普通布局；中高级全屏按局保存，总览只观察和定位，Active 继续计时。`centerColumn/centerRow` 保存期望中心，纯布局夹紧不覆盖它；总览倍率和临时返回快照不进入正常视图存档。
- 独立文件为 `minesweeper-v1.json` 及其 `.bak/.tmp`。最终棋盘、冻结时间、`resultRecorded` 和本档记录同次捕获；失败重试只重写，不重新结算。解码必须先验证完整 Profile，读主档失败再读备份，禁止拼接两份版本的记录和棋盘。
- 文件读写已由 [MinesweeperSaveCodec](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSaveCodec.cs) / [MinesweeperSaveStore](../Assets/Scripts/Runtime/Minesweeper/MinesweeperSaveStore.cs) 独立实现：限制字节/深度、严格 UTF-8 与 token、先编码校验再 flush 临时文件、原子替换、损坏主档不能覆盖合法备份。扫雷具体字段与不变量以主设计第 11 节为准，不改其他游戏协议。
- 首版无待下载素材：原创 UGUI 棋盘与旗/雷/错旗图形，复用 [UiFactory](../Assets/Scripts/Runtime/UiFactory.cs)、[UiIconGraphic](../Assets/Scripts/Runtime/UiIconGraphic.cs) 与现有 Noto 字体。Kenney 候选不引入。`MinesweeperFeedback` 按主设计第 8 节自行合成并管理短音，不复用会读取 Jump 资源的 `Miss/Land`；暂停、离开、关闭声音时停止，销毁时释放音频。

## 5. 实施顺序与验证分界

规则、状态/存档、输入与页面已按以下依赖顺序落地；检查结果和剩余交付项在实施计划、验证记录中逐项维护：

1. 固定规则/快照与随机源、单调时钟及数据边界，建立三档规则测试；手势和视口数学可在同一契约下并行实现。
2. 接上 Session/Profile 与真实 Codec/Store，验证暂停恢复、终局一次结算、低倍率视图往返和损坏备份；此时无需 Unity 页面。
3. 实现 Pointer、BoardView、Feedback 与普通/全屏/总览页面，再落实宿主全部接线；页面只消费前面已定义的模型和结果。
4. 扩展真实 UGUI 检查和既有大厅断言；完成 Unity 导入/编译与 Play Mode 后再构建隔离 APK。
5. 在独立包完成 Android 验收与已有游戏回归，记录证据；源码实现、独立检查、Unity、APK 和真机分别记录，不以此清单替代验收记录。

## 6. 测试入口

命令从本工作树根目录执行。以下入口已存在；是否已执行以及结果以验证记录为准：

| 现有入口 | 用途 |
| --- | --- |
| `scripts/test-core.sh`、`scripts/test-input.sh`、`scripts/test-persistence.sh` | 2048/共享输入/公共存档基线 |
| `scripts/test-jump.sh`、`scripts/test-jump-persistence.sh` | 跳一跳规则与存档回归 |
| `scripts/test-sheep.sh`、`scripts/test-sheep-persistence.sh`、`scripts/test-sheep-layouts.sh`、`scripts/test-sheep-session.sh`、`scripts/test-sheep-presentation.sh`、`scripts/test-sheep-difficulty.sh` | 旸了个旸已有规则、存档、会话、布局与表现检查 |
| `scripts/test-idiom.sh`、`scripts/test-idiom-persistence.sh`、`scripts/test-idiom-resources.sh`、`scripts/test-idiom-ui.sh` | 成语已有规则、存档、资源与 UI 检查 |
| `scripts/test-sudoku.sh`、`scripts/test-sudoku-content.sh --bank`、`scripts/test-sudoku-persistence.sh`、`scripts/test-sudoku-session.sh`、`scripts/test-sudoku-profile.sh`、`scripts/test-sudoku-selection.sh` | 数独已有规则、题库、存档、会话与选择策略检查 |
| `scripts/test-watermelon.sh`、`scripts/test-watermelon-persistence.sh`、`scripts/test-watermelon-physics.sh` | 合成大西瓜已有规则、存档与 Unity 物理检查 |
| `scripts/test-minigame-ui.sh` | 公共 UI/大厅检查；已将顺序断言扩展至七款，保留原用例 |

扫雷专用测试入口已实现：

```bash
scripts/test-minesweeper.sh
scripts/test-minesweeper-session.sh
scripts/test-minesweeper-input.sh
scripts/test-minesweeper-persistence.sh
scripts/test-minesweeper-ui.sh
```

独立测试沿用 `net10.0`、C# 7.3、直接 Compile 生产文件、warnings-as-errors。脚本模板见 [test-watermelon.sh](../scripts/test-watermelon.sh)、[test-watermelon-persistence.sh](../scripts/test-watermelon-persistence.sh)；项目模板见 [SudokuSessionTests.csproj](../Tests/SudokuSession/SudokuSessionTests.csproj)、[WatermelonPersistenceTests.csproj](../Tests/WatermelonPersistence/WatermelonPersistenceTests.csproj)。持久化测试使用真实 Newtonsoft.Json `13.0.2` 和临时目录。

[MinesweeperUiChecks](../Assets/Editor/MinesweeperUiChecks.cs) 已提供内存 Profile、真实 raycast、生产页面渲染与截图，复用 [MiniGameUiChecks](../Assets/Editor/MiniGameUiChecks.cs) 的隔离方式。失败重试检查只用显式临时目录；不能读取或清空日常玩家存档。[test-minigame-ui.sh](../scripts/test-minigame-ui.sh) 需要图形渲染，不加 `-nographics`；不要和同一工作树的 Unity 编辑器/构建并行占用工程。它不能证明 Android 原生取消、多指分发、物理 dp 或 IL2CPP 行为。

## 7. 独立验证 APK 与文件要求

以下构建链已实现并已实际构建成功，包名、版本、哈希和验证边界见[扫雷验证记录](verification-minesweeper.md)：

```text
scripts/build-minesweeper-validation.sh
  → XanaduYonder.EditorTools.ProjectBuild.BuildAndroidMinesweeperValidation
  → com.duanyao.xanaduyonder.minesweepertest
  → artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk
```

复用 [build-watermelon-validation.sh](../scripts/build-watermelon-validation.sh) 与 [ProjectBuild.BuildAndroidWatermelonValidation](../Assets/Editor/ProjectBuild.cs) 的真实结构：脚本指定 Unity 和本树 projectPath，检查退出码及非空 APK，日志写 `artifacts/logs/build-minesweeper-validation.log`；Editor 入口在 `Prepare()` 后临时设置独立包名/产品名，以 `Development | StrictMode` 构建，在 `finally` 恢复原包名与产品名并保存。保持 `0.4.0`/code `4`，不改发布签名、不创建 Release、不覆盖正式应用。

安装启动必须指定实际设备序列号和隔离 APK。下面是本次产物成功生成后的安装/启动命令，执行状态以验证记录为准：

```bash
ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.4.0-minesweeper-validation.apk
"$ADB" -s DEVICE_SERIAL shell am start -n com.duanyao.xanaduyonder.minesweepertest/com.unity3d.player.UnityPlayerActivity
```

现有 [install-android.sh](../scripts/install-android.sh) 虽接受 APK 参数，但启动包名固定为正式应用，不能直接充当上述隔离包的安装启动流程；此处无需改造该脚本。

沿用 [Bootstrap.unity](../Assets/Scenes/Bootstrap.unity) 与现有 [Core](../Assets/Scripts/Core/XanaduYonder.Core.asmdef)、[Runtime](../Assets/Scripts/Runtime/XanaduYonder.Runtime.asmdef)、[Editor](../Assets/Editor/XanaduYonder.Editor.asmdef) 程序集。Core 保持 `noEngineReferences=true`；Editor 已通过 [AssemblyInfo](../Assets/Scripts/Runtime/AssemblyInfo.cs) 获得 Runtime internal 访问，不需新场景或 asmdef。新增 Assets 文件/目录提交唯一 `.meta`，不改既有 GUID；独立 Tests 工程文件在 `Tests/` 下维护。构建、截图、报告和测试 bin/obj 遵循 [.gitignore](../.gitignore)，不提交 APK、Library、临时玩家数据或授权文件。

## 8. 验证证据与交付判断

[扫雷验证记录](verification-minesweeper.md) 是实施阶段命令、退出码、报告与限制的统一入口。四套扫雷生产源码测试共 107 组、实际 Unity 扫雷界面 3144 条断言、共享界面 209 条断言均通过；已有游戏核心/共享能力回归及 Android IL2CPP APK 构建也已执行。设备输入和真人多指体验不由上述结果代替。

准备阶段曾在此工作树执行 `scripts/test-core.sh` 43/43、`scripts/test-input.sh` 10/10、`scripts/test-persistence.sh` 13/13，均退出 0；这 66 项仅为当时已有功能的基线。准备阶段的网页交互示例也仅证明原型中的小格、全屏和总览交互，不能当作 Unity 或设备证据。

已执行独立规格与代码审查，发现的问题已修复并重跑直接受影响的检查。设备验收继续重点覆盖真实多指竞争与取消、50% 命中、实际 32/44 dp 尺寸、两指转单指、缩放边界、全屏中心往返、总览计时、共享设置后台关闭与终局不复活。具体已验证场景以验证记录为准；没有对应设备证据的项目保持待验证，不将本开发分支称为新的正式发布。
