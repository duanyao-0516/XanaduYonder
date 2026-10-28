# 五子棋实现与验证记录

日期：2026-10-28。开发仅在 `/Users/bytedance/.codex/worktrees/gomoku/XanaduYonder` 的 `p/duanyao.0516/feature/gomoku` 分支，从 `master` 当时的 `8d64b5f` 开始。其他工作树后续提交未合入。最新正式发布版本仍为 v0.4.0；本次不创建发布或修改正式安装。

随后按用户要求完成独立 Android 模拟器验证：65 条操作／存档断言通过、36 张截图，覆盖实际 AI 应手与取消、双人胜负、进程恢复及文件故障；详见[模拟器验证记录](verification-gomoku-emulator.md)。未操作正在被其他任务使用的真机。

## 完成范围

人机简单／普通／困难、执黑／执白、本地双人、换边、预选／取消／确认、四边 A–O 与 1–15 坐标、末手标记、胜利连线、悔棋、暂停、后台及离开恢复、每模式独立续局、终局只读、存档异常和重试均已接入。顶部直接复用 `UiFactory.IconButton` 的 Home / Book / Settings，坐标分别为 `(56,34)`、`(536,34)`、`(606,34)`，大小均为 58×58。无全屏入口。

算法、线程与持久化边界见[架构说明](architecture.md)及[完整设计](superpowers/specs/2026-10-28-gomoku-design.md)。AI 是原创有界搜索，无联网依赖；简单／普通／困难采用 100／450／1200 ms 目标预算，困难增加连续冲四证明。候选裁剪不等于完整搜索，未宣称最优棋力。主机基准与局限见[核心测试说明](../Tests/Gomoku/README.md)。

## 自动验证

| 验证 | 结果 | 范围 |
| --- | --- | --- |
| `scripts/test-gomoku.sh` | 22 组通过 | 四向胜负、长连、满盘和棋、合法重放、坐标、战术、冲四证明、预算、取消、不可变输入 |
| `scripts/test-gomoku-session.sh` | 12 组通过 | 预选确认、回合、双方悔棋、暂停、请求身份、重复及过期结果、终局 |
| `scripts/test-gomoku-persistence.sh` | 14 组通过 | 真实 JSON、严格字段与 token、大小深度、合法历史、原子主备份、坏档和真实文件失败 |
| `scripts/test-gomoku-input.sh` | 19 组通过 | 225 个交点、四角、H8/H10、边界、拖动、多指、取消、禁用和旧手势 |
| 合集 UI 回归 | 213 项断言通过 | 720×1320 与 720×1582，八款大厅顺序、现有数独计数与交互、图标、其他页面渲染 |
| 五子棋专项 UI | 1,064 项断言通过 | 真实生产 UGUI、相机渲染、指针事件、设置与弹窗、存档异常、异步 AI |
| Android ARM64 验证包 | 构建成功 | 独立测试包，非正式发行 |

既有 `test-core.sh`（43）、`test-input.sh`（10）、`test-persistence.sh`（13），以及数独规则／会话／存档、扫雷规则／会话／输入／存档均执行成功。生产源码经过 Unity 6000.3.27f1 Android 目标导入编译。合集 UI 使用本工作树的隔离环境运行：

```bash
PROJECT_ROOT="$PWD"
source scripts/gomoku-unity-environment.sh
scripts/test-minigame-ui.sh
```

专项 UI 追加真实 `AppController.OpenSettings` / `StopAnimation` / `OnApplicationPause` / `OnApplicationFocus` 链路：设置完成恢复原回合，设置期间后台关闭不执行完成回调，迟到回调不复活棋局，明确继续后才能重新选点；共享及五子棋保存错误的优先级和重试均通过。

最终 UI 日志出现一条 UnityEditor.QuickSearch 启动索引的 `ArgumentOutOfRangeException`（`SearchDatabase.EnumerateAll` → `SearchInit.IndexationOnStartup`）；调用栈仅在编辑器内部，没有游戏代码帧。检查器继续执行至 `GOMOKU_UI_CHECKS_PASS: 1064 assertions` 并返回 0。此为观察到的编辑器日志事项，未修改项目代码以隐藏该异常。

独立审查覆盖设计符合性与代码正确性，另一个未参与实现的审查代理检查了规则／AI、请求验收、存档恢复、输入和宿主生命周期。已处理初始取消新局后主按钮不可用、棋盘缺失 raycast 接收层、玩法说明高度等实际问题；最终审查未发现未解决的明确代码问题。

## 场景与验证边界

| 已确认设计场景组 | 实现与验证方式 |
| --- | --- |
| 人机三档、黑白先后手、新局取消 | 真实设置按钮与会话配置，确认前不改变旧局 |
| 点击空点、换点、取消、重复／边界点 | 规则、输入测试及生产 UGUI 点击 |
| 电脑思考、应手、已完成结果取消 | 会话身份测试与真实 Task.Run → 主线程 Update 集成 |
| AI 悔棋前后、双人同意／拒绝 | 会话测试及真实双人确认弹窗 |
| 双方回合、换边、再来一局 | 玩家身份与执子单独维护；本地再来默认换边 |
| 胜负、长连、和棋、终局恢复 | 合法棋谱重放、渲染胜线、只读棋盘与禁用悔棋 |
| 规则／设置、新局确认、暂停、背景与离开 | 撤销预选和旧任务；恢复明确继续；旧设置回调失效 |
| 双模式独立续局 | 两个存档槽，恢复时不保留任务或预选 |
| 备份恢复、两档损坏、保存失败和重试 | 真实临时文件；未确认新局不覆盖坏档；失败退出提示风险 |

上述场景对应已确认的交互草图状态，不等同于 Android 人工完成全部 42 个场景。触摸 OS 取消、多指与拖动逻辑已做生产输入状态机测试；编辑器真实交互使用鼠标事件，不代替设备触摸分发。

## 构建产物与截图

- APK：`artifacts/builds/XanaduYonder-0.4.0-gomoku-validation.apk`，ARM64，约 60 MiB；`aapt dump badging` 确认包名 `com.duanyao.xanaduyonder.gomokutest`、versionName 0.4.0、versionCode 4、minSdk 26、targetSdk 36。
- SHA-256：`fc8e9e3e32083dffd2b7b5131cac5fbd93c49bf1867acea2428c180a8d7ea510`。
- 构建日志：`artifacts/logs/build-gomoku-validation.log`，结果 `Build Finished, Result: Success.`；构建后 `ProjectSettings/ProjectSettings.asset` 无差异。
- UI 报告：`artifacts/validation/gomoku-ui/report.txt`；36 张短/长屏 PNG 同目录，包括双方预选、AI 设置/思考/实际应手、规则、悔棋同意、暂停、胜线/和棋和存档异常。
- 已人工查看其中短屏预选、人机设置、规则、双人悔棋、终局胜线，以及长屏 AI 应手和保存失败弹窗，坐标、文字和按钮无可见裁切或重叠。

## 隔离与未验证项

- `Library`、`Temp`、`Library/GomokuGradle` 均为本工作树本地路径，无指向其他工作树的链接。Unity 检查与构建串行，使用私有临时 ADB 端口。
- 验证包名为 `com.duanyao.xanaduyonder.gomokutest`，构建方法以 try/finally 恢复包名与产品名，不改正式版号或其他游戏存档协议。
- 初次构建时没有连接设备；随后用户明确真机由其他任务使用，已改用专用模拟器完成安装与运行验证。Android 真机上的 AI 延迟、发热、长时运行、多指与系统行为仍需实机验证，不把模拟器结果称为真机验收。
- 本地提交后保留工作树继续开发，不合并、推送或发布。
