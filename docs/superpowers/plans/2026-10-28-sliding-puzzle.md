# 数字华容道 Implementation Plan

> **For agentic workers:** Use subagent-driven-development for independent modules; review requirements and code before delivery.

**Goal:** 在最新 master 0f6e4fd 的隔离工作树接入第 13 款小游戏，严格还原用户批准的数字华容道 UI 及本轮修改。

**Architecture:** 纯 C# 规则及固定关卡、独立会话/档案/严格存档、Unity 页面/棋盘输入与动画、AppController 路由。公共导航、颜色、字体、声音/触感沿用现有 UiFactory；不复用 2048 移动规则。

**Tech Stack:** Unity 6000.3.27f1, UGUI, C# 7.3-compatible code, Newtonsoft.Json 13.0.2, .NET 10 tests.

## 用户批准的设计与调整

原始稿归档于 `docs/prototypes/sliding-puzzle/approved-reference.html`。参考内容宽 390 CSS px，左右28，棋盘334；按同一可用画面等比映射到720设计单位。手机外框、浏览器阴影不是App UI。

- 颜色、标题、模式切换、统计卡、棋盘、操作区、选关和完成弹窗按原稿；使用合集 Noto 字体，记录字号/字重差异。
- 大厅、设置使用现有统一图标按钮；设置左侧增加 Book 玩法按钮并打开规则 dialog。原底部玩法入口移到顶部，撤回/重开平分原操作行。
- 所有提示合为页面最下方一行：首次移动开始计时 · 不限时；开始后为自动保存/练习状态；失败时该行显示真实未保存提示。正式版移除体验局字样。
- 删除“一格一格，回到恰好的位置”。
- 未解锁进阶篇/挑战篇标题与说明灰色，右侧网格绘制锁图标；解锁后整行可进入该章节。
- 自由拼图3/4/5阶分别续局，默认4阶；闯关3章每章6个真实可解关卡，完成本关解锁下一关及下一章。所有关卡必须能游玩，不保留演示占位。
- 每次单块合法移动+1步，撤回+1步且标练习；首次有效移动开始计时，设置/帮助/选关/后台暂停。通关幂等记账，辅助不混入无辅助纪录，仍能解锁。
- 独立 `sliding-puzzle-v1.json` 保存自由各档、活动闯关、完整撤回历史、用时、真实纪录和进度。切模式/返回保留，重开须确认。

## Task 1: 规则、生成、内容与输入

- [x] 新建 `Core/SlidingPuzzle/SlidingPuzzleGame.cs`、Snapshot、Catalog、Generator。
- [x] 先写 `Tests/SlidingPuzzle` 行为测试：不相邻/跨行/空格拒绝；合法移动与胜利；撤回计数；恢复历史与防御复制；奇偶不可解拒绝；18关解法重放；3阶最短距离独立BFS验证。
- [x] 固定前章6题，后两章提供固定题与合法解法，不把洗牌数称最优难度。
- [x] `scripts/test-sliding-puzzle.sh` 红后绿，记录输出。

## Task 2: 会话、档案、存档

- [x] `Runtime/SlidingPuzzle` 新建 Session/Profile/SaveData/SaveCodec/SaveStore，仅存储层使用JSON。
- [x] 先写 `Tests/SlidingPuzzlePersistence`：计时暂停、各模式独立续局、解锁和重复获胜、真实最佳元组、null/缺字段/错误类型/重复键/未知版本/非法历史、主档坏备份恢复、失败保留旧档。
- [x] 显式API与规则模块对齐；每次有效操作捕获并原子保存，恢复按真实规则重放。
- [x] `scripts/test-sliding-puzzle-persistence.sh` 红后绿。

## Task 3: Unity UI 与宿主接入

- [x] 创建 `SlidingPuzzlePage`（拆 Layout/Dialogs）、BoardView、Pointer；使用单输入提交通道和正常触点所有权，取消后不补发。
- [x] 追加大厅第13张卡，更新路由、离页、保存、终局、设置及后台生命周期；不改变其他游戏数据。
- [x] `SlidingPuzzleUiChecks` 使用真实UGUI/Camera.Render/GraphicRaycaster；覆盖720宽的短屏/标准/长屏、3/4/5棋盘、长计数、双模式、锁定/解锁、帮助/设置/重开/胜利、后台和最终一步取消动画。
- [x] `scripts/test-sliding-puzzle-ui.sh` 先失败后实际渲染通过。

## Task 4: 对比与交付

- [x] 同盘面同可用尺寸对比原稿与Unity，逐项记录导航、文本、颜色、圆角、棋盘间距、按钮、锁图标和单行底注；修复差异后重拍。
- [x] 运行受影响共享UI回归、新规则与存档检查；基线 `test-core.sh` 已因递归编入 Sokoban JSON 类却未引用 Newtonsoft 而失败，单独修正其2048测试编译范围并复测，不把它归因于新游戏。
- [x] 使用独立包 `com.duanyao.xanaduyonder.slidingpuzzletest` 构建，隔离Gradle与ADB；安装到明确设备验证。不得覆盖正式App或停止其他任务设备。
- [x] 独立交叉审查功能/数据契约与UI符合性；修复后复验。
- [x] 更新README、架构、玩法/验证文档，保留截图和验证分层；不发布、不推送、不合并master。

完成证据见 [验证记录](../../verification-sliding-puzzle.md)。实际范围：Android36专用模拟器，实体手机验收边界已在记录列出。
