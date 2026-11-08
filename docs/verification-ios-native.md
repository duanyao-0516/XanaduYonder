# iOS 原生小游戏构建记录

## 范围

分支 `p/duanyao.0516/feature/ios-native-games`。应用标识 `com.duanyao.xanaduyonder.iosnative`，版本沿用 `ProjectBuild.Version` / `VersionCode`。iOS 大厅保留 19 个入口，战争进化史入口含两代；纯 Unity 黄金矿工保留。切水果、只有一道门、经典黄金矿工、赵云与阿斗、割绳子、王国保卫战暂不包含。

Android 的 25 个入口保持原顺序。原工作树不用于切换 iOS 构建平台；所有 Unity 导出操作在 `artifacts/ios/project` 副本进行。六款游戏专属 Resources / StreamingAssets 和 Android 插件在复制时排除，而非仅隐藏入口。

## 使用

1. Unity Hub 给项目固定版本安装 iOS Build Support；本次为 6000.3.27f1。
2. 安装兼容 macOS 的 Xcode，首次运行由使用者确认 Apple 许可并完成组件初始化。
3. 在仓库根运行 `./scripts/build-ios.sh`，输出 `artifacts/ios/xcode/Unity-iPhone.xcodeproj`。可选 `XANADU_IOS_TEAM_ID` 只传 Team ID，不传账号密码。
4. 在 Xcode 登录自己的 Apple Account，选择 Personal Team，连接已信任的 iPhone，启用手机 Developer Mode，再构建安装。首次启动还需在手机 `Settings → General → VPN & Device Management → Developer App` 信任自己的账号。导出 Xcode 工程不等于已生成可安装的签名包。

本次已完成后续签名和安装。排障期间生成工程可能含临时诊断代码；制作正式包应先重新运行 `scripts/build-ios.sh`，再进行个人签名构建。环境变量填写自己的 Team ID 和设备 ID：

```bash
xcodebuild -project artifacts/ios/xcode/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -configuration Release -sdk iphoneos \
  -destination "id=${XANADU_IOS_DEVICE_ID:?请设置目标设备 ID}" \
  -derivedDataPath artifacts/ios/DerivedData \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
  "DEVELOPMENT_TEAM=${XANADU_IOS_TEAM_ID:?请设置个人 Team ID}" \
  CODE_SIGN_STYLE=Automatic build

xcrun devicectl device install app --device "$XANADU_IOS_DEVICE_ID" \
  artifacts/ios/DerivedData/Build/Products/Release-iphoneos/XanaduYonder.app

xcrun devicectl device process launch --device "$XANADU_IOS_DEVICE_ID" \
  com.duanyao.xanaduyonder.iosnative
```

Xcode 必须仍有可用的账号登录状态。重新构建后应核对描述文件有效期；自动续签尚未配置。生成工程中的个人团队配置与证书/描述文件均不提交到 Git。

构建记录：`artifacts/logs/build-ios.log`；副本清单：`artifacts/ios/staging-manifest.json`；实际打包资源：`artifacts/ios/packed-assets.txt`；导出摘要：`artifacts/ios/export-summary.txt`。

## 已取得的证据

- 2026-11-07：USB 检测到用户的 iPhone 17 Pro，ProductType `iPhone18,1`、iOS 26.5；配对验证成功。开发者模式最初关闭，通过设备工具成功显示设置入口，启用需要用户在手机确认。
- 已安装 Unity 6000.3.27f1 iOS Build Support、Xcode 26.6 (17F113)、libimobiledevice 1.4.0_2。Xcode 首次许可与账号由用户完成。
- 条件编译前后校验：iOS 入口从 25 变为精确 19；Android 入口及顺序不变。Android 专属 Editor 类型不进入 iOS 编译；两个公用准备函数仅调整可见性。
- 隔离脚本的语法、排除清单、源文件不变、软链接拒绝、失败退出码传播、无新产物拒收、日志失败处理已通过临时检查，记录在 `artifacts/ios-script-verification/results.txt`。没有新增正式测试源码。
- 首次 Unity iOS 导出成功：副本含 2761 个文件、349111508 字节，保留 GoldMiner 166 个文件；BuildReport 包含 1347 个资源路径，排除内容检查通过。独立源码审查未发现阻断问题。
- 实际 Unity iOS 目标 PlayMode 校验通过：调用生产 `BuildLobby`，19 个唯一入口，双列/目录/单列各 19 张卡片；未运行 AppController.Awake 或初始化游戏存档。报告为 `artifacts/ios/catalog-check.txt`。临时检查器只放在构建副本内，最终重新导出的副本与正式源码均不包含该检查器。
- 最终版 `scripts/build-ios.sh` 已从新的副本完整执行，退出码 0，日志含 `IOS_EXPORT_PASS` 与最终 Xcode 工程路径。脚本开发期间的首次运行曾因同一脚本正在更新而在末尾发生 shell 错误；最终版本已重跑通过，不以首次 shell 结果作为完成依据。
- 原仓 `Assets/Plugins/Android` 与切水果 StreamingAssets 仍存在，未因 iOS 资源过滤被删改。生成工程支持设备族 `1,2`（iPhone+iPad），最低系统 15.0，自动签名。
- 2026-11-08：用户完成 Xcode 许可/账号初始化及手机 Developer Mode 重启确认；设备再次报告 `enabled`、`paired` 和 DDI services available。Xcode 的 iOS 平台组件最初未下载完，导致 destination 检查失败；组件就绪后原命令成功，无需修改游戏代码。
- Xcode 26.6 / iPhoneOS 26.5 SDK 的 `Release` 真机编译退出码 0，日志含 `BUILD SUCCEEDED`。产物为 `artifacts/ios/DerivedData/Build/Products/Release-iphoneos/XanaduYonder.app`，原生编译日志 `artifacts/logs/build-ios-native.log`。
- 最终 `.app` 校验通过：Bundle ID 正确，版本 `0.9.5` / build `14`，iPhoneOS 平台，设备族 `1,2`，主程序与 UnityFramework 均含 ARM64，`codesign --verify --deep --strict` 通过；embedded profile 包含目标 iPhone，签名 app identifier / team 与 profile 一致。报告 `artifacts/ios/signed-app-verification.json`。
- 实际 profile 的 `LocalProvision=true`、有效时长 168 小时，到期时间 `2026-11-14T16:12:20Z`，即北京时间 **2026-11-15 00:12:20**。这是当前这一份签名的期限。
- `devicectl` 安装成功，设备 app 列表确认版本 `0.9.5` / `14`。首次启动被 iOS 的 developer trust 检查拒绝；用户在手机信任后，同一安装包成功启动，进程 PID 744，随后进程检查仍在运行。证据分别为 `artifacts/ios/device-install.json`、`installed-app.json`、`device-launch.json`、`device-running-process.json`。
- 通过 Xcode 原生 `Take Screenshot` 获取实际 iPhone 17 Pro 大厅画面：显示“全部游戏 · 19 款”，可见数独、数字华容道、2048 等卡片。截图为 `artifacts/ios/iphone17pro-lobby.png`。这证明真机渲染成功，不等于所有游戏触控与存档均已回归。
- 已打包开发签名 IPA：`artifacts/builds/XanaduYonder-0.9.5-ios-native.ipa`，281791343 字节（约 281.8 MB），ZIP 完整性检查通过；SHA-256 记录在 `artifacts/ios/ipa-summary.json`。该包受当前个人签名的授权设备和有效期约束。

`export-summary.txt` 保留 Unity 导出时的阶段快照（其中原生编译/签名/安装为 `NOT_RUN`）；后续完成状态以 `artifacts/ios/native-device-summary.json` 和上述实际证据为准。

## 声音与触感后续验证（2026-11-08）

- 原安装包中跳一跳与 Flappy Bird 无声；用户已排除系统静音并确认其他 App 可以播放。诊断时声音开关开启，音源与监听器有效，Unity 主混音 PCM 非零、DSP 时间正常推进，iOS 输出路由为扬声器。会话激活成功本身没有恢复可闻声音。
- 对照结果：只配置 Playback / MixWithOthers 与激活仍无声；只在原 Ambient 策略下重启 Unity 输出也无声；配置并激活会话后跨一帧 StopAudioOutput / StartAudioOutput 可以恢复。原生提示音曾用于隔离实验，正式候选不含提示音或原生播放器。
- `IOSAudioOutputBootstrap` 与 `XanaduAudio.mm` 实现首次前台启动的一次启动恢复流程，在输出停止前与恢复前准备会话，没有每游戏播放声音，也没有固定秒数等待。后续生命周期仍由 Unity 管理。该处理是已验证有效的兼容恢复方案，**底层根因尚未证明**，不能据此断言 Unity 或 iOS 存在某个确定缺陷。
- 干净候选经完整 Unity 导出、Xcode 真机编译、签名检查和覆盖安装。用户确认跳一跳、Flappy Bird 的声音，以及回桌面再返回后的声音正常；存档正常。证据：`build-ios-feedback-native.log`、`ios-feedback-cold-launch-confirmed.log`、`signed-feedback-app-verification.json`、`device-install-feedback.json`（均在 artifacts 的对应日志或 ios 目录）。
- 可恢复的干净候选保存在 `artifacts/ios/feedback-candidate/XanaduYonder.app`，对应 IPA 为 `artifacts/builds/XanaduYonder-0.9.5-ios-native-feedback.ipa`。首次触感验收时手机的 System Haptics 关闭；后续单变量对照结果如下。
- iOS 触感桥接已接入 GameFeedback 与 DinoAudio。全链路日志捕获 18 次游戏请求及 A/B/C/D 各 3 次对照请求，均通过主线程、前台状态、窗口检查，实际执行到原生 API。扫雷在声音开和关时各有 3 次有效触感请求。日志证明请求到达，不以 API 返回声称硬件已经振动。
- 用户确认 Accessibility → Touch → Vibration 为 On，Sounds & Haptics → System Haptics 为 Off；关闭时长按主屏 Camera 图标仍有反馈。只把 System Haptics 改为 On，同一个安装包的 A（原始轻触感）、B（持久轻触感）、C（持久重触感）和扫雷都恢复了震感。因此本机游戏无触感的原因确认为系统触感设置抑制；原实现无需改强度或对象生命周期。D 使用另一套 AudioServices 震动接口，仍无震感，其原因未定位；正式游戏不使用 D。
- [Apple 的 UIKit 触感说明](https://developer.apple.com/documentation/applepencil/playing-haptic-feedback-in-your-app) 明确列出系统 Haptics 开启这一条件。System Haptics 不等于禁止一切振动，不能由本次结论推断所有 App、来电、通知均会被关闭。用户要求保留现有行为，仅补充游戏内说明。
- Android 多款游戏无震感是用户报告；源码确认现有 View.performHapticFeedback(VIRTUAL_KEY) 受系统设置影响，且不需要 VIBRATE 权限。本轮没有连接 Android 设备，未定位其设备端原因，也没有改变 Android 反馈实现。
- 跳一跳所有落地结果（含原平台小跳）在动画结束后请求触感；蓄力和起跳没有触感。扫雷插旗／拔旗每次都请求反馈，适合重复对照。现有玩法均为短脉冲，没有持续震动效果。
- 单个临时诊断包已完成输入／游戏事件／开关／Unity 音频／iOS 会话／AudioUnit 以及触感原生调用的完整可见链路采集。恢复前用户确认无声，但有效 AudioUnit 的创建、设置、初始化和启动均成功，持续收到 render 回调，Unity 混音非零，系统输出缓冲区抽样字节也随播放出现非零变化。恢复时同一 AudioUnit 仅执行成功的 Stop/Start，没有重建或重新初始化。这排除了“初始 AudioUnit 未启动、没有渲染回调”的推测；无法据此确定最终扬声器门控或音频会话内部原因。
- 输入格式请求为 24 kHz / int16 / 交错双声道，实际输出格式快照为 48 kHz / float32 / 非交错双声道；两处 API 均成功，不能把采样率不同本身认定为故障。原始字节抽样不等于 RMS 或可闻输出。证据：`artifacts/logs/ios-chain-console.log`、`artifacts/ios/chain-haptic-evidence.json`、`artifacts/ios/audio-investigation.json`。
- 诊断源码只在 `artifacts/ios-native-games` 与隔离副本内，正式 Assets 不含诊断面板、AudioUnit 包装器、混音探针或对照震动按钮。
- 恢复正常包时，手机一度提示联网验证开发者失败；两包签名证书及描述文件完全相同、未过期。用户联网完成 Verify App 后，正常包重新启动成功，日志再次确认一次输出恢复完成。这次是设备信任验证，区别于重新签名续期。
- 新增共用设置中的“声音与触感说明”：解释媒体音量、事件触发、设备能力，以及 iPhone 的 System Haptics / Vibration 两个开关。长文可滚动，返回后仍在原设置流程；2048、跳一跳、Flappy Bird、扫雷帮助补充各自触感时机与入口。
- 用户要求暂停静置卡住／灰屏问题调查；没有将该项列为已修复。

## 帮助说明正式包（2026-11-08）

- 共用说明页、滚动末尾、返回设置及 2048 帮助已通过生产 UI 的离屏渲染检查，覆盖 720×1320 与 720×1582；图片在 `artifacts/ios/feedback-help-preview`。Flappy Bird 原有 UI 检查通过 180 项断言，包含新帮助布局，日志为 `artifacts/logs/ios-help-flappy-ui.log`。渲染辅助脚本仅存在于 artifacts；其日志出现一次 Unity Editor SearchDatabase 启动索引异常，渲染完成并正常退出，未发生在游戏运行代码中。
- 随后重新运行正式 `scripts/build-ios.sh`，新的隔离副本不包含诊断或渲染辅助脚本，导出退出码 0，1347 项资源与排除清单检查通过。Xcode Release 真机编译退出码 0，日志为 `artifacts/logs/build-ios-help-native.log`。
- 新包的 ARM64、应用标识、设备覆盖、描述文件期限与严格签名校验通过；实际签名叶证书包含在描述文件中。二进制中未发现诊断探针标记。报告为 `artifacts/ios/signed-help-app-verification.json`、`help-signature-detail.json`。本次复用了原签名授权，期限仍为北京时间 2026-11-15 00:12:20。
- 已覆盖安装到 iPhone 17 Pro，没有卸载应用；安装前后 Documents 中 29 份存档与备份文件逐字节一致。保留声音关闭、触感开启的用户选择。证据为 `artifacts/ios/device-install-help.json`、`help-save-verification.json`。
- IPA 为 `artifacts/builds/XanaduYonder-0.9.5-ios-native-help.ipa`，281793286 字节，ZIP 完整性检查通过，SHA-256 为 `8c9c8667acd835feb9013b1cc21fefacf7d1259a009f5c7b02e289baa6d286d7`。旧的初装包与反馈候选包均保留。
- 安装后的首次启动被手机锁屏拒绝；用户解锁后，同一包于 14:42 成功启动。日志确认存档加载、大厅启动流程完成，以及一次音频输出恢复 `session=1, stopped=True, started=True`，后续进程检查确认 PID 1098 仍在运行。证据为 `artifacts/logs/ios-help-console.log`、`artifacts/ios/device-help-running-process.json`。这是启动与调用链验证，此前声音与系统触感的实机验收结论不冒充本包重新听感／震感验收。

## 尚待验证

音频底层根因仍未证明；Android 设备端无触感原因未验证。19 款游戏逐项触控、横竖屏切换和存档回归尚未完成。iPad 只配置为受支持设备，本次未连接 iPad；iOS 15 触感 fallback 仅做编译检查，没有对应真机验证。

## 免费签名与续签

Apple Personal Team 的描述文件有效期为 7 天，每台设备最多安装 3 个此类 App。免费续签工具重新申请签名并更新应用，不能把签名变成永久有效。AltStore / SideStore 自身也占用一个名额。

续签需要由签名工具联网向 Apple 获取新的授权，再重新签名安装。复用未过期的旧描述文件进行本地构建不等于续期，应检查新包内实际的 ExpirationDate。手机的 Verify App 是另一条开发者信任验证流程，不会延长描述文件期限；本次已由用户联网完成并成功恢复启动。游戏内容本身可离线运行，但不能保证系统永远不再要求联网验证。

- [Apple Personal Team 限制](https://developer.apple.com/help/account/basics/about-your-developer-account/)
- [AltStore Classic Remote AltServer](https://faq.altstore.io/altstore-classic/remote-altservers)：完成设置后，Wi-Fi 与 LocalDevVPN 开启时可不依赖自己的电脑在线。后台刷新不能视为每次必然成功。
- [SideStore 当前准备条件](https://docs.sidestore.io/docs/installation/prerequisites)：首次借助电脑安装，日常刷新使用 Wi-Fi、LocalDevVPN 和有效配对数据。

本轮未登录任何第三方签名服务，未配置自动续签，也未上传设备配对秘密。
