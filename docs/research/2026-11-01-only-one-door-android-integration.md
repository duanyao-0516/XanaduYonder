# 只有一道门 2.1：Android 完整离线接入调查与建议设计

日期：2026-11-01。基线：`1f99287016f353fd92ee0ca94a761c2e6bd433e9`，分支 `p/duanyao.0516/codex/only-one-door-package`。

本文件记录最初的宿主、原游戏内容与平台依赖调查。用户随后已批准实施；当前工作与验收进展以[实施计划](../superpowers/plans/2026-11-01-level-devil-android.md)为准。此前已取得的包与浏览器抽测见[下载记录](2026-11-01-only-one-door-243372-package.md)。

## 1. 推荐方案与边界

采用 **Unity 大厅 → 独立横屏 Android Activity → 本地 WebView 执行原 Haxe/OpenFL/Stencyl 游戏**。游戏资源随 APK 安装，Java/JavaScript 的小范围适配只负责平台准备、去广告、暂停恢复、设置与可靠存档。物理、碰撞、陷阱、关卡脚本、顺序、秘密内容、服装、原菜单及原触摸控件由原游戏继续负责。

| 路线 | 对原玩法的影响 | 结论 |
| --- | --- | --- |
| 原 H5 + 系统 WebView + 独立 Activity | 保留现有游戏运行时；需要处理平台、生命周期、资源加载 | 推荐；与合集现有外部运行时接入边界一致 |
| 全部重写为 Unity/C# | 需要重新实现全部行为、物理与特殊关卡；没有原始 Stencyl 工程辅助 | 无法低风险保证原版一致，不选 |
| APK 再附带完整 Chromium 或重建原生 Haxe 应用 | 额外浏览器体积与维护负担，或需要当前未取得的原工程 | 当前无必要，不选 |

“完整”的范围是 **4399 页面243372对应的已下载版本**。不把它等同于其他商店/平台的更新版本。保留原版正常进度解锁；“包含全部关卡”不等于启动时全部解锁。

本游戏新增 Android 权限集合应为空。合集已有触感权限仍按公共功能使用；游戏本身无需新增震动表现。

## 2. 已查证的完整内容

原件24文件、5,229,774字节；原件 ZIP 2,085,341字节。没有原始 Stencyl/Haxe 可编辑工程。原始主 JS SHA256：`5af62be64b44d294acec64c695ad1afe933856d581749992b98924e4cff7217f`。

PAK 中2138项已逐项gzip校验。包含1709图像、57文本和372二进制项；外置19个MP3均与清单大小一致。19个OGG是源站404的备用编码，Chrome使用首选MP3；Android验收必须逐个验证这19个MP3的真实WebView解码，不伪造缺失OGG，也不对现有声音有损转码。

利用原运行时的 MBS reader 解码得到184个场景，401个资源定义和360个行为定义。当前已检查场景actor type、snippet、event、sprite及行为实现引用，无缺项；此检查不等于所有动态行为已运行通过。

| 场景分类 | 数量 | 范围 |
| --- | ---: | --- |
| 第一部分主线 | 80 | 15主题×5 + Boss×5 |
| 第二部分主线 | 66 | 13主题×5 + `y_Boss` |
| 双人竞赛 | 14 | `2P_1_Blah` 至 `2P_14_Speed_Changes` 的实际目录；原胜负规则保留 |
| 秘密、结尾与过场 | 11 | `Boss_Exit`、`y_Ending_1…7`、`Z_Congrats`、`z_TikTok`、`z_Secret_B` |
| 菜单、预加载与双人结算 | 7 | 两部分选关、设置、服装、模式选择、预加载、双人胜利 |
| 静态跳转未引用的旧/测试场景 | 6 | 原包保留，不裁剪、不强加到玩家选关页 |
| 合计 | 184 | 不能称为184个普通关卡 |

177个静态跳转目标全部在场景表中，加启动场景61，共178个有静态入口依据的场景。单人SceneManager有155个完成分支，分支数不是关卡数量。

**保留策略：PAK及19个MP3逐字节保留，184组场景数据全部随包；不重编码图片、声音，不按“看起来没用”删资源。** 两个Poki SDK从生产包删除；它们作为来源证据可保留在非运行目录。广告图标等素材即使仍在原PAK内，广告专用展示/行为必须停用。

完整分类、ID和引用检查见 [content-audit.json](../../artifacts/research/only-one-door-243372/integration-audit/content/content-audit.json)。

## 3. 职责与实际宿主接点

| 层 | 拟新增/修改位置 | 职责 |
| --- | --- | --- |
| 大厅 | `AppController.LevelDevil.cs`、`Runtime/LevelDevil/LevelDevilLauncher.cs`、`AppController.cs` | 大厅卡、进入前保存/停止反馈、启动busy、失败回滚、返回恢复 |
| Android | `Assets/Plugins/Android/LevelDevilActivity.java`、`LevelDevilBridge.java`及Manifest | 横屏安全区、WebView、安全限制、前后台、音频/触点释放、退出与错误 |
| 本地平台适配 | `ThirdParty/LevelDevil/`下可审查的适配源与确定性补丁 | 平台ready、广告后续动作、显式免费解锁/跳关、公共静音门控、存档错误保护 |
| 原内容 | `ThirdParty/LevelDevil/`中的固定来源包、provenance与内容清单 | 保存来源和哈希；生成包含完整原资源的离线内容AAR |
| 构建 | `LevelDevilAndroidBuild.cs`、`scripts/level-devil/` | 固定依赖、内容打包、补丁断言、权限/资源/探针泄漏检查 |

现有 `AppController.KingdomRush.cs:23` 已实现进入前门禁、保存、停止声音、禁用大厅输入与失败恢复；`KingdomRushLauncher.cs:15`/`:46` 给出JNI启动和状态轮询契约。新游戏使用同样的职责划分，但有自己的 WebView Activity。

不能只加一张卡：`AppController.cs:95` 的 `LobbyReadyForInput` 和`:1444`的Back逻辑目前只检查Kingdom Rush。应在 AppController 这一公共外部页面所有者中归并“是否有外部游戏运行”判断，同时覆盖两款游戏；不把Level Devil分支塞进某个游戏的基类。销毁时解绑事件，启动失败或renderer崩溃时必须解除busy。

现有App唯一入口 `KingdomRushLauncherActivity` 实际是任务栈trampoline，保留它。新Activity `exported=false`、`sensorLandscape`，无第二个launcher、无浏览器外跳。Unity大厅继续竖屏。

布局使用完整原游戏视口，在Android安全区内等比适配，保留原控件位置。按用户最终确认，第一次系统Back显示“再返回一次，回到大厅”，2.5秒内第二次Back发起存档并退出；不注入游戏ESC，不使用WebView浏览历史。原画面内的返回/回选关仍保持游戏内语义。取消的预测返回手势不计数，后台/失焦重置计数。当前targetSdk36下不能直接复制旧`onBackPressed`/`KEYCODE_BACK`路径：API33+注册平台`OnBackInvokedDispatcher`/`OnBackInvokedCallback`，API26–32使用旧回调，统一交给同一游戏返回处理；不为此关闭整个App的预测返回。API36的手势返回和三键返回都须实测。[Android 16行为说明](https://developer.android.com/about/versions/16/behavior-changes-16#predictive-back)

源码依据：[宿主审计](../../artifacts/research/only-one-door-243372/integration-audit/host/host-audit.md)。

## 4. 本地加载、断网与权限约束

使用Android官方 `WebViewAssetLoader`，固定专用本地HTTPS来源，例如 `https://level-devil.xanaduyonder.invalid/assets/level-devil/index.html`；该URL由应用内资源拦截器处理，不启动localhost服务器，也不向该域名发起网络连接。固定独立origin同时隔离原存档键。官方推荐此类HTTP(S)本地资源加载，避免`file://`跨域放宽。[Android本地内容文档](https://developer.android.com/develop/ui/views/layout/webapps/load-local-content)

具体约束：

1. 仅允许规范化后匹配发行文件清单的本地URL和加载方法，拒绝路径穿越、意外查询、外部来源和非资源用途请求。AssetLoader未命中时显式返回拒绝响应；不能返回null落回互联网。正确提供JS/PAK/MP3的MIME及需要的读取语义。
2. `setBlockNetworkLoads(true)`；不开INTERNET或ACCESS_NETWORK_STATE。Android官方说明无INTERNET权限时此开关默认阻断，尝试放开会抛SecurityException。[WebSettings文档](https://developer.android.com/reference/android/webkit/WebSettings#setBlockNetworkLoads(boolean))
3. 开启运行所需JavaScript和DOM storage；关闭file/content访问、file URL跨域放宽、混合内容、新窗口和第三方cookie。引擎读取本地PAK/MP3所需XHR保留，所有响应都来自APK白名单文件。
4. 拦截外部导航、`intent:`/market/浏览器跳转、弹窗、下载、文件选择；不自动调用外部Activity。生产包不注册Service Worker、不加载在线SDK，不保留可触发的外部广告/统计请求。
5. 网页摄像头、麦克风等permission request统一deny，定位回调拒绝；不申请相机、录音、定位、通知或广泛存储权限。官方要求宿主明确grant/deny；这里选择拒绝。[WebChromeClient文档](https://developer.android.com/reference/android/webkit/WebChromeClient#onPermissionRequest(android.webkit.PermissionRequest))
6. 发布APK检查完整merged权限集：本游戏新增权限必须为空；禁止网络、相机、录音、定位、通知、电话与外部存储权限。允许集根据现有批准合集功能核对，新增项使构建失败。不能只看源码Manifest或四个历史黑名单。
7. 不提供泛用文件/网络JavaScript bridge。若需要JS到宿主消息，只开放本地origin、主frame和固定消息集合（ready、error、exit、save-state），拒绝URL/路径/任意方法调用。API26兼容实现不依赖仅新WebView才支持的可选特性。

最终需要在允许联网的设备环境和飞行模式分别测试：飞行模式证明无需网络；允许联网时严格的资源请求白名单与APK权限证明不存在伺机发出的网络调用。仅“外域请求0”不够，因为旧SDK曾向空URL/`./`发统计请求。

## 5. 去广告必须保留的行为

原 `poki-sdk.js` 会再加载 `poki-sdk-core.js`，后者仍含广告、统计、cookie/savegame收集、远程脚本和错误上报。两个文件均不进入生产运行包。对原主JS只改明确的平台适配边界和已列明的广告UI调用，记录每处补丁的原始哈希、匹配数和输出哈希；不对整个minified文件重新格式化或宽泛正则替换。

| 原路径 | 定位 | 离线行为 |
| --- | --- | --- |
| 平台初始化 | `PokiSiteLock.checkSiteLock` / `StencylPoki`，原JS127–132行 | 本地ready完成，移除遥测和脚本注入；保留引擎启动的完成事件 |
| 两种输入触发重试 | `ActorEvents_781`，4434–4435行 | 本地异步完成原后续动作，恢复并重新加载当前关 |
| 单人/双人通关广告 | `SceneManager:4693`、`2PSceneManager:4912` | 取消广告展示，保持过场、保存、解锁和切关顺序 |
| 两个速通解锁入口 | `ActorEvents_708:4360`、`ActorEvents_959:4672` | 用户点击后免费本地解锁，执行原save/menu逻辑；不提前改所有进度标志 |
| 可选跳关 | `ActorEvents_762:4422` | 保留原出现条件、排除条件和用户主动点击；点击后免费执行原level_complete |

广告/视频专用标识和提示移除，原入口改为明确的“解锁速通”“跳过本关”操作。不能把广告统一返回false，否则丢速通/跳关；不能用没有完成回调的空实现，否则暂停无法恢复；也不能启动时统一发奖励导致自动通关。

下载版本SDK的隔离探针中commercialBreak返回1、rewardedBreak返回true，这支持上述用户触发的免费路径与该版本的已观测结果一致，但不代替实际三个奖励入口和四个商业中断分支的验收。

离线continuation必须异步且恰好一次，绑定当前页面session；销毁后作废。回调只能释放其自己的暂停原因，不能在Android后台或用户暂停期间恢复游戏。

依据：[平台审计](../../artifacts/research/only-one-door-243372/integration-audit/platform/platform-audit.md)、[调用点清单](../../artifacts/research/only-one-door-243372/integration-audit/platform/platform-callsites.json)、[SDK运行探针](../../artifacts/research/only-one-door-243372/integration-audit/platform/sdk-runtime.json)。

## 6. 生命周期、触摸、声音与存档

### 生命周期与输入

原引擎失焦只变更inFocus，没有自动暂停玩法；Android `WebView.onPause()`本身也不暂停JavaScript。[Android生命周期API](https://developer.android.com/reference/android/webkit/WebView#onPause())

Android暂停/失焦、锁屏、返回、窗口重建、renderer终止分别处理：释放原触摸ID、按键和mouse-held状态，暂停游戏与音频；恢复时保留原暂停状态，后台时长不推进速通计时。WebView/游戏桥使用明确的宿主暂停原因和session生命周期，避免迟到的广告延续或异步加载覆盖新页面状态。需要冻结timer时注意`pauseTimers()`影响进程内所有WebView，当前合集仅此WebView，也必须在退出时成对恢复。

原版已创建单人3键、双人6键并监听多点触控；直接保留这些控件。验证多个指针同时按住、跳跃+移动、滑出、touchcancel、来电/锁屏中断，不能把触摸转换成单一鼠标事件。主JS触摸cancel与普通touchend的mouse-up处理有差异，必须测试并在输入适配边界清理残留。

### 公共设置

进入时由Unity传入公共声音状态。原游戏`Volume`继续拥有游戏内声音选择；有效输出同时受合集静音门控，不能在返回/重进时把用户原来的Volume从0重写成100。公共设置不写入原游戏进度字段。原`Hide_Controls`设置、单人/双人模式和服装不重置。

### 原存档及必要保护

真实存档是 `localStorage['UnicycleLegend:mySave']`，由启动场景61设定，采用OpenFL SharedObject/Haxe序列化。固定本地origin和应用私有WebView数据目录，禁止每次进入清缓存/清storage或使用随机来源。原字段、默认值和通关/钥匙/秘密关/速通纪录的更新流程保留；不改合集`xanaduyonder-v1.json` schema。

已发现原写入先removeItem再setItem，且错误回调不覆盖所有失败；原读取解析失败还可能继续默认流程。建议在该游戏自己的存储边界做最小保护：先序列化和验证，再直接替换主键；保留上一份经原解析器和字段检查确认有效的备份，恢复失败保留坏档并提示，不先删除旧数据。写失败保留内存进度并持续显示未保存，不伪报成功。`setItem`可能因配额/禁用存储失败，必须真实注入验证。[Web Storage标准](https://html.spec.whatwg.org/multipage/webstorage.html#the-storage-interface)

这只加固持久化I/O，不改游戏序列化字段或关卡进度语义。原单人完成逻辑约2000ms才提交切关/进度，约2010ms执行saveGame（原代码浮点常量2009.9999999999998）；退出时抢先save并不能保存尚未执行的进度更新。显式返回合集应先拒绝新输入，先让本次通关/解锁更新完整进入原可保存字段，再将退出请求排在该完成事务及真实存档确认之后，再销毁WebView；写入失败必须给出可重试结果，不能永久等待一个遗漏的回调。Android切后台仍立即暂停，进程被杀只承诺恢复最后已提交的原检查点。原存档不包含任意时刻全部物理状态，不承诺从跳跃或未提交过场的某一帧继续。

## 7. 构建方式与已核实前置条件

推荐固定 `androidx.webkit:webkit:1.18.0`。已读取Google Maven实物：minSdk24、minCompileSdk33、minAGP7.2.0；AAR自身Manifest不声明权限。与本仓库API26/target36的声明门槛相容，实际Gradle传递依赖图和最终编译仍须验证。[官方发布记录](https://developer.android.com/jetpack/androidx/releases/webkit#1.18.0)

证据保存于 `artifacts/research/only-one-door-243372/integration-audit/androidx/`。

- Java Activity/Bridge放Unity的`Assets/Plugins/Android`源码插件路径，由Unity生成的Gradle编译；已有`IdiomKeyboardBridge.java`先例。
- 独立`LevelDevilAndroidBuild.cs`在生成的unityLibrary Gradle文件中幂等声明固定AndroidX依赖，在根gradle.properties启用AndroidX。固定Unity安装自带的默认模板已有google()/mavenCentral()；依赖下载只发生在构建期，运行期不下载内容。
- 生成内容型`LevelDevilContent.aar`，manifest明确min26/target36；避免低target库合并时引入历史存储/电话权限。使用APK asset加载，不经UnityWebRequest、远端URL、热更新或首次下载。
- 来源包和补丁放`ThirdParty/LevelDevil/`，不放Resources；来源归属、版本、哈希、保留范围写入provenance和THIRD_PARTY_NOTICES。当前研究用instrumented JS、解锁夹具、来源ZIP、两份广告SDK绝不进入正式APK。
- 构建前断言原哈希、补丁匹配次数、输出清单和PAK/MP3哈希；成品再按APK ZIP实际内容复核，防止“目录正确但打了旧内容”。

**初始构建前置缺口（实施时已恢复并通过原门禁）**：此新工作树起初缺少既有`KingdomRushRuntime.aar`、`KingdomRushContent.aar`、匹配`game.love`与prebuilt库；所有Android构建会被`KingdomRushAndroidBuild.cs:15–40`拦住。现已从同版本代码的本地工作树复制所需生成物，在本树重定位验证报告并通过原哈希门禁；未跳过门禁或移除现有游戏。证据见 `artifacts/research/only-one-door-243372/integration-audit/host/kr-restoration.json`。

当前源版本是0.9.4/versionCode13，某些旧文档/脚本仍写0.9.1；构建要核验实际输出与时间戳。先用独立测试包名、非Development构建进行离线验收，避免覆盖用户正式存档和引入调试网络权限；正式签名/发布另按现有交付流程。

原网页ZIP约2.09MB，仅能作为内容压缩量参考。实际APK增量还包括AndroidX和宿主代码及压缩策略，须构建后测量；无需另打包完整浏览器内核或新的游戏物理运行时。

## 8. 实施顺序与完成门槛

| 阶段 | 交付内容 | 必须验证 |
| --- | --- | --- |
| 1. 固定来源与闭包 | 可复现来源/补丁/清单；完整PAK/MP3；实际关卡图 | 184场景和资源哈希一致，177静态跳转目标无缺项；探针不入包 |
| 2. 离线游戏适配 | 本地ready、7个广告后续分支、去广告UI、存储保护 | 3个实际奖励UI与4个中断路径；正常解锁不变、无后台误恢复、无统计请求 |
| 3. Android宿主 | AssetLoader、固定origin、权限拒绝、生命周期、声音/触摸 | 无INTERNET的清数据冷启动；19MP3解码；单双人并发触摸与Back/Home |
| 4. 合集集成 | 大厅卡、busy/失败/返回、资源构建门禁 | 原20个入口仍存在；新入口往返、重复进入、图标重入；KR/成语键盘等交叉回归 |
| 5. 全内容验收 | 最终APK证据与逐关状态表 | 以下完整性、功能、设备三层均闭环后才称“完整接入” |

最终验收分三层，不相互替代：

1. **静态完整性**：逐项资源解包/哈希、场景与行为引用、真实跳转图、APK资源与权限、无SDK/测试钩子。六个旧/测试场景仍保留。
2. **全部活动场景运行**：诊断专用构建为场景补齐原必需状态后装载178个有入口依据的场景，记录初始化错误、缺图、音频、异常请求；不能简单强切场景制造未初始化状态。原始版本与离线版使用同一夹具比较，正式包不含夹具。此项只证明装载，不证明每关能完成。
3. **实际玩法与Android回归**：146个主线关、14个双人场景及秘密/Boss/结尾按目录逐项记录进入、死亡/重试、实际完成、下一关、解锁和存档；正常流程验收不得把强制level_complete当成真实通关。两部分速通、隐藏入口/钥匙/服装、所有菜单和三种奖励入口单列。覆盖最低支持API26与当前目标API36、目标真机WebView、刘海与窄屏、双人多指、无网首次启动、系统Back、图标重入、锁屏、杀进程重开、升级保档、存储失败与renderer死亡，特别覆盖通关后0–2.1秒立即Back/Home/杀进程的保存竞态。

如果某关仅完成静态或装载检查，验收表必须明确标记，不能以抽样结论宣布全部关卡已验证。全部实现完成前不承诺Android性能、完整通关或最终APK体积。

## 9. 本轮实际完成的验证

- 三条独立审计线：现有Unity/Android宿主、原游戏场景/资源、SDK/存档/输入/生命周期。
- 原MBS内容通过研究专用runtime class导出读取，184场景/资源引用检查无错误；没有逐场景运行或逐关通关。
- 原SDK直接调用的离线Chrome探针，区分SDK方法返回值和真实玩法UI。
- Android官方API与AndroidX实物元数据核对；未做Gradle依赖解析、Android编译/安装/真机回归。
- 研究证据全部在被忽略的`artifacts/research/only-one-door-243372/integration-audit/`；本轮仅新增本调查设计文档，未改原游戏原件和产品代码。

交叉复核已处理：补齐通关约2秒延迟提交与退出的竞态、Android16/target36的新Back回调路径，并纠正Gradle模板来源；产品实现和设备验收仍未开始。
