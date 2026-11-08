# 统一构建入口与双平台整理验证

日期：2026-11-08。源码基线为 iOS 功能分支 `4afceca`，本记录随统一入口整理提交一并合入共享 `master`。版本保持 `0.9.5 / build14`，没有创建新发布版本。

## 变更边界

- `scripts/build.sh android|ios` 是日常构建入口；Android 可选择 Debug／Release 及构建后安装，iOS 仅导出 Xcode 工程。
- 三个旧入口保留兼容委托，后端放到 `scripts/build/`，共用固定 Unity 版本及工具路径定位。显式平台决定打包范围。
- 两个 iOS 专属托管桥移动到 `Assets/Scripts/Runtime/Platform/iOS/`；源码及原 `.meta` 逐字节保持一致，GUID 不变。
- README、构建、开发、架构文档和文档导航区分当前能力与历史验证。历史文档、游戏代码、存档协议和本机已部署的 iOS 续签快照未调整。

## 构建与脚本验证

环境：macOS，Unity `6000.3.27f1`，项目固定的 Android ARM64 工具链及 iOS Build Support。

| 检查 | 实际结果 |
| --- | --- |
| 总入口、后端及兼容脚本语法 | Bash 检查通过 |
| 临时 CLI／后端夹具 | 参数路由、帮助不访问凭据、无效参数、兼容固定模式、失败退出码、旧产物保留、签名隔离、带空格路径、iOS staging 清理及 Unity.app 符号链接场景通过 |
| `./scripts/build.sh android --configuration release` | 退出 0；正式 APK 签名有效；ARM64、非 debuggable、无 INTERNET 权限、无 Development Console |
| `./scripts/build.sh android --install 3B6F4NE8A66ZXTRL` | 退出 0；独立 `.debug` APK 编译、校验、安装及启动成功 |
| 现有 `test-minigame-ui.sh` | `MINIGAME_UI_CHECKS_PASS: 1467 assertions`，覆盖现有生产 UGUI 检查；不等同于全部真机交互验证 |
| `./scripts/build.sh ios` | 退出 0；`IOS_EXPORT_PASS`，实际打包资源 1,347 项，排除资源及 Android 二进制检查通过，Unity 原生黄金矿工资源保留 |

iOS 导出产物为 `artifacts/ios/xcode/Unity-iPhone.xcodeproj`。此次没有重新执行 Xcode 原生编译、签名或 iPhone／iPad 安装；19 个 iOS 入口的范围来自现有条件编译与过滤清单，没有据此声称本轮完成 19 款真机回归。

APK SHA-256：

- Release：`561e83195c09a23a99d7dfc9857d63b1454abf7f4ccb07b94ef7e419f4a41ceb`
- Debug：`1f6ec97335c25ff9a3d39b143996c4293cf2ce3f534117ac4de2e16528dcf122`

独立审查发现并修复了 `UNITY_EDITOR` 经 Unity.app 符号链接指定时的模块定位兼容问题，新增临时夹具先复现再验证修复；复审无遗留阻断问题。临时验证脚本和产物仅保存在忽略目录 `artifacts/`，没有新增正式测试源文件。

## Android 真机已完成范围

设备为 OPPO PKM110，Android 16，1080 × 2373，USB 授权连接。

手机已有主包 `com.duanyao.xanaduyonder` 使用 Android 调试证书，与本机正式证书不同。因此没有卸载、清数据或尝试用 Release 覆盖旧包，改为安装 `com.duanyao.xanaduyonder.debug` 做独立验证。旧包已备份；其外部 Unity 目录中的 18 份 JSON 存档及备份在安装后逐字节一致。此结论不包含无法访问的应用内部 WebView 数据，也不是正式证书覆盖升级验证。

| 场景 | 观察结果 |
| --- | --- |
| 启动与大厅 | 独立包启动成功，显示 25 款；双列卡片、单列大卡及分类目录均有实际渲染，目录可滚动至底部 |
| 声音与触感说明 | Android 专属说明正常显示；系统返回能回到设置 |
| 设置持久化 | 声音／触感关闭后强制结束并重启，仍显示关闭；随后恢复两项开启 |
| 王国保卫战 | 横屏开始页渲染成功，双返回回到竖屏大厅 |
| 割绳子 | 开始页渲染成功，双返回回到大厅 |
| 赵云与阿斗 | 匹配画面及主页面渲染成功，返回大厅 |
| 经典黄金矿工 | 单人／双人选择页渲染成功，收尾返回大厅；未进入局内 |

按用户要求，到此停止追加排查和游戏回归。切水果、只有一道门及其余原生玩法的完整真机交互、本轮实际听感／震感、长时间运行和全部存档恢复均未验证。原有 iOS 卡住问题仍保持暂停排查状态。

## 本机证据

证据保存在忽略目录，未上传设备数据或安装包到 Git：

- `artifacts/platform-integration/`：三次构建命令日志、APK／证书校验、原包与存档备份、存档哈希比较、设备截图和应用进程日志。
- `artifacts/validation/minigame-ui/report.txt`：本轮 1,467 项断言报告。
- `artifacts/ios/export-summary.txt`、`packed-assets.txt`、`staging-manifest.json`：本轮 iOS 导出与过滤证据。
- `artifacts/build-cli-verification/`：临时脚本夹具。

构建后仅补充了旧命令提示、注释和本文档；没有再改变玩家运行时代码。Unity 自动写入的空 iPhone 图标元数据已移除，避免将编辑器副作用混入提交。
