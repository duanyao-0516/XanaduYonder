# 游戏精简与 iOS 网页游戏适配：实现与安装记录

分支：`p/duanyao.0516/feature/gameplay-polish`。范围和实现计划见[迁移计划](plans/2026-11-08-ios-web-games-and-content-removal.md)。本记录按实际执行更新，不替代既有 v0.9.6 Release 记录。

## 已实施的输入边界

- 删除经典黄金矿工独立入口、战争进化史、王国保卫战、切水果大师的运行代码、资源和专用构建链。Android 公共 launcher 解除王国保卫战依赖。
- Android / iOS 当前目录均为 22 项；Zuma 和飞机大战继续使用 Unity 实现。
- 经典黄金矿工迁移后保留 611 个单人对象、594 个双人对象，共 1,205 个；旧原生黄金矿工 v1–v4 存档不重解释为源战役。源战役 27,129 条断言、旧核心 53 项、旧目录 22 项、存档 32 项通过；原 JS 的 40 个目标和 200 组价格区间另行执行对照。
- iOS staging 单独打包三个 HTML 游戏：赵云与阿斗 305 文件，割绳子 194 文件（425 关），只有一道门 26 文件，另各有 manifest。每份资源以完整路径清单、大小、SHA-256 核对。

## 当前检查证据

- `verify-removed-games.py --source-root .` 已通过；检查实际目录及忽略的 AAR，不只 Git 跟踪文件。
- 删除检测的污染用例覆盖专属路径、9 个 SO、运行时符号、UTF-16、跨读块、嵌套 JAR 和 Unity packed inventory；允许共用 libc++ 与迁移后的原生黄金矿工数据。
- iOS staging 的路径、额外文件与篡改内容拒绝检查通过。iOS 原生宿主通过 iPhoneOS SDK / ARM64 / iOS 15 的无警告语法编译。
- iPhone 17 Pro 的临时 DeviceChecks 已按用户要求卸载；后续安装不启动游戏，实际玩法由用户验证。

- Unity Play Mode 黄金矿工 UI：16,521 条断言通过，覆盖旧挑战流程及 Source 双模式新局、全部 27 张源精灵、等比世界坐标、源画布裁剪、按钮安全区和原倒计时时序。截图位于 `artifacts/gold-miner-development/ui/source-{solo,coop}-game-*.png`。
- 使用 iPhone 实际备份的 rulesVersion 3 黄金矿工存档验证新生产 codec/session：历史 profile、布局、关卡和资金不变，升级后序列化往返通过。29 份 Documents 文件已备份及计算哈希。
- 交叉审查发现并修复：孤立延迟奖励导致恢复崩溃、Source 商店购买与实物账不一致、单人倒计时晚一秒、iOS 保存失败引发无限自动重写。错误回路复现从 26 次写入且仍有错误排队变为 1 次写入后队列清空；重试仍由用户触发。

## 打包、安装与用户反馈

- Android APK 构建、清理检查、OPPO 覆盖安装成功，包体 220,995,169 字节。实际检查了大厅 22 项和网页游戏 Home 后通过 launcher 回到原 Activity；之后按用户要求停止操作设备。
- iOS Unity 导出清理检查通过（3,492 个产物文件、1,121 条 packed asset 记录）；最终 Xcode 编译、严格签名、三个网页资源的完整 manifest/大小/哈希核对通过。最新修复包已于 2026-11-08 覆盖安装到 iPhone，未卸载应用或清除数据。
- 修复实际崩溃日志中的方向冲突：Unity LandscapeLeft 对应 UIKit LandscapeRight；等待方向时优先读取 Unity app delegate 的动态 mask，并在导出中声明竖屏及双向横屏。
- 三款网页容器均全屏，返回入口仅首屏显示。只有一道门复用游戏原画风按钮，并把真实安全区换算为 CSS 像素保护控件。浏览器实际按钮验证通过 exit-intent → exit-ready，进入对局后隐藏。
- 方向恢复改为保存 Unity 请求策略而非尚未提交的屏幕实际方向；割绳子显式以竖屏进入，继续允许自动旋转。
- WebKit 媒体暂停 API 按次数累积；macOS 实际 WKWebView 对照确认重复暂停、一次恢复仍 Suspended。已按状态变化去重调用并保持跨 reload 状态；网页 audioSession 使用 playback，与 Unity 策略一致。用户确认只有一道门在此前方向修复后已可打开；最新全屏、切换方向及声音修复的 iPhone 听感/操作尚待用户验收，不记作已通过。
- 最新证据在 `artifacts/ios/web-hotfix/`，媒体对照在 `artifacts/ios/web-audio-probe/`。这些本地产物不进入 Git，不替换已有 GitHub Release。
- 本次未更新 Mac 自动续签的固定验收快照；当前新版本与旧台账不一致时执行器会拒绝覆盖。新版验收后需同步该快照和实际安装记录。

