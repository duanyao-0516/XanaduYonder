# 切水果大师 WebView 接入就绪核查

日期：2026-11-01。核查分支 `p/duanyao.0516/codex/fruit-slice-package`，基线 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`。本轮为源码、资源、环境核查，未实现接入、未构建 APK。

## 结论

可以开始实现，没有需要用户先补齐的核心游戏资源或工具。现有 418 个原始游戏文件、9,840,649 bytes 已重新校验大小和 SHA-256；包含引擎、游戏逻辑、关卡、场景、模型、图集和实际音效。4 个源站缺失资源用于其他平台分享/推广或未调用的背景音乐方法，不阻塞离线 WebView 方案，具体见同目录 `2026-11-01-fruit-slice-package.md`。

源码是压缩发布版，改造和验证成本高于有原始 Laya 工程的情况，但不妨碍开始。高清大厅 Logo、未压缩源码可作为后续补充，不是前置条件。

## 已确认的用户要求

- 大厅新增「切水果大师」入口，进入后使用全屏 WebView。
- 连续两次从屏幕边缘触发系统返回后，回到合集大厅。
- 适配手机等小屏幕及操作手势。
- 移除广告、联网及其他非必要平台内容。
- 复活、三倍奖励、刀光解锁全部免费保留，无需广告。各动作直接执行本地业务逻辑，不保留广告等待链。

## 建议实现边界

Unity 大厅通过独立桥接打开新的 Android Activity，内部 WebView 承载本地 Laya 游戏；大厅在下层保留，退出 Activity 后恢复大厅输入。现有 `AppController.KingdomRush.cs` 与 `KingdomRushLauncher.cs` 已提供该宿主交接模式，但 SDL/Lua 的 Activity 实现不适用于 WebView，双返回逻辑也需新建。

通过 `WebViewAssetLoader` 或等价的受限本地资源加载器装载 APK 内文件，使用固定本地 origin 保留 DOM storage。游戏不需要外部服务；移除平台 SDK、统计、分享推广、外链和原平台专用弹窗，同时拒绝外部导航/请求，验收 APK 不声明 INTERNET 权限。采用本地 HTTP(S) origin 并不意味着启动服务器或访问互联网。官方依据：[加载应用内内容](https://developer.android.com/develop/ui/views/layout/webapps/load-local-content)。

原版界面按 720×1280 设计，建议首版竖屏全屏。修正 HTML/index.js 残留的横屏声明和 `fixedheight` 在长屏下缩窄逻辑宽度的问题，按安全区保持画面比例，重新核对按钮触控范围及 3D 射线坐标，不能只拉伸 WebView。

返回由 Android 系统回调统一处理，建议第一次提示“再返回一次回到大厅”，2 秒内第二次退出，超时或切后台重置。只统计已完成的系统返回，取消手势不计入；不依赖 H5 history，也不另写与系统抢占边缘的滑动识别器。保留系统边缘返回通道，并在系统接管触摸时清除刀光和按下状态。官方依据：[系统返回适配](https://developer.android.com/guide/navigation/custom-back/predictive-back-gesture)、[手势导航冲突](https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav)。

## 必须完成的适配工作

1. **单指切割所有权**：原游戏 Down/Up 检查 touchId，帧循环却读取全局 mouseX/Y，引擎默认支持多指。需要绑定唯一活动指针并处理取消，避免第二指引起刀光跳变。
2. **前后台与计时**：原游戏使用 Date.getTime 计算倒计时和出水果间隔，单纯暂停 WebView/渲染不足以冻结时间。宿主需要驱动业务暂停、恢复、时间补偿和声音停止。
3. **退出与存档**：原版 LocalStorage 保存关卡、钻石、刀光和最高分，不保存完整进行中对局。首版可保留长期进度、退出结束本局；若要跨退出恢复飞行水果等完整局面，则属于新增存档能力。读取存档还需处理损坏 JSON。
4. **彻底离线**：除远程脚本外，bundle 中无条件初始化的统计逻辑也要移除，它目前会向当前页面发 POST。不能只阻断域名或删除 SDK 后保留依赖回调的按钮。
5. **游戏免费规则**：复活、三倍钻石和原视频刀光入口均保留为本地动作，避免重复点击造成重复发奖，并保留明确的已解锁/已使用状态。

## 环境与现有整包依赖

已核查 Unity 6000.3.27f1、JDK 17、Android SDK 35/36、build-tools 36、NDK。当前连有已授权的 OPPO PKM110（Android API 36 / ARM64），系统 WebView 为 `com.google.android.webview 138.0.7204.179`。这些是本轮快照，开始构建/设备验收前需再次确认连接状态。

当前新工作树没有被 Git 忽略的王国保卫战 AAR 与构建证书；直接全量 Android 构建会被已有 `Assets/Editor/KingdomRushAndroidBuild.cs` 门禁拦住。已找到本机来源，无需用户补包：

`/Users/bytedance/.codex/worktrees/xiaomi-jump-release-091/XanaduYonder`

该来源的两个 AAR、game.love、内容验证报告及 runtime-build.json 已通过当前工作树 `scripts/kingdom-rush/verify-build-input.py`；相关 70 个源码/构建文件与当前分支一致。另两处现有工作树也通过相同内容门禁。开始整包构建前，需要恢复这些本地依赖，并在新工作树重新运行门禁；本轮未复制文件，未绕过验证。

## 验收范围

重点覆盖 360dp/短屏/长屏布局、快速连切、多指误触、边缘返回及取消、双返回超时、反复进出大厅、切后台恢复、三种模式、50 关资源、免费复活/三倍奖励/刀光解锁、声音开关和本地存档。WebGL 性能、冷启动内存和音频行为必须以 Android WebView 真机为准，桌面浏览器首启截图不能代替这些验收。
