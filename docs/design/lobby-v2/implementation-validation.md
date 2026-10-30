# 大厅 v2 原生实现与验证

## 实现边界

批准稿为同目录 `lobby-ui-refined.html`，原文件与图标哈希保持不变。产品保留现有 17 个游戏，三种视图按固定分类与组内顺序排列；单列不增加分类标题，分类直接写在每张卡片的编号与玩法标签之间。移除自定义排序，展示方式按钮打开实际选择浮层。

`AppController` 保留游戏入口与真实纪录读取、更新和错误提示的所有权；`LobbyView` 管理布局、选择浮层与可见纪录，不创建游戏会话。视图切换重建卡片，但保留 `ScrollRect` 和状态文字对象，游戏存档协议不变。展示偏好用独立的 `xanadu.lobby.display.v1`，缺失或非法值回退双列。

视觉以 390 设计单位映射现有画布。UGUI 文字使用两倍字号和文本框、反向局部缩放，避免设计层缩放导致小字模糊；显示字号和间距仍与设计稿一致。数独单列纪录必要时从 11 缩至 9，保留四档完整信息。Flappy Bird 原图透明留白不对称，按可见像素的旋转包围盒进行光学居中；原图及游戏内使用不变。

小图标使用批准的 PNG Sprite，不使用旧程序图标代替。两个在研游戏只准备 `kingdom-rush` 与 `age-of-war` 资源，未创建假入口。详情见 [图标接入说明](README.md)。

## 本轮执行结果

先运行新大厅验收，旧实现因缺少三视图展示入口而按预期失败；实现后执行以下 Unity Play Mode 检查，最终均通过：

| 检查 | 最终结果 | 主要范围 |
| --- | --- | --- |
| LobbyDesignChecks | 2939 assertions | 17 个原回调、三模式完整目录、输入、浮层隔离/关闭、偏好、纪录更新、长分数错误提示、两种尺寸渲染 |
| MiniGameUiChecks | 1211 assertions | 大厅像素/图形、真实轻点/抖动/滑动取消、数独/跳一跳/旸了个旸已有行为 |
| StartupHandoffChecks | 223 assertions | 启动就绪、输入门控、同步解除遮挡、大厅实际滑动/点击 |
| TetrisUiChecks | 152 assertions | 俄罗斯方块入口、UI 与存档不可读语义 |
| FlappyBirdUiChecks | 180 assertions | Flappy Bird 入口与生产 UGUI |
| GoldMinerUiChecks | 16303 assertions | 黄金矿工入口及现有玩法/UI 检查 |
| DinoPageValidation | 24 cases / 543 assertions | Dino 入口、双方向纪录与现有页面生命周期检查 |

截图在 `artifacts/lobby-design/`：720×1320、720×1582 下的三视图顶部/底部和展示浮层。已目视核对文字清晰度、图标同行对齐、分类位置、单列大 Logo 与数独一行纪录。MiniGame 像素检查中，Flappy Bird 可见中心偏移已从约 `(1.27, -8.19)` 修正到 `(0.27, -0.19)` 像素。

另完成独立源码审查与资产哈希/像素/导入元数据核验。上述自动检查是 Unity 渲染和输入模块证据，不替代 Android 物理多点触摸、系统旋转或完整玩法真机回归。此前用户确认的启动滑动和大西瓜边界投放行为，本轮不改其规则。

## 测试包交付

2026-10-30 构建并通过 `adb install --no-streaming -r` 安装到已连接的 OPPO PKM110；包名为 `com.duanyao.xanaduyonder.startuptest`，应用名为“Xanadu 启动测试”，版本 0.7.0 / code 7，未清除原测试应用数据。启动后已读取真机截图确认新大厅显示；`artifacts/lobby-design/device-double.png` 为本轮设备截图。三模式全面交互与布局检查仍以上述 Unity 验收为证据，未宣称已完成所有游戏的真机玩法回归。

- APK：`artifacts/builds/XanaduYonder-0.7.0-startup-validation.apk`
- 大小：114984823 bytes
- SHA-256：`c53488ccaeca39782984539307fd016e015854955b88f07b3447f9098f3f309d`
- 包信息：min SDK 26 / target SDK 36；APK v2 签名验证通过。
- 构建退出码 0；构建后 `ProjectSettings.asset` 无差异。

## master 交付前整合验证

用户随后授权汇总提交到远端 master。先同步 `2a7566a`（成语接龙完整词库与输入体验修复）；上游 255 个变更文件与本任务文件无交集，同步前后本任务所有文件哈希一致。

在整合后的源码上重新执行：LobbyDesignChecks 2939、MiniGameUiChecks 1211、StartupHandoffChecks 223、WatermelonPointerChecks 20、IdiomUiChecks 8413、IdiomPresentation 198 项通过；成语资源检查也通过，45776 条释义引用及打包哈希一致。交付范围、Assets 元数据/GUID、图标归档哈希与脚本语法另经独立核查，无阻塞项。

本节记录源码整合验证；上节 APK 是同步 master 前已安装的测试版本，未因本次提交操作重新安装。日志、截图、APK 与其他 `artifacts/` 文件不进入源码提交。
