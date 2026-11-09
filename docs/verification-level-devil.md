# 只有一道门 Android 接入与验证

## 2026-11-09 菜单与设置修正

两个选关页返回人数选择页（原scene125），只有人数页左上入口返回合集大厅；关内原返回行为保留。人数/选关页右上增加说明和设置，声音与原音量按钮使用同一确认链，触感用于有效跳跃按键按下。弹窗独立暂停输入、计时与声音，关闭不会解除原游戏或后台暂停。设置经native确认后生效并保存，失败显示重试。

泡泡堂与本游戏编辑同一全局声音/触感偏好，现共用原子native journal；ACK保存墓碑，旧泡泡堂记录仅在共享文件不存在时迁移。Unity把已导入revision与两项偏好一起保存，ACK失败后同进程或冷启动均不重放旧值；native启动以Unity传入值为准。真实浏览器菜单回归覆盖返回层级、窄屏弹窗、暂停计时、真实master volume、原音量按钮和触感消息门控；native ACK在浏览器中模拟，不代表真机声音/震动验收。截图/结果在 `artifacts/level-devil/menu-settings/`。下文保留2026-11-01交付历史。

2026-11-01，基于 `master` 的 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`，工作分支 `p/duanyao.0516/codex/only-one-door-package`。2026-11-01 用户授权完成大厅图标调整后合并推送远端 master。本文更新至最终 `016eb687` 内容包。最终 APK 构建、资源/权限/签名校验、29 项浏览器回归及下述独立 Android 模拟器交互验证通过；最终修复版已于 13:25 覆盖安装到 OPPO 并核对设备包哈希，保留存档、停在大厅供用户手动测速；厂商行为和真机速度仍待用户反馈。

初版 `44162e56` 报告已原样归档至 [previous-verification.md](../artifacts/level-devil/oppo-followup/previous-verification.md)。下文保留其模拟器验证历史，并与本轮结果分开标明。

## 交付内容与本轮修复

大厅新增第 21 款游戏「只有一道门」，打开独立横屏 WebView Activity。游戏 JS、PAK 和音频随 APK 打包，使用固定本地域名和逐文件校验加载，不依赖首次联网或网页缓存。原版单人、双人、关卡规则和存档格式保留；手机触控支持多指独立按住、滑入/滑出和取消，后台或切关清除旧输入。

移除 Poki SDK、广告调用、统计及外链入口。原广告解锁和跳关改为玩家主动点击的免费操作，保留原有资格判断、场景切换和保存流程，不在启动时解锁全部关卡。WebView 拒绝非本地清单资源、外部跳转、新窗口、下载、文件访问及权限请求。

本轮针对 OPPO 反馈修改了以下行为：

- **系统返回兼容。** OPPO 日志确认关闭预测返回时，即使 API 36 也会调用旧 `onBackPressed`；原先 SDK 判断将该路径忽略。现在 legacy 与 `OnBackInvoked` 都进入同一个返回计数器。第一次完成系统返回显示“再返回一次，回到大厅”，2.5 秒内第二次等待结算和存档写入后退出；取消的手势不计数，失焦/后台重置计数，保存失败仍提供重试/继续。没有自定义侧滑识别，也没有修改用户系统设置。[原始证据](../artifacts/level-devil/oppo-followup/backs-before.log)
- **导航区域。** 当前所有 API 26–36 均保留系统导航控件可见、请求透明导航背景，只隐藏状态栏；游戏窗口继续铺满屏幕并处理安全区。此策略用于避免隐藏导航触发厂商额外防误触层，是否解决 ColorOS 的第一层拦截仍待 OPPO 空闲后验证。
- **全屏和操作布局。** 移除原生 16:9 WebView 限制，网页画布占满窗口；原 427×240 游戏世界等比居中，两翼使用当前场景背景/主题色，原版操作精灵移至两侧。原生只传安全区，JS 换算布局，不重复添加内边距。
- **选关返回大厅。** 三个菜单新增使用原 actor 759 图标的左上返回入口，点击目标至少 48 CSS 像素，点击后走现有保存确认流程。原游戏已有返回按钮继续原来的关卡/菜单导航；保存失败并选择继续后，新入口可以重试。
- **启动工作量。** 原包同时预解码四倍率共 1,709 张图片；现在仅预解码当前倍率的 425 张加 9 张共用图，共 434 张。完整 PAK、四倍率资源、音频及显式加载能力仍保留；本次 WebView 固定纹理倍率，避免原全屏重载同步读取尚未解码的其他倍率。
- **首次展示等待。** 仅首次启动跳过 Scene 61 的 100ms 等待、61→125 的 250ms 淡出和 250ms 淡入，以及首次菜单 50ms 等待加 500ms 门帘动画。同步读档、场景初始化、原有双人数组追加均保留；一次标志不写存档，后续过渡保持原样。`ready` 仍由真实菜单初始化完成、非过渡中的更新触发；损坏存档保持失败状态，不发送正常 `ready`。

额外修复了真实 Android 多指 CANCEL 后下一次点击被吞的问题：原 Lime 取消路径不产生鼠标 UP，留下模拟鼠标按下状态。现在仅清理被取消主触点的鼠标状态，保留其他触点；后台抬手/取消可退休旧触点，但不会合成点击。

源码独立审查记录：[原生 OPPO 修复](../artifacts/level-devil/oppo-native-followup.md)、[全屏与预载](../artifacts/level-devil/fullscreen-startup-independent-review.json)、[首次展示优化](../artifacts/level-devil/first-boot-independent-review.json)。

## 当前最终 APK

- 构建命令：`bash scripts/build-level-devil.sh`。
- 文件：[XanaduYonder-0.9.4-level-devil-preview.apk](../artifacts/builds/XanaduYonder-0.9.4-level-devil-preview.apk)。
- 大小：321,382,631 字节，约 306.5 MiB，这是整个 21 款游戏合集。
- SHA-256：`46b3ef94d5b37593e869adb05e5d8aaa5d7553e343831ed07aa91272ae959dc0`。
- 独立包名：`com.duanyao.xanaduyonder.leveldeviltest`，不覆盖正式合集及其存档；非 Development、不可调试，使用本地测试签名。
- 完整权限列表只有原有 `android.permission.VIBRATE`；没有 INTERNET、网络状态、存储、定位、相机或麦克风权限。此游戏没有增加权限。
- 内容 AAR：5,089,999 字节，SHA-256 `016eb687e585665e2b958b868b46c3f01bf90ea39c2e6887023a2bac0f6a18f1`。
- 最终 Gradle 的 5 个 Java 宿主文件与生产源码逐字节核对通过；Activity SHA-256 为 `1a744610eb22898d642883d68c2c5319da907f9e9fd3e1ce749541d2cf349cce`。APK 签名检查通过。

[APK 校验结果](../artifacts/level-devil/apk-verification.json)、[Java 输入核对](../artifacts/level-devil/final-native-inputs.json)、[签名检查](../artifacts/level-devil/apk-signature.txt)、[构建日志](../artifacts/logs/build-level-devil.log)。APK 检查比较当前 AAR 的全部 27 个文件（26 个资源加私有 manifest）、逐项大小/hash、无广告 SDK/研究钩子，并确认现有王国保卫战 `game.love` 仍在。开发中旧 AAR 被错误打入 APK 的一轮构建曾被该门禁实际拦截；构建成功不能替代运行验证。

## 当前资源与浏览器验证

`build-content.py --verify` 和 `verify_original.py` 通过：原始 PAK、19 个 MP3 字节不变，2,138 个 gzip 成员全部通过边界/CRC 检查，184 对场景数据完整。场景包括菜单和特殊场景，不能把这个数字称为已通关的关卡数。

最终 `016eb687` 内容包的 14 项浏览器回归通过：离线启动、ready、触点所有权、独立宿主暂停、写入失败保留存档、结算保存并进入新场景后退出、真实多指取消、重试中断结算、按住输入跨场景、两个速通解锁入口、免费跳关退出、双人拖动释放、备份恢复、双份损坏拒绝静默重置。26 个生产资源请求全部来自本地，没有未预期拦截或脚本错误。

同一 `016eb687` 内容包的 8 项全屏回归也通过：三个菜单图标保存退出、写入失败后取消重试、DPR 3 安全区、四种视口保持 427×240 世界和等比缩放、单/双人原精灵两侧布局、真实触屏多指独立释放、双人六个触控区与缩放取消、48 CSS 像素最小返回目标且原游戏返回不变。首次过渡独立探针另确认：首次菜单无门帘、重载存档不变，后续菜单恢复两帘，再次经过 Scene 61 恢复原 0.25/0.25 秒过渡。

同一最终包另通过 7 项取消专项：主/副触点分别取消、全取消后的第一次 80ms 返回按压、无取消误点击、后台取消/正常抬手后恢复首次点击。Stage 的 click/releaseOutside 均未被清理事件触发。见 [取消专项报告](../artifacts/research/only-one-door-243372/runtime-validation/cancel-result.json)。

[14 项浏览器报告](../artifacts/research/only-one-door-243372/runtime-validation/browser-result.json)、[8 项全屏报告](../artifacts/research/only-one-door-243372/runtime-validation/viewport-result.json)、[内容实现说明](../ThirdParty/LevelDevil/README.md)。测试引擎访问器只注入测试浏览器响应，不在生产 JS/APK 中。历史免费操作 UI 和 prompt 传输检查不替代本轮最终 Android 运行验证。

## 本轮 Android 验证

使用独立 `OnlyOneDoorApi36` / `emulator-5584`，Android 16 / API 36、WebView 133.0.6943.137、320dpi；验证 720×1280 和 720×1582 两种屏幕，其中后者模拟 OPPO 的长屏比例，不能替代刘海和厂商导航测试。飞行模式开启，Wi-Fi/移动数据关闭。

自动化验证阶段按用户要求仅操作自有模拟器，没有操作其他任务的模拟器。之后用户明确要求安装最新测试包，已于 13:25 将 `46b3ef94` APK 覆盖安装到 OPPO，设备端 SHA-256 与本地产物一致，保留存档并停在大厅；未提前进入游戏，未测量此包的 OPPO 启动耗时。安装证据：`artifacts/level-devil/oppo-followup/latest-oppo-install.json`。

| 最终包检查 | 实际结果与证据 |
| --- | --- |
| 全屏、选关入口、保存退出 | 原比例画面与两翼背景，左上原图按钮；菜单按钮保存后回 Unity。[选关页](../artifacts/level-devil/oppo-followup/release-first-click-after-cancel.png)、[大厅](../artifacts/level-devil/oppo-followup/release-menu-return-hall.png) |
| 单/双人、触控取消后第一次点击 | 实际 Android 双指移动并 CANCEL，随后第一次 80ms 点击就从双人关卡返回原地图；重现的旧包需点击两次。[注入记录](../artifacts/level-devil/oppo-followup/release-duo-cancel.log)、[第一次点击结果](../artifacts/level-devil/oppo-followup/release-first-click-after-cancel.png) |
| 预测返回、取消手势 | 最终包在自有模拟器恢复预测返回并重启后，取消侧滑不计数；随后两个真实侧滑分别只产生一次 `onBackInvoked`，第二次保存返回大厅。[日志](../artifacts/level-devil/oppo-followup/release-predictive.log)、[大厅](../artifacts/level-devil/oppo-followup/release-predictive-return-hall.png) |
| legacy 系统返回、后台恢复 | 自有模拟器关闭预测返回并重启，实际侧滑到 `onBackPressed`；Home/最近任务恢复后第一次仍只提示，第二次保存退出。[日志](../artifacts/level-devil/oppo-followup/release-native.log)、[大厅](../artifacts/level-devil/oppo-followup/release-resume-two-back-hall.png) |
| 存档延续 | 覆盖安装旧/新包仍保留进度与死亡计数 4，最终选关截图可见；读档失败和结算退出事务另由最终 14 项运行时回归覆盖 |

最终包的两条系统返回路径均已验证。原生宿主 Java 在本轮三个内容版本之间保持一致。中间版 `f5822170` 还通过 2.5s 超时重新计数、CANCEL 不计返回和双人取消后角色停止；最终包修复了该测试暴露的下一次菜单点击问题。

第二世界选关的蓝色原图返回按钮由最终浏览器检查真实点击覆盖；Android 中间版布局截图见 [第二世界](../artifacts/level-devil/oppo-followup/final-second-world-map.png)，其画布/菜单代码在最终包中未改变。当前没有在 Android 新通关全部关卡；单人测试包含原陷阱死亡和重试，未把失败尝试记成通关。初版通关与存档记录保留在下方历史段。

## 启动速度采样与限制

截图采样从点击派发开始，每约 350ms 轮询一次，记录加载遮罩消失后首次观察到游戏画面的时间上界；它包含 Android 启动、截图及轮询开销。原生日志的 `sinceCreateMs` 从 Activity 创建计时到收到 `ready`，与点击到可见画面的指标不同，不能混用。

| 内容与容器 | 点击到首次画面上界 | 证据 |
| --- | --- | --- |
| 旧 `44162e56` 内容 / 独立宿主，同一模拟器 | 冷启动 3.858s、5.102s；再次进入 3.833s | [冷 1](../artifacts/level-devil/oppo-followup/emulator-old-content-cold1.json)、[冷 2](../artifacts/level-devil/oppo-followup/emulator-old-content-cold2.json)、[再次进入](../artifacts/level-devil/oppo-followup/emulator-old-content-warm1.json) |
| 预载优化 `ffefb4d5` / 独立宿主，同一模拟器 | 冷启动 2.683s；再次进入 1.984s | [冷启动](../artifacts/level-devil/oppo-followup/emulator-new-content-cold1.json)、[再次进入](../artifacts/level-devil/oppo-followup/emulator-new-content-warm1.json) |
| 启动优化中间版 `f5822170` / 完整合集，高并行负载 | 冷启动 5.783s；再次进入 5.327s、5.478s | [冷启动](../artifacts/level-devil/oppo-followup/final-f582-cold1.json)、[再次进入 1](../artifacts/level-devil/oppo-followup/final-f582-warm1.json)、[再次进入 2](../artifacts/level-devil/oppo-followup/final-f582-warm2.json) |

中间版 `f5822170` 完整合集这三次 Activity 创建到 `ready` 分别为 4.850s、3.887s、4.367s，见 [原生日志](../artifacts/level-devil/oppo-followup/final-f582-startup.log)。此时主机有多模拟器及构建并发，高 CPU 负载造成明显波动；独立宿主不含 Unity，不能直接与完整合集比较。这些中间版高负载样本完整保留，没有因为速度慢而删去。随后对同一模拟器先后覆盖安装旧/新完整合集，保留存档、相同 80ms 按压和截图口径：旧版冷启动 **5.110s**，最终版冷启动 **2.779s**；最终版长屏再次进入 **2.386s**。见 [旧整包](../artifacts/level-devil/oppo-followup/paired-old-full-cold.json)、[最终整包](../artifacts/level-devil/oppo-followup/paired-final-full-cold.json)、[再次进入](../artifacts/level-devil/oppo-followup/release-wide-warm-valid.json)。模拟器重启后最终包的另一首次进入样本为 **3.056s**，见 [重启后样本](../artifacts/level-devil/oppo-followup/release-predictive-cold-valid.json)。这是有限次数、仍受主机并发影响的模拟器样本，**不是稳定秒开承诺，也不是 OPPO 的实测加速幅度**。首屏阶段日志证明图片仅预载 434 张，原开场展示等待已移除；首次贴图等工作仍有成本。无效样本（前台被抢占、未真正重新启动）均标记 valid=false，不参与对比。

## 初版 Android 模拟器验证历史（仅 `44162e56`）

以下结果对应旧内容 `44162e56`、Activity `5b05c929` 和初版完整 APK `f7a990f2`，当时为 API 31+ 隐藏系统栏的旧策略。它们保留历史覆盖范围，**不构成当前最终包的完整设备验收**。当时开启飞行模式、关闭 Wi-Fi 和移动数据。

| 检查 | 实际结果与证据 |
| --- | --- |
| 初版合集大厅入口 | 显示 21 款，只有一道门原图标和入口可点击，[截图](../artifacts/level-devil/emulator-validation/final-lobby-entry.png) |
| 初版合集冷启动 | 从大厅打开横屏原版菜单，全程断网，[截图](../artifacts/level-devil/emulator-validation/final-offline-game.png) |
| 初版合集触控通关 | 通过真正的 Android 双指 DOWN/POINTER_DOWN/UP 操作移动和跳跃，通过第一小关进入 Pits 2，[截图](../artifacts/level-devil/emulator-validation/final-stage2.png) |
| 取消返回手势 | 侧滑后撤回并注入 CANCEL，随后第一次完整侧滑仍只提示；第二次才退出，[截图](../artifacts/level-devil/emulator-validation/final-first-back.png) |
| 返回大厅与输入恢复 | 第二次侧滑返回竖屏大厅，随后可滚动和再次启动，[返回截图](../artifacts/level-devil/emulator-validation/final-returned-lobby.png)、[操作恢复](../artifacts/level-devil/emulator-validation/final-lobby-input-restored.png) |
| 初版合集存档重进 | 退出后从大厅重新打开、进入第一世界，仍为 Pits 2，[截图](../artifacts/level-devil/emulator-validation/final-reentered-save.png) |
| 生命周期 | 初版合集 Home→最近任务[恢复通过](../artifacts/level-devil/emulator-validation/final-home-resume.png)；熄屏/唤醒后可继续完成返回动作，无宿主异常 |
| 双人多指与取消 | 相同生产宿主/内容的独立测试包中，两名角色各自的右移按钮同时响应，CANCEL 后均释放，[按住](../artifacts/level-devil/emulator-validation/host-duo-held.png)、[取消后](../artifacts/level-devil/emulator-validation/host-duo-canceled.png) |
| 现有外部游戏边界 | 王国保卫战入口可启动至原运行时，但 SwiftShader 不支持其 ASTC8x8 纹理，未完成游戏运行回归；错误页可返回大厅，[错误证据](../artifacts/level-devil/emulator-validation/kingdom-rush-regression-start.png)、[返回证据](../artifacts/level-devil/emulator-validation/kingdom-rush-error-return.png) |

原生回归发现并修复两项浏览器无法发现的问题：窗口内容未挂载就取得 InsetsController 导致 API 36 首启异常；TRANSIENT 系统栏模式吞掉第一次侧滑。修复后重新构建宿主及整个合集，并实测上述流程。详见 [窗口初始化记录](../artifacts/level-devil/decor-startup-regression.md) 和 [系统返回记录](../artifacts/level-devil/system-back-gesture-regression.md)。另有 URL/Back 时序的 25 项临时 Java 行为检查及独立代码审查；测试注入器和宿主小包只在忽略的 artifacts 目录，不增加生产权限或调试接口。

## 验证边界

- 最终修复版已装到 OPPO 供用户测试；ColorOS 防误触、刘海/导航布局及实际性能尚无此次新包的真机回归结论。API 26–35 也未做本轮设备回归。
- 本轮覆盖上述最终模拟器行为和 14+8+7 项浏览器检查；未做所有 Android 版本、所有设备尺寸或全部关卡穷举。
- 原版资源与所有场景数据已完整保留并校验，没有逐关通关全部内容。
- 模拟器使用软件 GPU、关闭音频输出；已有声音状态与暂停逻辑检查，但未做扬声器听感或真机帧率/功耗评测。
- 没有重新回归合集全部 21 款游戏。王国保卫战资源/hash 验证通过，但受历史模拟器纹理能力限制，其玩法回归未完成；没有为此修改该游戏的纹理或运行时。
- 本次不发布、不覆盖正式包、不合并分支。原始游戏资源及依赖的来源/署名见 [THIRD_PARTY_NOTICES](../THIRD_PARTY_NOTICES.md)。

## master 交付补充（2026-11-01）

合并远端 `1daf099`，保留割绳子与全局系统返回修复；只有一道门入口编号调整为 22，三个外部游戏统一检查忙碌状态。小图标为大厅同色同线宽的原创门图，大 Logo 使用用户提供的 72×72 黄色原图。确定性内容校验与 14 项主浏览器回归本轮再次通过，AAR 仍为 `016eb687`。按用户最新要求，本次合并与图标改动未跑 Unity / Android 构建，也未更新 OPPO 安装包。此前 APK 和设备验证均指上文记录的旧产物。
