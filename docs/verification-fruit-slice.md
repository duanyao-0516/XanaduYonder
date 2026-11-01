# 切水果大师接入与验证记录

交付补充：用户于 2026-11-01 验收并要求快速合并 master，本轮不运行测试或编译。单列大 Logo 改为原小图标，双列及目录改为原创绿色切西瓜线稿。固定原始输入位于受版本控制的 `ThirdParty/FruitSliceSource/`，完整运行内容及图标也纳入版本库；下文关于资源被忽略的描述属于此前本机验证阶段。此前 APK 不包含本轮图标调整。

日期：2026-11-01。工作分支为 `p/duanyao.0516/codex/fruit-slice-package`，基线为 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`。

**去经济串行版已完成构建、成品检查并安装到用户 OPPO 手机：取消钻石与刀光领取计时，27 款刀光首次点击直接装备。完整 Unity 大厅入口耗时仍待用户实际测试，尚未证明性能问题已修复。** 新规则已通过独立源码、资源及 VM 复核；同 Native 的模拟器 WebView probe 观察到首页无钻石/领取计时、商店吉他图首击变为「使用中」。并行加载实验没有稳定收益，未纳入交付。下文保留先前缺类修复包及浏览器证据作为历史基线，不把旧包验证扩展到新规则，也不宣称全部验收通过。

## 接入范围与交互契约

- 大厅新增编号 21「切水果大师」，归入敏捷挑战；公共入口覆盖单列、双列和分类目录。Unity 保留大厅，独立的竖屏 Android `FruitSliceActivity` 使用 WebView 承载离线游戏。
- APK 内资源通过固定来源 `https://appassets.androidplatform.net/fruit-slice/index.html` 加载。Java 宿主负责受限资源响应、系统返回、安全区、生命周期与销毁；H5 负责玩法、存档和刀光装备。
- 第一次完成系统返回提示「再返回一次回到大厅」，2 秒内第二次完成返回才结束 Fruit Slice Activity。切后台、失去焦点会重置确认；API 34+ 的返回手势取消回调也会重置。加载失败页仍使用同一系统返回规则，页内「返回大厅」按钮可以单次点击退出。
- **以追加规则为准：27 款刀光首次点击直接装备**。图标和装备按钮均直接生效，不显示价格、锁、广告、购买确认或领取等待；取消首页的限时刀光入口及全局领取倒计时。先前的两次点击获取规则已被此要求替代。
- **移除钻石经济及结算货币三倍奖励**：不再展示或累计余额、掉落/结算货币、延迟发奖、花费和三倍领取。保留玩法分数、星级、关卡进度与纪录；切割 Down/Move 立即响应，复活与重玩保留本地一次性动作。限时玩法本身的 60 秒时钟及炸弹扣时继续保留。
- 存档保留关卡、星级、最高纪录和有效的已装备刀光；忽略并停止写入旧货币、拥有状态和领取时间字段。旧存档中有效的刀光编号不因旧锁状态丢失。返回大厅结束当前局；后台暂停后恢复当前局，后台时长不应计入游戏计时。进行中飞行水果等完整局面不做跨退出存档。
- 移除广告平台、统计、推广、分享和外链等不需要的内容；不使用伪造 SDK 成功回调。构建与运行还需分别证明离线资源闭包、无网络权限及无外部请求。

## 当前追加任务：独立模拟器与阶段样本

独立 AVD 为 `XanaduFruitSliceValidationApi36`，serial 为 `emulator-5556`，Android API 36 / ARM64，WebView 主版本 133。此前隔离调查只操作该模拟器。用户随后明确慢点为「点击大厅切水果入口后」，并要求将最新测试包安装到其手机计时；主流程已完成该安装，用户实际测量待反馈。普通启动器 probe 包名为 `com.duanyao.fruitsliceprobe`，按钮调用实际 `FruitSliceBridge.open(this, true, false)`；三份 Native 文件逐字节复制，内部 Activity、离线资源来源和业务路径保持一致。probe 无网络权限、非 debug，资源相等及签名检查通过。它隔离测量 Native/WebView/content，不包含 Unity 入口 Save、JNI 前处理或 UGUI。

| 内容快照 | probe 证据 |
| --- | --- |
| 先前缺类修复基线 | `startup/probe/FruitSliceProbe.json`：407 文件、8,841,704 bytes；APK SHA-256 `cb6b54694d15bf76b0e9fc93fc87ced115bb5dc4992d29090d8862138bb8e210`；资源与历史 `startup/baseline.apk` 逐字节一致 |
| 去经济串行加载版 | `startup/probe/FruitSliceProbe-economy-serial.json`：406 文件、8,826,023 bytes；manifest SHA-256 `5de671bac329db9b9a609441c58585e455315d7404afcc19ddfcfda5b302343a`；probe APK SHA-256 `5e26c7f96736fbe1f33850b1f62a9f3c0d3cdba9f34731ba50a436e882013eb3` |

以下数值均为相对 Bridge `open_requested` 的毫秒数，直接来自 `artifacts/validation/fruit-slice/startup/native-samples.log`，当前记录截至 13:18:52：

| 内容 / PID / session | loadUrl | mold 请求 | 首关请求 | 全 50 关请求已发出 | ready | visual callback |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 原版 / 2362 / 1 | 270 | 940 | 1623 | 1711 | 1863 | 1964 |
| 原版 / 2362 / 2 | 20 | 343 | 1041 | 1168 | 1318 | 1378 |
| 原版 / 2362 / 3 | 20 | 396 | 1512 | 1668 | 1999 | 2059 |
| 去经济串行 / 2613 / 1 | 168 | 590 | 1566 | 1671 | 1693 | 1745 |
| 去经济串行 / 2613 / 2 | 96 | 482 | 1756 | 1935 | 1990 | 2029 |
| 去经济串行 / 2613 / 3 | 97 | 450 | 1537 | 1641 | 1735 | 1764 |

每组 session 1 是该进程首次样本，2/3 是同进程重复打开；未声称清空 OS/文件缓存后的严格冷启动。资源阶段表示请求开始，非读取/解析完成；visual callback 表示 WebView 状态可用于后续绘制，非实际屏幕显示或命中水果证明。不同内容及进程样本不直接合并为性能提升结论，原版重复样本本身也有明显波动。

主流程通过 CUA 在去经济串行 probe 观察到首页无钻石与刀光领取计时，商店吉他图首次点击后显示「使用中」。实际 UI 只观察了这一个刀光；独立 VM 已逐项验证全部 27 款首击装备、两个装备入口、旧档迁移及三种结果流程无货币/三倍奖励。此分层证据仍不能等同于全部实际 UI 验收。Unity 诊断包已完成构建，但 CUA 镜像中的拖动未成功推动大厅 UGUI 滚动，尚未取得完整 Unity 大厅→Fruit Slice 的测量；不把工具手势问题判断为产品故障。

并行加载实验未纳入交付：最终并行版本 PID 2913 的首次/重复 visual callback 为 2399 / 2105 ms，未呈现稳定收益，交付恢复去经济串行 manifest `5de671bac329db9b9a609441c58585e455315d7404afcc19ddfcfda5b302343a`。早期并行 v1 / PID 2799 的 1675 ms 属于不同散列版本，单列排除，不作为最终并行收益依据。

Native 有界阶段日志的 API 36 编译通过，证据为 `artifacts/validation/fruit-slice/startup-native-javac.log`；诊断未开启 WebView 调试或增加网络权限。又在 `AppController.FruitSlice.cs` 增加四个有界 Unity 入口日志（有效点击、Save 完成、TryOpen 前/后），使用同一 `FruitSliceStartup` tag 和 Android elapsedRealtime 时钟，保持业务/存盘顺序；独立复审及最新 Unity/Gradle 构建均通过。同步 Save 及 Unity 暂停时 Android UI 等待 Unity 线程目前只是源码支持的候选，尚未测得其实际成本。详见 `artifacts/validation/fruit-slice/startup-investigation.md`。**用户手机完整入口计时与触控、返回、后台、声音等交互验收仍待补。**

## 最新去经济串行包（2026-11-01 13:25）

| 项目 | 已确认结果 |
| --- | --- |
| 独立复审与行为检查 | `artifacts/validation/fruit-slice/review-economy-free.md`：27 款图标/按钮首击装备、旧档装备/关卡/星级/纪录保留、退休字段不回写、主页无领取定时器、三模式结果无货币及延迟结算通过；Native 与四条 C# 日志附加复审无明确阻断。属于源码/VM 证据 |
| 内容门禁 | 406 文件、8,826,023 bytes、674 处资源引用、133 处类引用、11 个已注册游戏类、50 关；全部 406 文件与独立 staging 相同。证据：`content/economy-determinism.json`；manifest SHA-256 `5de671bac329db9b9a609441c58585e455315d7404afcc19ddfcfda5b302343a` |
| Unity / Gradle | 最新构建退出码 0，含四条 C# 入口日志；`artifacts/logs/build-fruit-slice-current-console.log` 记录内容门禁及验包 PASS |
| APK | `artifacts/builds/XanaduYonder-0.9.4-fruit-slice-preview.apk`；327,432,435 bytes；SHA-256 `511bdec042f5172b201afff45a2213dfb0b5c915233e423a30de8b331b3f15cb` |
| 成品检查 | `artifacts/validation/fruit-slice/apk.json`：独立包名 `com.duanyao.xanaduyonder.fruitslicetest`，406 份资产散列相符，非 debug、签名有效、无网络权限 |
| OPPO 安装 | 用户明确授权后以 `--no-incremental -r` 安装，保留应用数据；`startup/oppo-latest-install.log` 为 Success，lastUpdateTime 为 2026-11-01 13:25:20 |
| 最新包完整入口测量 | 已开始收集 `startup/oppo-user-test.log`，等待用户实际操作及反馈；尚无完整 Unity 入口耗时结论 |
| 同包模拟器安装 | `emulator-5556` 安装同一最新 APK 成功，证据 `startup/emulator-latest-install.log`；尚未取得该包的完整大厅入口测量 |
| 工作区 | 四份不相关 Unity 音频 meta 已恢复，`git diff --check` 通过；未提交或推送 |

模拟器稳定串行 probe 再次打开商店，吉他刀光仍显示「使用中」，截图为 `artifacts/validation/fruit-slice/startup/economy-shop-emulator.png`。该截图属于独立 probe UI，不作为最新完整 Unity APK 的大厅路由成功证明。

## 历史基线已确认检查

以下内容/构建检查对应追加需求前的缺类修复版本。

| 验证层 | 当前结果与证据 |
| --- | --- |
| Kingdom Rush 既有构建依赖 | 已从之前校验的本地工作树恢复两个忽略的 AAR、`game.love`、验证证书和运行库报告。只调整报告中的本地路径，散列未改；目标工作树的完整 `verify-build-input.py` 通过。65 份相关已跟踪源码逐字节一致。证据：`artifacts/fruit-slice/validation/kingdom-rush-baseline-restore.json` |
| Native 与 Unity 边界独立审查 | 先审查计划符合性，再审查实现质量。已核对局部业务归属、双方 native 入口互斥、大厅输入屏蔽、异步启动失败恢复、会话令牌、ready 后重放当前暂停/声音状态、本地 URL 约束及 WebView 销毁。加载失败时首次系统返回直接退出的问题已修复并复核。证据：`artifacts/fruit-slice/validation/native-independent-review.md` |
| 后续启动异常修订 | 再次只读核对 `FruitSliceBridge`：外层 UI 调度与内层 Activity 启动均捕获 `RuntimeException` / `LinkageError` 并清除本次运行状态。构建前置检查同时要求单列封面和其他布局使用的两份图像存在 |
| Android Java 编译 | 三份 `FruitSlice*.java` 使用 Unity 6000.3.27f1 所带 OpenJDK、API 36 `android.jar`、Java 8 source/target 编译，退出码 0；共 15 条引导类路径/兼容性弃用警告，无编译错误。日志：`artifacts/validation/fruit-slice/native/review-api36-javac.log` |
| 返回状态机主机检查 | `BackPressCheck` 编译及执行通过：首次、2 秒内第二次、超时后重新计数、重置、恰好 2 秒边界、时钟倒退。共 11 项断言；只证明纯 Java 状态机，不证明系统手势分发。日志：`artifacts/validation/fruit-slice/native/review-back-compile.log`、`review-back-run.log` |
| 缺类修复版生成 H5 内容门禁 | 通过：407 文件、8,841,704 bytes、707 处资源引用、156 处类引用、11 个已注册游戏类、50 关。构建与验证共同检查实际注册表、场景及保留内联 prefab 的 `runtime` / `type` 引用、资源闭包、散列和 JS 语法。最终 manifest SHA-256：`2e6cd014fb5eca43b0d8d15428aa915ef5213a5cd2d989c57c6d021ea33013d2` |
| 缺类修复版 H5 独立复审与行为检查 | 六组临时 VM 检查通过，覆盖即时切割、商店确认、免费奖励与幂等、暂停时钟、存档、实际 Laya 取消输入、漏切扣血，以及实际 `SceneUtils.getCompInstance` 的主界面→50 关选关列表→第 1 关路由。最终 407 份生产文件与独立验证输出逐字节一致。证据：`artifacts/fruit-slice/validation/content-independent-review.md`、`artifacts/validation/fruit-slice/content/challenge-final-determinism.json`、`challenge-production-check.log` |
| Unity 实际大厅检查 | 现有 `scripts/test-lobby-design.sh` 通过，3,757 项断言，覆盖 720×1320、720×1582 与三种布局；17.85 秒结束，未触发 180 秒限定。生成 28 张新截图，人工查看三布局底部截图，入口 21、敏捷挑战 7 款、图标与封面可见。证据：`artifacts/lobby-design/report.txt`、`artifacts/validation/fruit-slice/lobby/result.json` |
| 工作区格式检查 | 当前 `git diff --check` 通过。未提交或推送代码 |

原始第三方文件保持在忽略的下载证据目录，转换源码与来源记录纳入跟踪；生成内容位于忽略的 `Assets/StreamingAssets/fruit-slice/`。内容生成与检查命令及边界见 [离线内容说明](../ThirdParty/FruitSliceContent/README.md)。临时主机验证夹具保存在忽略的 `artifacts/validation/fruit-slice/`，不是新增正式测试源码。已有大厅检查的入口/路由/序号列表同步为 21 项；未新增常驻单元测试文件。

## 历史运行结果与尚未验证部分

CUA 连接已恢复。先前的连接超时及早期 `browser-report.json` 中的启动等待超时不代表最终结果。修复前主流程在桌面浏览器观察到主界面、商店第一次选择/第二次获取、刷新后保留获取状态、单击免费复活及无尽模式失败流程；这些局部流程未出现 JS 错误，随后挑战选择页暴露了缺类问题。

修复后使用最终生产内容重新执行 CUA 浏览器检查：从主界面进入选关页并启动第 1 关，无尽模式可启动，限时模式从 60 秒开始并正常倒计时。选关截图显示首屏，50 关完整性来自内容校验，未逐关浏览或通关。截图保存在 `artifacts/validation/fruit-slice/content/`：`final-challenge-select.png`、`final-challenge-level1.png`、`final-endless.png`、`final-timed.png`、`final-timed-play.png`。同目录 `final-browser-report.json` 记录三种模式观察、旧版错误隔离和修复后无新增错误。这些是实际内容的浏览器运行证据，不是修复包的 Android WebView 真机回归；未据此确认真实首次命中和划切手感。

物理设备为 PKM110（`3B6F4NE8A66ZXTRL`）、Android API 36，系统 WebView 为 `com.google.android.webview` 138.0.7204.179。初版独立包安装成功后由用户在手机上启动试玩；主流程通过 Android Studio 的设备镜像和 CUA 观察。用户进入挑战页后出现 Native 错误页，设备日志 `artifacts/validation/fruit-slice/device-errors.log` 在 11-01 12:20:22、12:20:28 记录 `FruitSlice: Can not find class ui.Prefab.donghuaUI`，并有对应 WebView renderer 退出记录。日志中的其他历史系统错误不作为本次缺陷依据。

修复包已安装到同一独立测试应用，`install-fixed.log` 返回 `Success`，设备包最后更新时间为 2026-11-01 12:29:11。安装后尚未在手机重新打开验证挑战页。已保存的 `artifacts/validation/fruit-slice/device-observed.png` 只显示初版加载画面，不能作为修复包或游戏实战成功截图。

系统边缘返回两次退出、取消手势、切后台恢复计时、首次划切手感、真实多指取消、音效听感、长时间性能和反复开关页面仍未完成真机验收。桌面浏览器成功流程、纯 Java 状态机和 Unity 大厅检查不能替代这些项目。未使用 `adb input` 代替 CUA 进行 UI 操作。

API 33 的 `OnBackInvokedCallback` 没有手势取消事件，当前主动取消重置仅由 API 34+ 分支实现；API 26–32 使用旧 `onBackPressed` 路径。纯 Java 检查不能扩大为这些版本的手势兼容结论。音效听感、长屏安全区、真实多指取消与长时间性能也未由上述源码/编译检查证明。

## 初版故障与门禁修正

初版转换误删了类 `r`（注册名 `ui.Prefab.donghuaUI`），但 `scene/selectMap.json` 的关卡卡片仍通过 `props.runtime` 引用它。初版资源路径、散列、直接构造调用及五组 VM 检查均未覆盖这一注册表路径，构建与安装成功后仍在用户设备上失败。该缺陷说明资源文件存在和 JavaScript 可解析不足以证明导出场景能够实例化。

最终转换恢复必要的 `r`/`donghuaUI`，继续保留漏切提示 `h`/`XXShowUI`。经构造调用、注册字符串、导出场景和内联视图完整引用审查，无调用的 `d`/`handUI` 及其蛋箱资源仍被删除。新增门禁读取实际游戏注册和内联视图，校验全部场景类引用；把旧错误输出交给新门禁时，会在选关节点准确拒绝 `ui.Prefab.donghuaUI`。实际引擎查类回归还验证了删除该注册项能够复现原始错误。初版证据保留于 `apk-before-prefab-fix.json`、`build-before-prefab-fix.log` 和设备错误日志，不作为最终产物结果。

## 历史缺类修复预览包与待补结果

本节记录追加需求前的历史包，不能作为新经济/启动版本的成品证明。预览入口为 `ProjectBuild.BuildFruitSlicePreview`，构建脚本为 `scripts/build-fruit-slice.sh`。最终产物已确认使用独立包名 `com.duanyao.xanaduyonder.fruitslicetest`，非调试包且签名有效。该身份与正式 `com.duanyao.xanaduyonder` 共存，不替换正式应用或共享其数据。构建入口关闭 Development、脚本调试和自动连接 Profiler，使用本地测试签名，并在结束时恢复项目设置。

| 项目 | 状态 |
| --- | --- |
| 最终 Unity / Gradle ARM64 构建 | 第二次构建成功、退出码 0；日志为 `artifacts/logs/build-fruit-slice.log`，完整门禁与成品输出为 `artifacts/fruit-slice/validation/build-command.log` |
| 历史 APK 保留路径 | `artifacts/validation/fruit-slice/startup/baseline.apk`；原构建输出路径已由最新包覆盖，以以下散列区分 |
| 最终成品检查 | `scripts/verify-fruit-slice-apk.py` 通过：独立包名、非调试、签名有效，无 `INTERNET` / `ACCESS_NETWORK_STATE`，407 份内嵌 H5 文件散列与最终生产内容相符 |
| 历史大小与 SHA-256 | 327,445,487 bytes；`f8b08a73682100ee6f7731da504bd2fd62d23055c6dcabd28073375babe985f0`。保留的 `startup/baseline.apk` 已重新核对散列；当前 `apk.json` 对应最新去经济包 |
| 最终安装 | OPPO PKM110 / `3B6F4NE8A66ZXTRL` 安装成功；`artifacts/validation/fruit-slice/install-fixed.log` 为 `Success`，lastUpdateTime 为 2026-11-01 12:29:11 |
| 最终浏览器模式入口 | 进入选关页并启动第 1 关、无尽启动、限时 60 秒启动与倒计时已观察；证据见上文五份最终截图及 `final-browser-report.json` |
| 修复后手机功能与触控验收 | 尚未重新打开验证；挑战页、首次命中、双系统返回、后台恢复、声音等仍待真机回归 |
| 初版历史产物 | 327,443,991 bytes；SHA-256 `5024a506adc49dc4880af096d8b1967e3845c798ae40df4f773043c32a946540`。此版存在挑战页缺类故障，已被最终产物替代 |

构建使用本工作树的 Gradle 目录及独立 ADB 服务端口，同一工作树的 Unity 检查与构建串行执行。新版本待补记录须将源码、主机检查、Unity/Gradle 构建、APK 检查、安装和设备交互分别填写。
