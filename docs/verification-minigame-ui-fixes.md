# 小游戏界面与大厅修正验证

日期：2026-10-27。基于本轮新拉取的 `origin/master`：`128e7cc8bbc2f967df9c11a196c7068dee9e9673`。

工作区：`/Users/bytedance/.codex/worktrees/minigame-ui-fixes/XanaduYonder`；分支：`p/duanyao.0516/codex/minigame-ui-fixes`。原目录的分支和未跟踪数独设计稿保持不变。本记录描述发布前的开发与验证阶段；后续正式产物与发布信息见[v0.4.0 发布记录](releases/v0.4.0.md)。

## 完成范围

- 旸了个旸教学牌面采用完整牌场的缩放上限；656 宽面板的教学牌宽从 160 调整到约 72.52，与挑战牌一致。保持牌面比例、遮挡、居中和挑战牌场高度利用。
- 跳一跳顶栏采用大厅、重开、玩法、设置四个共享图标，移除底部重开/玩法文字按钮，原回调不变。
- 数独选关/对局统一导航图标，工具采用撤回、橡皮、铅笔、铃铛；保留提示余量和铅笔开启配色。玩法采用半透明遮罩的居中可滚动对话框，保留暂停、恢复和输入隔离。进度为已填写/剩余空格，排除给定格及候选，错误正式填写计入已填写；删除底部重复控件。
- 大厅顺序为 2048、跳一跳、旸了个旸、合成大西瓜、成语接龙、数独；同步实际显示的序号和说明。
- 2048 卡片改为浅绿色，显示最高纪录和最高数字。最高数字在原 schema 1 中兼容增加 `bestTile`；旧档缺字段取保存盘面的最大值，无法推算此前已结束对局的历史最高数字。显式非法字段不作为旧档处理。
- 数独卡片显示总完成与四档完成数，分两行容纳满额数据；来源为已校验的去重完成记录。难度菜单整体圆角、内部方角，奶油/浅沙配色区别于关卡卡片。
- 大厅导航内容起点从 132 缩至 108；离线/存档说明放到卡片流末尾，首页不再保留固定状态底栏。其他游戏状态区保持原逻辑。

## 自动化与源码审查

| 命令 | 结果 |
| --- | --- |
| `scripts/test-jump.sh` | 25 passed |
| `scripts/test-input.sh` | 10 passed |
| `scripts/test-sheep-presentation.sh` | 1,370,322 几何断言通过 |
| `scripts/test-sheep-layouts.sh` | 12 passed |
| `scripts/test-sudoku.sh` | 27 passed |
| `scripts/test-sudoku-session.sh` | 8 passed |
| `scripts/test-sudoku-selection.sh` | 7 passed |
| `scripts/test-sudoku-profile.sh` | 28 passed |
| `scripts/test-persistence.sh` | 13 passed |
| `scripts/test-minigame-ui.sh` | 205 实际 UGUI/像素/交互断言通过 |
| `scripts/test-idiom-ui.sh` | 最终源码上 1,594 断言通过 |
| `git diff --check` | 通过 |

羊牌跨关卡尺寸回归先复现 160 > 72.52，修复后通过。最高数字用真实 Newtonsoft 和临时文件验证旧字段缺失、显式 null/错误类型/非法值、跨重开保留、新记录提升及备份恢复。数独分档回归覆盖空记录、存档往返、通关更新、重玩去重和非法难度。

UI 检查在 720×1320、720×1582 的真实 Unity Play Mode 中渲染页面；覆盖点击、填写/擦除/撤回/候选/错误数字计数、玩法阻断底层输入、暂停/关闭恢复计时、四档下拉选项及满额 2000 关文案。最初截图暴露自定义图标在编辑器缺少 CanvasRenderer；`UiIconGraphic` 和 `SudokuIcon` 现显式声明组件依赖。最终检查不手动重建图标网格，通过正常 Canvas/Camera 渲染检查实际 CanvasRenderer 网格和像素，空白图标会失败。

独立源码审查覆盖职责、真实读写契约、记录更新顺序、页面状态显隐及下拉布局。唯一验证缺口（先强制重建才检查图标像素）已修正并复审关闭。Unity Play Mode 日志存在编辑器自身的 `UnityEditor.Search` 启动索引异常；测试通过独立协程执行完毕，最终无编译错误或本次测试异常。

## 构建与 Android 模拟器

Unity 6000.3.27f1 的普通开发包与独立验证包均返回 `Build Finished, Result: Success`。

- 开发 APK：`artifacts/builds/XanaduYonder-0.4.0.apk`，62,854,756 bytes。
- SHA-256：`b2886781ab09fdfcd96fe2a07b0a4e6ec5fa8f7d62f6625c92f44551a23b8b35`。
- 独立验证 APK：`artifacts/builds/XanaduYonder-0.4.0-ui-validation.apk`。
- 验证 APK SHA-256：`d4b12220014d2e617833bfef0ba3e27cabcf78002aedebb3b7cedbce3f00fb27`。
- 模拟器：`emulator-5554`，1080×2400，独立包名 `com.duanyao.xanaduyonder.uifixestest`；没有覆盖正式应用或其他测试包存档。临时构建入口在产物生成后已移除。

已查看最终模拟器截图，确认大厅浅色卡片及成绩、排序、页头、滚动说明；进入数独后展开菜单、选择简单难度、开始关卡、打开玩法对话框，按钮图标/布局正常。早期同轮验证包也实际检查了跳一跳顶栏与玩法回调。最终采集的 Unity/AndroidRuntime 日志片段未出现 FATAL 或应用异常。

截图与报告：`artifacts/validation/minigame-ui/`；模拟器截图及日志：`artifacts/validation/minigame-ui-device/`（最终截图以 `-final.png` 结尾）。截图测试使用独立内存数据；完整 2000 关成绩截图为边界测试数据，不是玩家记录。

验证边界：没有执行 Android 真机回归、长期游玩或全量生命周期/性能回归；编辑器与模拟器结果不代替真机验收。
