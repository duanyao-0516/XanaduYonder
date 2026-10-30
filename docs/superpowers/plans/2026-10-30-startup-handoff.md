# 启动图直接交接大厅验证包

**目标：** 按用户本轮追加要求，先产出只改变启动交接的 APK：取消渐隐，在大厅具备接收新触摸的条件后一次性撤掉启动层。保留现有 Logo 尺寸及保持时间。

**范围：** 基于远端 master `8b185db` 的独立工作树。大厅图案、数独文案和大西瓜输入暂不进入本次对照包。

**实现边界：** `AppController` 拥有初始化完成及大厅输入就绪状态；`BrandSplash` 只等待该状态并控制启动层。就绪检查不触发保存、导航或重复初始化。所有游戏及存档协议保持现有行为。

- [x] 新增真实 UGUI 交接检查：旧实现第 22 项断言因半透明拦截失败；使用内存大厅夹具，不读取/写入玩家存档。
- [x] `AppController.Awake` 完整返回前标记 `startupComplete`，只读 `LobbyReadyForInput` 检查前台、焦点、无弹窗、活动 Canvas/大厅/ScrollRect、有效布局及活动 EventSystem 输入模块。
- [x] `BrandSplash.Start` 保持不透明等待；就绪后再等已渲染帧并复查条件，先取消射线拦截、同步禁用启动层，再销毁。删除 0.25 秒透明度动画；不以超时揭开尚未就绪的大厅。
- [x] 检查未就绪时大厅不提前露出、帧末失焦不交接、就绪后不存在透明拦截层、首个新拖动能移动大厅、按钮点击可达。独立审查发现的启动期间 Back 隐藏退出框问题已先复现失败再修复；交接前不创建弹窗，交接后恢复正常返回操作。
- [x] `scripts/test-startup-handoff.sh` 最新 207 条断言通过，包含独立根启动层的组件禁用及宿主停用清理。此前 `scripts/test-minigame-ui.sh` 285 条断言通过；前置 Core 43 项与输入 10 项通过。
- [x] 串行构建 Android ARM64 APK，验证包名、版本、签名和 SHA-256。采用独立 `com.duanyao.xanaduyonder.startuptest` / “Xanadu 启动测试”，关闭 Development，使用本机调试签名；与正式应用并存，不共享正式玩家存档，不发布 Release。

## 首轮对照包（历史记录）

- APK：`artifacts/builds/XanaduYonder-0.7.0-startup-validation.apk`，114,761,597 字节，versionName 0.7.0 / versionCode 7 / ARM64 / minSdk 26。
- SHA-256：`594b9ecedcefcd2969978e09a5b7b8677c19a9b9e5fe281e5e75311b2af8877d`。
- `apksigner verify --verbose --print-certs` 成功，v2 签名有效；证书为本机 Android Debug。`aapt dump badging` 已核对独立包名和名称，APK 未标记 debuggable。
- 构建日志：`artifacts/logs/build-startup-validation.log`；交接检查及两次 RED 证据：`artifacts/validation/startup-handoff/`；大厅检查：`artifacts/validation/minigame-ui/`。
- 此对照包安装到 OPPO PKM110 后，用户确认仍有约 2 秒无响应；下列真机排查已进一步定位并修复。

## 真机排查及已验证修正

原启动层在 0.25 秒渐隐期间继续拦截触摸，但去掉渐隐未消除用户报告的问题。连续冷启动截屏和短时输入探针确认：大厅已经显示时，启动白底仍接收每次新手势，滚动位置始终为 0；这一阶段帧号持续约 60 fps 增长，没有 2 秒主线程停顿。

诊断记录 `artifacts/validation/startup-device/probe-01/` 中，启动 Canvas 的 `sortingOrder=32760`，实际 `renderOrder=0`；大厅 `sortingOrder=0`，实际 `renderOrder=1`。白底尺寸、颜色、不透明度和射线区域正常。数值改为 100、只移到场景根都未解决，不应把它们记为根因。

实际包原生引擎分析发现：新 Canvas 默认 WorldSpace；大厅在切到 Overlay 之前调用 SetParent 并触发排序，切换模式本身未重新排序。因此射线排序与绘制排序分离。`BrandSplash.Start` 在全部 Awake 完成后、首帧之前重新应用一次启动排序，纠正实际绘制顺序；不需要每帧重排。保留场景根节点以符合 Unity 的 Overlay 层级约定，并在停用/销毁时同步清理。

修正诊断包 SHA-256 `7af0fd01998abbcd4f5f8adf1450902861c60223c04f20cc59584a9ab53901bd` 的真机记录位于 `artifacts/validation/startup-device/resort-after-awake/`：首帧启动层 renderOrder=1、大厅=0，截屏显示实际 Logo；交接后首个新手势使滚动位置从 0 变成 560.46，连续滑动也生效。用户随后明确反馈“实际测试已经可以”。临时 StartupDeviceProbe 已从 Assets 删除，仅在忽略目录保存诊断源码与日志。

原有 207 条检查使用 WorldSpace 夹具，仅证明交接/输入/生命周期，不证明生产 Overlay 像素。新增 `scripts/test-startup-overlay.sh` 保留实际 Overlay 和创建顺序，但非批处理 Unity 首次启动遇到新版条款弹窗，未接受也未执行此检查，不能记作通过；当前显示顺序验收采用真机前后截图与触摸日志。测试定义已同步在首张截图前运行生产 Start 的排序步骤。

## 后续事项

大西瓜：用户已批准方案并要求玩法说明加粗原话。已在 Pointer 层实现棋盘内按下后锁定原触点，横向越界继续钳位到球心合法范围；正常抬起且纵向仍在棋盘高度内时投放。纵向越界松手、系统取消、后台、离页与弹窗仍取消；第二指保持现有忽略语义，不新增取消规则。玩法说明使用独立 `FontStyle.Bold` 标签展示“左右越界仍然可以投放，上下越界不可投放”。Pointer 检查 20 项、Core 检查 36 项通过；帮助页水果/校徽主题截图已核对。

大厅卡片整修已完成：数独/扫雷正方格；六款缺失预览补齐；俄罗斯方块使用方块组合；Flappy Bird 图案与按钮内容居中；数独统计去掉“四档难度”并压成单行。17 张卡片保持原顺序、尺寸、游戏入口和存档协议。`MiniGameUiChecks` 789 条断言通过，包含预览实际像素、射线穿透、方格/方块、Flappy 对齐和数独最大统计一行；相关卡片截图已逐项目视。原版检查在第 65 项准确发现 2048 缺图，证据为 `artifacts/validation/minigame-ui/before-lobby-polish.txt`。

独立审查未发现需修复的生产缺陷。新增 Overlay 编辑器像素检查仍未执行；Startup 的 WorldSpace 检查、大西瓜鼠标事件检查及 Android 真机验收分别记录，不相互替代。

## 最终交付与设备反馈

- 不带临时探针的最终 APK：`artifacts/builds/XanaduYonder-0.7.0-startup-validation.apk`，114,767,477 字节；SHA-256 `58c414df4757720b4b0e657e15d04cf8c67938f67ab3c608979db505f726e2da`，同名 `.sha256` 已更新。
- 包名 `com.duanyao.xanaduyonder.startuptest`、名称“Xanadu 启动测试”、版本 0.7.0/7；Android Debug v2 签名校验通过，使用覆盖安装保留测试应用存档。正式应用未覆盖。
- 2026-10-30 已安装到已连接 OPPO PKM110。最终包冷启动截屏位于 `artifacts/validation/startup-device/final-clean-startup/`：启动层撤除后首个新手势即可滚动；启动日志不再包含 `STARTUP_PROBE`。最终源代码 Startup 207、MiniGame UI 789、Pointer 20、Watermelon Core 36 项检查均通过，构建成功。
- 用户明确告知已自行测试冷启动滑动、大西瓜左右越界投放、上下越界取消及切后台不误投放，代理随即停止操作手机，不将用户测试记为代理自行完成的设备回归。代理仅进入游戏和读取画面，没有继续投放或重复这些测试。
- 改动保留在工作树分支 `p/duanyao.0516/codex/startup-lobby-polish`，未提交或推送。

## 大厅第二轮调整（用户追加）

- 移除成语接龙图案；跳一跳预览整体缩至原来的 85%，保持平台与棋子相对位置；大西瓜图案中心左移 26、下移 9 个设计单位，与标题区对齐。
- Zuma 与欢乐球球使用用户提供的第二、第一张 PNG，原文件不重绘、不裁剪。独立放入 `Resources/Lobby`，保留透明度并关闭压缩和 mipmap，以 96×96 的正方形居中展示；不会受游戏资源释放影响。
- 17 张大厅卡片全部移除独立开始、继续及选择按钮，卡片背景自身绑定同一原有游戏入口。保留游戏顺序、选择页、续局与存档错误处理；卡片高由 350 调整为 280，间距仍为 24，列表总高随之收紧。
- 使用现有 UGUI Button 与祖先 ScrollRect 的事件分发：点卡片文字、图案或空白均可进入；超过拖动阈值时由输入模块取消点击资格，松手不进入游戏。没有新增手势转发层。
- 原有启动交接与大西瓜边界投放实现保持不变；上一节的用户设备验证属于上轮包。本轮需单独记录整卡点击、滚动取消及视觉检查结果。

用户随后要求“直接打包安装我来做验证”，因此停止追加与运行 UI 检查，直接构建本轮 Android 包。生产改动已经独立只读审查；已更新的编辑器检查参与编译，但本轮没有执行，不沿用上一轮通过数量作为本轮证据。

- 本轮 APK：114,888,861 字节；SHA-256 `afb01a89de2caf1a28428674875ea4a1032ef48f059cee327c183b842fb2c409`。
- 构建成功，包名/名称/版本仍为 `com.duanyao.xanaduyonder.startuptest` / “Xanadu 启动测试” / 0.7.0（7），v2 签名校验通过；ProjectSettings 无遗留改动。
- 已通过 `adb install --no-streaming -r` 覆盖安装到 OPPO PKM110，返回 `Success`。未代替用户操作页面或执行本轮交互验收，视觉效果与整卡点击由用户接手验证。
