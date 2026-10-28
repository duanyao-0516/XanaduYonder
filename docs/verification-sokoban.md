# 推箱子实现与验证记录

日期：2026-10-28。工作树分支 `p/duanyao.0516/feature/sokoban`，开发基线 `8d64b5f`。Unity 页面、规则、内容、存档及大厅接入已实现；以下分别记录验证层次。

## 视觉基准

用户确认的原型为 `docs/prototypes/sokoban/`，参考提交 `5c89209`。保留其页面结构、颜色、图标、字体比例、单行信息栏、棋盘和集中操作区，不增加专注模式或常驻底部说明。

浏览器实测手机边框内部宽 388 CSS px，扣除原型模拟的系统栏和手势栏后可用高度 784 px。Unity 对比使用相同可用比例（720×1455）；原型的系统栏装饰不画进游戏安全区域。

| 元素 | 原型坐标（相对可用区域，CSS px） |
| --- | --- |
| 导航 | y=0，h=63 |
| 标题区域 | y=63，h=47.421875 |
| 单行信息 | x=19.5，y=110.421875，w=349，h=45 |
| 棋盘视口 | x=19.5，y=163.421875，w=349，h=452.578125 |
| 集中操作区 | x=19.5，y=632，w=349，h=152 |
| 撤回/重来 | y=656，h=48 |
| 缩放控制 | y=716，h=44 |

参考截图和测量 JSON 保存在本地 `artifacts/validation/sokoban-ui/`。真实 UGUI 渲染与上述几何断言通过，按相同可用尺寸进行并排视觉检查，修正了下拉箭头位置、禁用控件透明度、目标点圆角、方向箭头宽度和标题粗细。Unity 沿用项目 Noto Sans CJK，浏览器使用系统字体；字形、纹理采样与抗锯齿仍由各自渲染引擎决定，因此没有宣称逐像素完全相同。

![原型与 Unity 紧凑关卡对照](screenshots/sokoban-comparison-compact.png)

![原型与 Unity 高地图对照](screenshots/sokoban-comparison-tall.png)

左侧为确认原型，右侧为真实 Unity 渲染。高地图在正式教学编排中为第 08 关，原型中为第 02 张预览地图；地图内容保持一致。

## 检查记录

- 已通过既有 20 组独立测试：core、input、persistence、jump、jump-persistence、sheep、sheep-session、sheep-persistence、idiom、idiom-persistence、watermelon、watermelon-persistence、sudoku、sudoku-session、sudoku-profile、sudoku-persistence、minesweeper、minesweeper-session、minesweeper-input、minesweeper-persistence。完整输出在 `artifacts/validation/sokoban/regression.txt`。
- `scripts/test-sokoban.sh`：8 组核心规则检查通过。
- `scripts/test-sokoban-persistence.sh`：18 组会话/存档检查通过，包括完整 50,000 步恢复、异常 JSON、内容版本变更、坏档归档、原子写入重试，以及读取后主档被改坏时仍保留合法备份。
- `scripts/test-sokoban-content.sh`：17 项导入/事务测试、19 项严格目录拒绝测试、108 关生产规则重放和每个非终局前缀的完整撤回通过。见证共 4,596 步 / 1,203 次推动；资源与 manifest 可逐字节重现。Unity 规范化的图片导入 metadata 作为有哈希的模板固定，避免 Android 构建补写字段后使清单失效。
- `scripts/test-sokoban-input.sh`：83 项断言通过，覆盖单指滑动、缩放拖动、多指取消、方向盘归属、完整抬起后继续、归一化视口、48dp 命中区几何以及 320/360/388/432dp 宽度。
- `scripts/test-sokoban-ui.sh`：233 项断言通过，使用真正 UGUI、Camera.Render 和 GraphicRaycaster；在 720×1150、720×1320、720×1455、720×1582 可用尺寸下验证原型几何、真实点击、计数/撤回、帮助/设置、通关结果、缩放、50,000 步单行显示及高地图完整适配。额外覆盖实际 AppController 设置/后台/保存提示，以及短屏从宿主宽设计框进入 720 宽页面的初始化链路。所有推箱子夹具使用注入的内存 profile，不读写玩家存档。
- 既有 `scripts/test-minigame-ui.sh`：213 项断言通过，覆盖八张大厅卡顺序、跳一跳回调、数独和旸了个旸 UI；使用本工作树隔离的 Gradle/ADB 环境运行。
- 独立源码复核已完成。已修复「取消最终动画后漏掉通关面板」「短屏初始化比例与进入时比例不同」「物理命中区过小/重叠」；最终复核未发现新增可落实问题。审查结论不替代设备验收。
- 独立 Android 开发包已通过 IL2CPP/ARM64 构建，日志记录 `Build Finished, Result: Success.`。包名 `com.duanyao.xanaduyonder.sokobantest`，不覆盖正式应用；APK 为 `artifacts/builds/XanaduYonder-0.4.0-sokoban-validation.apk`，62,988,242 字节，SHA-256 `6ae1928de6364d0eacca6e11aa89fea7b154af8e44a8098fb6fd6f9456a0bad1`。aapt 核对为 minSdk 26、targetSdk 36、ARM64，版本仍为本项目 0.4.0 开发包，未发布正式版本。
- 设备安装与启动使用本轮首次构建（SHA-256 `2ef7f25c8bc4df77fece4ea17fd246b529f69e41276b1512922ad0a79539e961`）；最终包仅更新可重现的图片导入信息及对应 manifest，游戏代码与画面相同。后续接入 OPPO PKM110 / Android 16 / ARM64 设备，1080×2373，当前覆盖 density 480。第一次流式安装随连接中断失败；重试 `adb install --no-streaming -r` 成功，包管理器确认独立包存在，启动 Activity 后进程与前台 Activity 正常，Unity 日志出现 `XanaduYonder ready` 和启动页完成记录。日志保存在 `artifacts/validation/sokoban/android-startup.log`。这只验证安装和启动，尚未完成真机画面对照、单/多指交互、后台恢复及低端性能验收；桌面模拟检查与 APK 编译不代替这些结果。

## 内容与许可边界

首版入包 8 关原创教学和 100 关 Boxoban，共 108 关，每关已通过生产规则重放完整解法。章节使用中性编号；机器证明可解不代表最优解，也不代表已经完成真人难度校准。关卡及美术来源见 `THIRD_PARTY_NOTICES.md` 和资源 manifest。7 张精灵、4 个短音效均从已归档 CC0 源资源选取；音量的设备听感仍需人工验收。
