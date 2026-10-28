# Zuma 导航、中文说明与瞄准反馈验证

日期：2026-10-28。基于隔离分支 `p/duanyao.0516/feature/zuma-original-ui` 的 `1644d5f` 继续开发，未合并、切换或修改其他工作树。原实现与 10 月 27 日包体记录见 [历史验证](verification-zuma.md)。

## 本轮变化

- 选关左上角新增 Xanadu 配色与中文字体的“返回大厅 / 玩法说明”。原版选关、地图、难度、游戏英文 UI 保留，名称为 Zuma。
- 说明分基本玩法、英文对照、难度说明三个可滚动页签：8 段玩法、38 条英文—中文—功能、四档实际难度参数。阈值、颜色数、初始球数和默认 ACE 从 Core 配置派生。回退是自动链条规则，ACE 是参考时间，金币每枚 500 分且中断 CHAIN，不虚构道具或游戏模式。
- 点状瞄准线单点由 5 增至 8 设计像素，增加 11 像素暗色外沿；18 点改为 22 点、间距 12 改为 14，增强末端可见度。射击、冷却、消除、碰撞和存档协议未改。
- 统一取消瞄准时清理视觉提示，避免暂停、后台或结果页保留旧点。说明弹层阻断点击；选关入口另有状态门禁，防止另一根手指在弹层出现前按下的旧按钮在松手时触发。
- 帮助视图由 Zuma 自己的横屏 Canvas 和页面持有，关闭不重建选关，不写存档。返回大厅走原 Exit 保存、释放资源和恢复竖屏链路。

## Development Console 原因与修复

上一版 `BuildZumaPreview` 显式用了 `BuildOptions.Development`。该模式使用 Unity 内置开发控制台运行库，旧 APK 带 `application-debuggable` 与开发网络权限；工程中没有移植一个自定义 Development Console。曾出现的早期 `frogland2.ogg` 解码问题已在前轮修复，物理手机本轮开始时仍安装 2026-10-27 18:01:09 的旧预览；不能把当前未复现的控制台显示认作又发生同一异常。

预览入口现默认关闭 Development / Script Debugging / Profiler，并在 finally 恢复包名、名称、签名与临时构建标志。原 `BuildAndroid` 诊断入口保留。不调用隐藏控制台或禁用日志的 API。

`scripts/verify-zuma-preview.py` 检验实际 APK 的独立包名、ARM64 单 ABI、非 debuggable、无权限、`libunity.so` 不含 `Development Console`、APK 签名有效；旧开发包已被此门禁拒绝，新构建通过。非 Development 与正式发布签名是两件事：这个包仍使用本地调试签名，仅作并行预览。

## 自动检查与构建

| 检查 | 本轮结果 |
| --- | --- |
| 既有 2048 / Jump / Sheep 八组规则、输入、持久化和布局脚本 | 172 项检查通过 |
| Zuma 规则 | 29 个场景通过，含 18 图 × 4 难度与完整快照回放 |
| Zuma 输入 | 18 项检查通过 |
| Zuma 存档 | 32 个场景通过，含未来版本、备份、I/O 错误和跨地图资源释放后重试 |
| Zuma 资源 | 8 个检查通过；构建前资源校验通过 |
| 帮助内容 | 102 个断言通过，术语唯一、翻译/作用齐全、四档难度与 Core 一致 |
| Unity 6000.3.27f1 Android ARM64 / IL2CPP | 构建成功；最终 APK 验证成功 |
| 独立代码审查 | 修复暂停瞄准残留、Scrollbar handle 越界、多指旧菜单回调三项后复核通过 |

日志位于本地 `artifacts/verification/zuma-help/tests/` 与 `artifacts/logs/build-zuma-preview.log`。最终交付前代码/文件差异检查无错误；Packages、ProjectSettings、原始游戏资源无新增修改。

最终 APK：`artifacts/builds/XanaduYonder-0.3.0-zuma-preview.apk`。

- 大小 **56,121,424 字节（56.12 MB，53.52 MiB）**，271 个 ZIP 条目，条目间空洞为 0。
- SHA-256：`a90a3d81081aeb8346319eb9e32f197ad4947a0240a9d04be64b87138df291fc`。
- 包名 `com.duanyao.xanaduyonder.zumapreview`，v0.3.0 / code 3；未公开发布。
- 物理 OPPO PKM110 已通过 `adb install -r` 更新独立预览，`lastUpdateTime=2026-10-28 12:25:53`，pkgFlags 无 DEBUGGABLE。更新前后 11,596 字节 Zuma 存档完全一致，SHA-256 `00de94af6d3ccaa03b4dedba5ef65c89b3035ffcd4f84e64282c92d555f5040e`。
- 手机主 APP 仍为 v0.4.0，更新时间保持 2026-10-27 15:24:31；未覆盖、降级、清数据。手机前台当时是其他测试应用，本轮只后台安装预览，没有抢占其交互。

## Android 交互回归

使用本轮专用 ARM64 Android 36 模拟器 `XanaduZumaHelpValidationApi36`，1080×2400 / 横屏 2400×1080。未使用或关闭其他任务的模拟器。

已实际操作并读回：

1. 从大厅进入横屏选关，左上角两按钮完整显示，不盖住难度选择。
2. 三个说明分页均可切换、滚动到末尾；文字换行和内容裁剪正常，滚动条不越过底部。
3. 点击遮罩覆盖下的 PLAY/NEXT 未触发选关动作；帮助切后台回来保留当前分页与滚动位置。
4. 系统返回键仅关闭帮助；关闭按钮同样回到原选关。新建资料和已有运行对局分别验证：帮助前后存档字节完全一致。已有对局证据 SHA-256 `458be047286fccb626a8e836735149b89eddb349ad39f4e3b29b9fd6c5ec87e6`。
5. 点击左上角返回大厅恢复竖屏；重新进入可继续已有对局。
6. DOWN / MOVE 时出现放大点线，UP 后单发；立即暂停读回仅一颗在飞弹珠、recoilTicks=7。按住切后台后回来为暂停页，瞄准点不残留。
7. 此进程实际交互期间 `Unity:E` / `AndroidRuntime:E` 日志为空。

截图在本地 `artifacts/verification/zuma-help/`：`selection.png`、`help-rules-top.png` / `help-rules-bottom.png`、`help-terms-top.png` / `help-terms-bottom.png`、`help-difficulty-top.png` / `help-difficulty-bottom.png`、`help-after-background.png`、`return-lobby.png`、`aim-enlarged.png`、`pause-after-held-background.png`。存档读回和安装证据在同目录，均未纳入 Git。

多指原按钮残留回调的修复经过独立源码审查，未声称用真实双指复现完成。物理手机本轮验证了安装兼容和存档保留，UI 交互验证使用上述专用模拟器；没有覆盖全部手机、长时帧率、峰值内存、音频听感或 72 种组合人工通关。

## 架构审查交付

[小游戏架构审查](architecture-audit-2026-10-28.md) 分别画出当前 Zuma 分支与 `master@ca50db0`，核查七款跨基线的规则、宿主、资源、存档和生命周期。主线六款尚不含 Zuma，不能将七款审查清单理解为本 APK 已经集成七款。

结论为部分隔离，非整体重写：优先处理旧 Store 未来版本保护、保存去重、模块初始化恢复；再统一宿主生命周期与拆分 2048 职责，依据测量安排资源缓存。架构风险本轮为静态审查与整改建议，没有夹带全 APP 重构。
