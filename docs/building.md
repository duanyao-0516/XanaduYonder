# 统一构建与平台范围

Android 与 iPhone／iPad 共用 `master` 和同一份 Unity 源码。唯一推荐的日常构建入口是 `scripts/build.sh`；必须显式选择目标平台，不根据当前电脑或 USB 手机猜测。

## 命令与产物

从仓库根目录执行：

| 目标 | 命令 | 结果 |
| --- | --- | --- |
| Android Debug | `./scripts/build.sh android` | 独立 `.debug` 包，`artifacts/builds/XanaduYonder-<version>-debug.apk` |
| Android Release | `./scripts/build.sh android --configuration release` | 正式包名及既有签名，`artifacts/builds/XanaduYonder-<version>-android-arm64.apk` |
| 构建后安装 Android | 在以上 Android 命令后加 `--install [DEVICE_SERIAL]` | 校验 APK 和已授权 ARM64 真机，再覆盖安装同包名、同签名应用 |
| iPhone／iPad | `./scripts/build.sh ios` | 过滤副本并导出 `artifacts/ios/xcode/Unity-iPhone.xcodeproj` |
| 参数帮助 | `./scripts/build.sh --help` | 无需 Unity、签名或设备即可查看用法 |

Android 的 `--configuration` 默认为 `debug`。Debug 指独立包名和本地签名，仍关闭 Unity Development、Android debuggable 和 PlayerConnection；它不提供 `run-as` 存档访问。Release 从既有本机签名配置读取凭据，不把密码传入 Unity 或写进 Git。安装失败不会自动卸载或清数据；多设备时显式指定序列号。

iOS 入口完成的是 Unity 导出，后续仍需 Xcode 编译、签名和安装；不接受 Android 的 `--configuration` 或 `--install`。具体命令及设备要求见 [iOS 构建记录](verification-ios-native.md)。构建新版与[自动续签](ios-auto-renew.md)分别管理：续签使用固定的已验收快照，不会自动接收仓库的新代码。发布新版需先验收，再更新续签快照与安装记录。

旧 `build-android.sh`、`build-release.sh`、`build-ios.sh` 保留为兼容入口，转交同一个总脚本；各游戏专用 validation/preview 脚本继续提供独立包名的验证流程。

## 平台内容

| 内容 | Android | iOS |
| --- | --- | --- |
| 大厅、2048、数独、华容道及其他 Unity 原生玩法 | 共有 19 个共用入口 | 19 个入口 |
| 切水果、只有一道门、经典黄金矿工、赵云与阿斗、割绳子、王国保卫战 | 保留，共计 25 个入口 | 暂不打包 |
| Unity 原生黄金矿工 | 保留 | 保留 |

入口由目标平台的条件编译控制。iOS 后端先调用 `prepare-ios-project.py`，仅在 `artifacts/ios/project` 副本中排除 Android 插件、相关 Resources／StreamingAssets 和专属图标；原仓资源不删除。Unity 导出再检查实际打包清单、Android 二进制残留和原生黄金矿工资源。

排除项是明确维护的清单，不会自动推断新游戏是否兼容 iOS。新增平台专属游戏时，应同时检查大厅入口、资源过滤、原生插件导入设置和目标平台构建结果。

## 环境与代码组织

- Unity 版本统一从 `ProjectSettings/ProjectVersion.txt` 读取；非默认安装位置可设置 `UNITY_EDITOR`。
- Android 需要该 Unity 版本的 Android SDK、NDK、OpenJDK；iOS 需要 iOS Build Support 和兼容 Xcode。
- `scripts/build/` 存放平台后端和公共工具定位，安装与签名继续复用现有边界。正常使用只调用总入口。
- iOS 专属托管桥位于 `Assets/Scripts/Runtime/Platform/iOS/`，原生桥位于 `Assets/Plugins/iOS/`；共用玩法和存档保持原有归属。
- 同一工作树的 Unity 检查和构建串行执行；不要让交互编辑器与批处理同时打开同一项目。

日常开发、单项测试见[开发说明](development.md)，目录及存档契约见[架构说明](architecture.md)。编译成功、编辑器渲染、真机操作和人工听感／震感分别记录，不能相互替代。

本次入口整理的构建结果、Android 实测范围及未验证项见[双平台整合验证](verification-platform-integration.md)。
