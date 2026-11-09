# Q版泡泡堂初代离线接入

2026-11-08 基于已同步的远端 `master`（`0d25dad`）接入。Android、iOS 共用原版游戏和触控适配，大厅新增第 27 号入口；现有 22 个入口保留，共 23 个。初次开发验证使用 0.9.6 / build 15 预览包；正式 Android 内容随 [v0.9.7 / build 16](releases/v0.9.7.md) 发布，iOS 本轮不发布新包。

## 游戏与手机操作

使用 [4399 初代页面](https://www.4399.com/flash/3881.htm) 对应的完整 `game.swf`，由固定 Ruffle 0.7.1 执行。保留原版人机、同屏双人、难度、角色、关卡和规则；没有接入联机服务。

生命道具已按原版源码和实际 Ruffle 运行核对：生命从1增加到2后，受伤仍结束本回合角色，但仅扣到1；原结算页的“重玩此关”可用剩余生命重新挑战同一关。盾牌才抵挡一次伤害。“基础道具”说明已加入这个具体例子；未修改原游戏的生命或死亡规则。定向夹具与重试截图在 `artifacts/bomb-it/life-diagnostic/`，诊断入口不进入生产包。7代仅做过文件评估，用户已要求暂缓，未加入项目内容。

- 横屏中央以原版 550×400 比例完整显示并贴底。画面矩形只取决于屏幕和安全区，不随摇杆／方向键、单人／双人变化。控件独立布局；窄屏的大圆环可半透明覆盖非交互边缘，但让开原游戏左下按钮，不裁切或拉伸画面。
- 单人左侧移动、右侧放弹；双人左右各有移动与放弹操作。默认浮动摇杆，设置可换四向圆环；偏好仍保存在 `xanaduyonder.bomb-it.controls.v1`，不影响原版排行。切换操作方式不重开对局。
- 浮动摇杆闲置时显示示意，以首次触点为实际中心，按下不走、滑动超过死区才移动；操作时示意让位于实际摇杆，松手恢复。正式对局启用所属半屏触区，排除工具、放弹、原游戏按钮与系统安全区。Android 的系统手势起点区只排除空白处的新浮动摇杆起点，不扩大视觉留白，也不拒绝已送达可见方向键、摇杆示意或放弹按钮的点击；系统实际接管手势时仍通过 cancel 松键。真实刘海和可见系统栏安全区仍保留。
- 圆环默认 144 个逻辑像素、中央 40 像素中性区，菜单、选角、对局和结算使用同一几何位置；单人右侧放弹与左控件等径、中心水平对齐，双人两组对称。空间受限时按屏幕尺寸统一缩放，不随游戏阶段跳变。圆环使用真实四扇区命中与换向滞回，只输出一个原版方向。触点按下时固定玩家归属；取消、失焦、后台、页面／模式／样式／安全区变化都释放输入。
- 左侧人数按钮切换真实游戏模式：确认后结束旧对局、重建实例，进入目标人数的原版难度和角色选择；取消不改变当前对局，点击当前人数不重开。真实游戏状态决定按键映射，选角时两位玩家仍需分别确认。
- 暂停图标、原版暂停按钮和 P 键共用手动状态；设置、说明、确认弹窗、后台分别维护宿主暂停原因，不能互相错误解除。桥在恢复前补偿原计时基准，保留未满 500ms 的实际游玩时间。切后台静音并暂停，返回后需点继续。设置与说明支持关闭按钮和点击遮罩，关闭不会取消原来的手动暂停；说明的“知道了”随正文滚动，放在最后一段之后并靠右对齐。
- 设置采用紧凑的操作方式、声音、触感三行，弹窗宽度最多 304 个逻辑像素；右侧暂停、设置、说明缩为 34 像素圆角按钮并排成一行。左侧大厅行与人数切换组保留 12 像素间距。声音与原版音乐按钮、Unity 全局偏好同步；触感是放弹按键按下的轻触反馈，开启开关可试听一次，不代表原版具备爆炸或击杀触感。实际效果仍受系统触感设置约束。native 先将偏好写入与“只有一道门”共用的全局 journal，再回传结果；Unity 返回或冷启动时导入，将 revision 与两项偏好一起持久化，完整 Save 成功后确认。ACK 墓碑阻止旧文件重新迁移，相同 revision 不再重放旧设置。
- 新增“继续闯关”：入口显示“继续闯关 · 难度 · 第xx/xx关”。保存人数、难度、关数、角色、关卡开局生命与累计分（包括电脑），保留原排行榜字段。胜利记录下一关，仍有生命的失败记录当前关，通关或生命耗尽清除。再次进入后从该关开局继续，地图重新生成；不恢复中途人物位置、炸弹、临时道具和剩余时间。保存失败会显示重试入口，退出前刷新失败不会销毁当前播放器。
- 原版简单15关、普通20关、困难25关，同一档内没有按关数逐步强化电脑。地图从5种场景主题随机生成，特殊武器每关轮换两种。双人是人类阵营合作；超时只比较仍存活双方的最高本局分数，人类必须严格更高，平局失败。说明列出三档区别、六类基础道具和六类特殊武器，依据固定原版动作核对。

大厅大 Logo 为原 SWF 的 bitmap 425（151×152）原样导出；小图标为本项目统一绿色描边的原创炸弹 SVG。二者分别用于大卡和紧凑布局。

## 离线净化与边界

原 SWF 为 2,167,060 字节，SHA-256 为 `a2b4cc65115e4b6eb813962d80e91f1e65746a91f27e91d5e86fa1963ed5b54d`。构建先核对输入散列，再按已审查标签删除 `FWAd_AS2` 的两个符号、两个导出和两个初始化动作，并清除 `FileAttributes.UseNetwork`。不重编译游戏动作，不包含站点 `mainload.swf`、`as3.swf`、广告／News 资源或网站页面。

首版 v0.9.7 净化 SWF 为 2,163,057 字节、SHA-256 `2127f525d471450fe3d39a477b489892b6a500e5e3162debc0cc6a52e2ec632a`，保留游戏动作逐字节一致。2026-11-09 的暂停、模式与检查点修复新增受限桥：当前独立审计确认 53 份原脚本不变、6 份精确追加、2 条暂停动作替换，1,033 个根标签不变，外部导航和缺失字符引用均为0。当前 SWF SHA-256 为 `43740bd89e3e101506ece7ec75ffef9e49e311f40d45ea0cae87f38d74f133af`。锁文件逐项记录原标签与新增字节散列；现在明确报告 `gameplayTagsByteIdentical: false`。独立审计方法和再生成命令见 [内容说明](../ThirdParty/BombItContent/README.md)。

Ruffle 0.7.1 把 ExternalInterface 与 `allowNetworking: all` 绑定，因此受限本地桥使用该选项及 `allowScriptAccess: true`。这不授予系统网络权限：CSP 仍只允许本地来源，`openUrlMode: deny` 不变；Android 固定离线 HTTPS origin、iOS 固定 loopback 端口 24684，仅提供 SHA-256 清单中的本地资源，宿主拒绝外部导航、弹窗及权限请求。广告和网络内容仍结构化删除。Android 不新增权限，最终 manifest 继续移除 INTERNET。

## 构建与检查

日常入口仍是 `./scripts/build.sh android` 或 `./scripts/build.sh ios`。前者在 Unity 前构建 `bomb-it-content.aar`，后者通过相同纯构建函数 staging 到过滤后的 iOS 副本。固定输入位于 `ThirdParty/BombItContent/` 和 `ThirdParty/Ruffle/`；生成结果位于 `artifacts/bomb-it/content/`，不会把未净化原 SWF 打入包。

`scripts/bomb-it/build-content.py --verify` 检查输入、净化报告、完整资源和 AAR；`verify-package.py <APK/IPA/APP/Xcode目录>` 检查实际包中的文件集合、大小和散列。Ruffle 两个 WASM 兼容变体及两份原许可保留，开发 source map 不打包。来源和许可见 [THIRD_PARTY_NOTICES](../THIRD_PARTY_NOTICES.md)。

首版已完成源内容审计、Android Java 编译、iOS ARM64 原生语法编译和 staging 检查，真实 Chrome 曾覆盖原版菜单、选角、多指、释放、音量及退出。该轮只检查播放器挂起和画面冻结，未证明恢复后的倒计时正确；安全区下也未断言两种操作方式画面矩形一致，因此遗漏了本轮两类缺陷。历史证据在 `artifacts/bomb-it/browser/`，不能替代新桥回归。

当前回归入口为 `scripts/bomb-it/check-touch-lifecycle.cjs`，通过 `PLAYWRIGHT_MODULE` 指定本机 playwright-core。`--baseline` 固定旧内容记录画面缩小和偏心触摸立即移动；`--short` 检查真实模式、输入、HUD 和布局；`--gesture-edges` 检查非零系统手势区下可见控件外缘、单/双人、四指、归属与取消，证据位于 `touch-gesture-edges/`；`--clock-only` 运行生产 SWF 的 181 秒真实暂停及连续短暂停；`--near-expiry` 使用只改初始时间常量的定向夹具验证临近超时，不替代生产内容验证。证据分别位于 `artifacts/bomb-it/touch-baseline/`、`touch-short/`、`touch-clock/` 和 `touch-near-expiry/`。浏览器检查不等于手机声音、性能或双人手感验收。

本轮设置与续关集成检查为 `scripts/bomb-it/check-feedback.cjs`：实际 Ruffle/SWF 验证偏好 ACK 后生效、失败保留实际状态并可重试、旧 token 隔离、原音乐按钮同步、放弹触感消息门控、说明与遮罩暂停关系，以及浏览器重载后通过真实续关入口恢复。native ACK 在这组检查中明确模拟，不证明物理声音或震感。截图与结果位于 `artifacts/bomb-it/feedback-browser/`。真实 SWF 的第7→8关、非默认角色/生命/分数、失败重试、终局清槽与容量失败证据分别位于 `checkpoint-browser/`、`checkpoint-campaign-browser/`；定向状态由 artifacts 夹具构造，生产内容不含诊断接口。

Android Java、iOS ARM64 原生语法，以及 Runtime C# 的 Android/iOS 两种条件编译通过，证据位于 `artifacts/bomb-it/native-feedback-check/`。两个平台的 native 偏好 journal 与 Unity Save 确认链已经交叉源码审查；两端实际声音、触感、系统手势手感仍须真机验收。

2026-11-09 后续边缘点击修正：旧版把 `gestureInsets` 在可见控件命中前整片排除。实际浏览器中，同一左方向外缘在 inset=0 时发出左键，inset=48 时被主动丢弃；将限制收窄到空白浮动摇杆起点后，该点和双人四组控件外缘均恢复。旧包红灯、单改输入后的绿灯、最终界面整合后的绿灯分别保存在 `touch-gesture-baseline/`、`touch-gesture-fixed/`、`touch-gesture-integrated/`。最终 `--short` 9项、`--gesture-edges` 6项、设置/续关 9项检查通过，页面错误为0；两个输入检查实际加载的资源散列与当前内容一致。界面截图覆盖844×390、740×328、640×360、915×412；独立检查补充568×320安全区布局。没有操作手机，本轮不重复修改SWF或native触摸分发。

本轮边缘/UI修正版统一Android脚本退出0，APK为 `artifacts/builds/XanaduYonder-0.9.7-debug.apk`，233,724,544字节，SHA-256 `08b2a81f7623b293b424eccebdd1bf91bea2f7c44c07e448b5a7482d2d6b1c85`。签名、运行时与包内14个泡泡堂文件检查通过；构建期间6个适配器输入保持不变。用户在编译期间授权直接安装，已用 `adb -s 3B6F4NE8A66ZXTRL install -r` 向OPPO覆盖安装，返回 `Success`；没有卸载、清数据、启动或操作游戏。设备手势、声音与双人手感由用户验收。

上一轮7项修正的 Android 构建于2026-11-09通过，统一脚本退出0；APK为 `artifacts/builds/XanaduYonder-0.9.7-debug.apk`，233,724,260字节，SHA-256 `2131028efbce0118ad84af94ecf8d09bad7fb58bc8371e5e5ef67768b727a79e`。沿用测试包 `com.duanyao.xanaduyonder.debug`、0.9.7/build16。签名、ARM64非Development运行时、无INTERNET权限、移除内容清单检查均通过；包内14个泡泡堂文件与最终输入完全一致。最后的小屏圆形放弹命中修正已纳入该包；实际浏览器最终加载的10个资源散列逐项匹配。新SWF的181,003ms暂停仍保持179.836秒剩余时间，恢复后才递减。未安装、启动或操作手机；安装命令交由用户执行。

2026-11-09 电脑端检查通过：同一屏幕和安全区下四种人数／操作组合画面矩形一致；首次落指中性、四指并行、跨半屏归属、输入释放、原暂停按钮两次点击、模式确认／取消和旧实例回调隔离均通过。生产 SWF 暂停 181,005ms 后剩余时间仍为 179.849 秒；恢复后正常递减。12 次短暂停间实际运行 2.907 秒，扣时 2.935 秒，未把不足 500ms 的活跃时间返还。剩余约 2 秒的定向夹具暂停后仍可继续，随后正常超时。最终短回归记录的 8 个实际加载资源散列与构建内容一致；这些常规回归未记录 JavaScript 错误。

`--near-expiry` 会自动生成夹具；`--prepare-near-expiry` 可单独生成，不需要 Playwright，且不会写入生产内容。`--lifecycle-errors` 检查销毁失败后的退出取消、重试与模式切换错误状态。极端的恢复后立即销毁曾出现三条 AudioContext pending-resume/close 拒绝，业务退出与重新进入仍完成；带 150ms 观察帧以及后续零延迟复跑均通过，但不能据此宣称该音频竞态已消除。首次转录保存在 `artifacts/bomb-it/rapid-retry-first-observation.json`，未取得原始调用栈；`--lifecycle-errors --rapid-exit-retry` 保留零延迟检查入口。

上一轮触控与暂停修复的统一 Android 构建以 0 退出，测试包为 `artifacts/builds/XanaduYonder-0.9.7-debug.apk`，当时为233,706,103字节、SHA-256 `c6bf2d71d74a2b8b22ff613c6f9ee663f2f1cd0adb4d86901dac775699e9b0b3`。包名 `com.duanyao.xanaduyonder.debug`、versionCode 16。该散列属于上一轮交付，后续同名包会更新。修改保留在隔离分支 `p/duanyao.0516/bugfix/bomb-it-touch-pause`，未夹带原工作区的其他玩法变更。

用户在编译期间改为稍后自行安装，本次没有安装或启动手机应用。连接原 OPPO 后，使用 `adb -s 3B6F4NE8A66ZXTRL install -r <上述 APK 的完整路径>` 覆盖测试包；不卸载、不清数据。真机操作、声音和双人手感仍由用户验收。本次未构建或发布 iOS，也未更新 GitHub release。

## 首次接入与上一版交付记录

Android 完整构建通过，APK 为 `artifacts/builds/XanaduYonder-0.9.6-debug.apk`，233,399,543 字节；对比本机接入前 APK 增加 12,404,374 字节（约 12.4 MB）。最终包内容与已审查资源逐文件一致，ARM64、non-debuggable、无 INTERNET 权限、无 Unity Development Console。拉取 master 后曾遇到既有三个网页游戏的旧内容缓存，按各自现有脚本重建后通过，不修改它们的玩法源码。

上一版仅更新 Web 适配器时，Unity 自动复用了原生资源：构建日志明确提示 `player data was not rebuilt`，因此 `packedAssets` 为空，严格资源清单检查停止。按 [Unity 6000.3 缓存说明](https://docs.unity3d.com/6000.3/Documentation/Manual/build-cache-location-reference.html)，将 Android 的 `Library/PlayerDataCache/Android/DataBuildDirtyInfo.json` 可逆备份到 `artifacts/bomb-it/build-cache-backup/`，随后使用原统一入口触发资源重建。没有跳过清单检查、修改玩法或清空全局构建缓存。

重建后重新生成 1,124 条真实资源清单，统一构建脚本以 0 退出，全部包内检查通过；扩大布局的新包再次向 OPPO 覆盖安装，ADB 返回 `Success`。两次安装都没有执行真机游戏自动操作。

按用户要求，已向连接的 OPPO 覆盖安装现有 `com.duanyao.xanaduyonder.debug`，ADB 返回 `Success`；没有卸载、清数据或操作游戏验证。

iOS 上一版接入的 Unity 导出和 Xcode Release 真机构建已成功，沿用 `com.duanyao.xanaduyonder.iosnative` 及既有开发签名，签名完整性和实际 APP 的泡泡堂资源散列通过。本轮扩大画面／图标／控制方式设置后，按用户要求暂停 iOS 编译与安装；旧 `artifacts/ios/DerivedData-web-games/Build/Products/Release-iphoneos/XanaduYonder.app` 不包含本轮布局改动，后续安装前须重新构建。尚未进行 Android／iPhone 真机玩法、声音、性能或双人手感验收，按用户要求由用户操作设备验证。
