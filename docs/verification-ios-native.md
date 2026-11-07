# iOS 原生小游戏构建记录

## 范围

分支 `p/duanyao.0516/feature/ios-native-games`。应用标识 `com.duanyao.xanaduyonder.iosnative`，版本沿用 `ProjectBuild.Version` / `VersionCode`。iOS 大厅保留 19 个入口，战争进化史入口含两代；纯 Unity 黄金矿工保留。切水果、只有一道门、经典黄金矿工、赵云与阿斗、割绳子、王国保卫战暂不包含。

Android 的 25 个入口保持原顺序。原工作树不用于切换 iOS 构建平台；所有 Unity 导出操作在 `artifacts/ios/project` 副本进行。六款游戏专属 Resources / StreamingAssets 和 Android 插件在复制时排除，而非仅隐藏入口。

## 使用

1. Unity Hub 给项目固定版本安装 iOS Build Support；本次为 6000.3.27f1。
2. 安装兼容 macOS 的 Xcode，首次运行由使用者确认 Apple 许可并完成组件初始化。
3. 在仓库根运行 `./scripts/build-ios.sh`，输出 `artifacts/ios/xcode/Unity-iPhone.xcodeproj`。可选 `XANADU_IOS_TEAM_ID` 只传 Team ID，不传账号密码。
4. 在 Xcode 登录自己的 Apple Account，选择 Personal Team，连接已信任的 iPhone，启用手机 Developer Mode，再构建安装。导出 Xcode 工程不等于已生成可安装的签名包。

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

## 尚待验证

Xcode 原生编译、个人签名、安装、启动和手机触控/存档回归尚不能由上述导出结果证明。iPad 只配置为受支持设备，本次未连接 iPad。现有触感实现只覆盖 Android，本轮没有新增 iOS 触感。

本轮最后观察到 Mac 锁屏，Xcode 首次启动停在 `Xcode and Apple SDKs Agreement`，尚未取得用户确认。需要用户解锁 Mac、完成 Xcode 许可/组件初始化并登录个人签名账号；手机则需要开启已显示的 Developer Mode，并在重启后确认。完成这些操作后，继续原生编译与安装，无需重新实现项目适配。

## 免费签名与续签

Apple Personal Team 的描述文件有效期为 7 天，每台设备最多安装 3 个此类 App。免费续签工具重新申请签名并更新应用，不能把签名变成永久有效。AltStore / SideStore 自身也占用一个名额。

- [Apple Personal Team 限制](https://developer.apple.com/help/account/basics/about-your-developer-account/)
- [AltStore Classic Remote AltServer](https://faq.altstore.io/altstore-classic/remote-altservers)：完成设置后，Wi-Fi 与 LocalDevVPN 开启时可不依赖自己的电脑在线。后台刷新不能视为每次必然成功。
- [SideStore 当前准备条件](https://docs.sidestore.io/docs/installation/prerequisites)：首次借助电脑安装，日常刷新使用 Wi-Fi、LocalDevVPN 和有效配对数据。

本轮未登录任何第三方签名服务，未配置自动续签，也未上传设备配对秘密。
