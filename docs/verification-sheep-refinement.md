# 旸了个旸 v0.3.0 玩法与品牌修订验证记录

验证日期：2026-10-26。Unity 6000.3.27f1，Android ARM64 / IL2CPP，版本号仍为 0.3.0 / versionCode 3。本记录对应当前修订包；[初版验证记录](verification-sheep.md)保留为历史证据。

物理机原用户存档已恢复，并通过恢复后启动及 Home 返回的最终 SHA-256 核验。下文分别记录规则测试、构建、设备交互和启动画面证据。

## 本轮范围

- 移出、撤回、洗牌不限次数。移出区仍最多 6 张；撤回只恢复最近一次未消除的取牌，保留准确来源和移出区顺序，不提供多步历史回滚。三消、移出、洗牌会清除该次撤回记录。免费复活仍为每关一次。
- 新挑战关选择布局 3..14，共 12 种分离且错位叠放的结构；连续重开排除上一布局。旧布局 0..2 的几何和牌 ID 保持不变，已有存档继续原局，重开后才选择新布局。
- 棋盘高度调整为 630 设计单位，移出区与卡槽下移；卡槽标题和牌槽保留上下内边距。盲堆显示扇开的空白牌背，不泄露下层图案。
- 使用用户提供的 `XY_logo.png` 作为应用图标及自有启动标识；Unity 启动标识关闭，启动背景为白色。Android 12 系统启动窗口也使用应用图标。

## 规则、存档与布局测试

同一份生产 C# 源码通过以下独立测试，总计 **172 组，0 失败**：

| 命令 | 通过组数 |
|---|---:|
| `scripts/test-core.sh` | 43 |
| `scripts/test-input.sh` | 10 |
| `scripts/test-persistence.sh` | 9 |
| `scripts/test-jump.sh` | 25 |
| `scripts/test-jump-persistence.sh` | 18 |
| `scripts/test-sheep.sh` | 31 |
| `scripts/test-sheep-persistence.sh` | 26 |
| `scripts/test-sheep-layouts.sh` | 10 |

新增规则与存档用例覆盖重复移出、移出区容量、反复取牌与撤回、重复洗牌、复活独立且仅一次，以及历史 used 字段经过 JSON 和真实磁盘恢复后不再限制三种普通道具。`sheep-v1.json` 保留原字段协议，前三个 used 字段仅记录曾使用过，`ReviveUsed` 仍控制复活次数。

布局测试覆盖两关旧布局的几何指纹及 ID 顺序、新增布局的 144 张牌数、边界、同层无重叠、多岛间隙、独立盲堆和结构差异；检查连续重开排除上一布局，并通过真实 `SheepGame.Pick` 重放 1,000 条新挑战关解法。核心测试另含 600 条生成解法和逐步存档恢复。生成解法证明初始局存在通关路径，普通洗牌仍不保证任意局面均可解。

## 构建与品牌资源

最终开发包与正式签名包均构建成功。构建入口分别为 `scripts/build-android.sh` 和 `scripts/build-release.sh`，日志位于本地 `artifacts/logs/build-android.log` 与 `artifacts/logs/build-android-release.log`。

| 产物 | 字节数 | SHA-256 |
|---|---:|---|
| `artifacts/builds/XanaduYonder-0.3.0.apk` | 58,840,817 | `4e2bf4952c544285a7f58bf50f22951736f2096ba997311c7c7d0e5b35e4035d` |
| `artifacts/builds/XanaduYonder-0.3.0-android-arm64.apk` | 38,079,832 | `ae2dc714c69370189083e4f47f4708092555b9f6ff6508e7009dd0d61e45f1bf` |

正式包已检查签名、包元数据和架构：包名 `com.duanyao.xanaduyonder`，版本 0.3.0 / code 3，minSdk 26、targetSdk 36、ARM64，未带 `application-debuggable`。发布证书 SHA-256 为 `e0d7e0da7eb12d1f03154356b7d3b3c7aaf6c1c07cd16e98d39623e833fdaf51`，沿用现有发布签名。本次只生成本地包，未创建 GitHub Release。

仓内 `Assets/Branding/XY_logo.png` 与用户原始文件 SHA-256 均为 `abc8b5f757023c44cc0f253b0f2e3c766b50422cae94d2dcd74eed15299a9b81`，原图字节未改。88 项 Assets 资源检查未发现缺失 `.meta` 或重复 GUID。

已核对 Unity 图标及 SplashScreen 配置，以及最终生成的 Android 资源：实际 Activity 使用的 `UnityThemeSelector` 的 `windowBackground` 为白色；launcher 的 `values-v31/xanadu_launch_theme.xml` 使用 `@mipmap/app_icon`，系统启动窗口和图标背景均为白色。运行时冷启动画面证据见下方真机截屏与模拟器录像记录。

## OPPO 真机交互

设备为 OPPO PKM110，Android 16，1080×2373。本轮通过实际 Android 触屏输入和存档状态检查完成以下验证，完整日志位于本地 `artifacts/sheep-refinement/`。

`unlimited-acceptance.log` 记录：

- 连续 3 次取牌与撤回循环可用，未因历史使用标记禁用撤回。
- 连续 3 次洗牌保留图案总量并更新 RNG。
- 连续 2 次移出累计 6 张暂存牌；暂存区满时禁用移出，拒绝操作不改变状态；点回暂存牌后可第 3 次移出。
- 结束进程并恢复后，历史 used 标记保留，洗牌与撤回仍可再次使用。
- 连续 5 次 UI 重开得到布局 `[10, 8, 9, 3, 11]`，相邻布局均不同。

`challenge-replay.log` 记录受控挑战关夹具的全部 144 次 UI 取牌，最终消除 144 张牌，累计通关数为 1。该结果验证生成解法能沿真实点击、遮挡、三消与存档链路完成，不代表任意取牌顺序都可通关。对应截图包括 `unlimited-tools.png`、`restart-layout-0.png` 至 `restart-layout-4.png`、`challenge-won.png`。

真机冷启动使用 10 次连续原始截屏记录，`phone-startup-03.png` 显示白底鸟形 Logo。本轮真机证据为截屏，完整冷启动录像及抽帧证据来自下面的模拟器验收。

## 模拟器冷启动与短屏验收

`artifacts/sheep-refinement/emulator-acceptance.log` 记录 Android ARM64 模拟器验收。冷启动命令返回 `LaunchState=COLD`，输入前等待本次进程的 Unity ready 日志。

- 系统应用信息页显示 XY 图标，证据为 `emulator-system-app-icon.png`。
- 实际冷启动录像 `emulator-cold-start.mp4` 及 5 fps 抽帧显示系统应用图标过渡到白底自有标识，采样画面未出现 Unity 标识。截图为原始捕获或直接抽帧，未作画面编辑。
- 在 1080×1920 短屏通过 UI 重开得到布局 9 和 11，每局均为 144 张。分离错位牌堆、棋盘、移出区、卡槽、工具和底部操作均在屏幕内。
- 卡槽标题具有顶部内边距，牌槽位于标题下方，与移出区及工具无重叠；帮助明确显示“三种道具不限次数，复活每关一次”。
- 验收后恢复模拟器原始 1080×2400 尺寸，并记录恢复后布局和最终大厅截图。
- 模拟器测试前备份的 6 个主档及备份文件已逐字节恢复，SHA-256 清单为 `emulator-restored-save-sha256.json`。

共用底部状态带下移后，补充检查了 1080×1920 下的 2048 和跳一跳页面：棋盘或场景、按钮、提示与状态带均完整显示且无重叠。证据为 `emulator-1920-2048.png`、`emulator-1920-jump.png` 和 `emulator-1920-other-games-regression.log`。本项仅进行页面导航，无游戏操作或设置变更；结束后恢复 1080×2400 大厅，6 个存档文件仍与测试前逐字节一致。

模拟器结果不替代真机触控和完整通关验证；两类证据分别以上述日志为准。

## 物理机用户进度恢复

覆盖安装前备份三款游戏的主档及备份，共 6 个文件，位于 `artifacts/sheep-refinement/pre-upgrade/`。本轮受控夹具测试结束后，原用户这 6 个文件已恢复；启动及 Home 返回后，最终 SHA-256 均与测试前的 `pre-upgrade/manifest.json` 一致，清单保存在 `user-progress-restored-sha256.json`。

`user-progress-restored.png` 显示用户仍处于第 2 关旧布局 2，已消 9 张、余 135 张，卡槽保留 2 张草图案；原用户进度未被本轮夹具覆盖。`phone-final-player.log` 对应最终进程，未发现 Unity 异常或 AndroidRuntime fatal。

## 独立审查与验证边界

独立审查覆盖核心规则、存档校验、旧布局兼容、新布局和生成解法、UI 坐标及生命周期、品牌配置与 Android 生成资源。审查提出的两项问题均已修复并复核：实际入口子主题覆盖父主题白底的问题，以及文档误写“免费复活不限次数”的问题。

本记录的源码审查、独立测试、构建及设备结果各有范围；未验证全部 Android 机型或长时间性能表现。自有启动标识的录像结论仅针对已记录的模拟器冷启动，真机启动画面采用连续原始截屏。
