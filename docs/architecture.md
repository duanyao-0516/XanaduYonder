# 架构与存档契约

当前开发版 v0.4.0 接入 2048、跳一跳、旸了个旸和数独。大厅、设置和生命周期由公共 shell 管理，每款游戏独立拥有规则、表现与存档。当前公开 Release 仍为 v0.2.0；本页描述源码中的模块契约。

| 模块 | 规则 / 表现 | 独立文件 | 记录 |
| --- | --- | --- | --- |
| 2048 | `Game2048` / `BoardView` | `xanaduyonder-v1.json` | 对局、最高分及共用声音/触感设置 |
| 跳一跳 | `Core/Jump` / `JumpPage`、`JumpSceneView` | `jump-v1.json` | 对局、最高分 |
| 旸了个旸 | `Core/Sheep` / `SheepPage`、`SheepBoardView` | `sheep-v1.json` | 对局、累计通关、胜利已计数标记 |
| 数独 | `Core/Sudoku` / `SudokuSession`、`SudokuProfile`、`SudokuPage`、`SudokuBoardView` | `sudoku-v2.json` | 每难度一局、撤回、计时、提示、模式、全局通关记录 |

[跳一跳模块](jump-game.md)、[旸了个旸模块](sheep-game.md)和[数独模块](sudoku-game.md)分别说明各自规则和存档。新增游戏不修改以下 2048 和公共设置的旧协议。

## 职责

- `Assets/Scripts/Core` 不引用 Unity。`Game2048` 是棋盘状态的唯一来源，输入方向后返回移动轨迹、合并得分和新方块位置。快照采用防御复制，外部不能修改进行中的对局。
- `AppController` 管理大厅、游戏、设置和弹窗。大厅使用 `ScrollRect` 与 `RectMask2D`，固定顶部导航、底部状态区域；内容中的卡片高 350 设计单位、间距 24，返回时保留滚动位置。
- `BoardView`、`JumpSceneView` 和 `SheepBoardView` 显示对应规则模型，不计算胜负。各页面在暂停、失焦、离开时终止尚未提交的输入或结束动画，并按最终模型重绘。shell 切换时使其他游戏页面离开，跳一跳同时关闭独立相机。
- `SwipeGesture` 保存一次触摸的所有权。只有从棋盘开始、超过阈值、由原手指正常结束的操作可以提交。UGUI 会把取消触摸派发为 pointerUp，适配层因此还会核对 `TouchPhase.Canceled`。
- `GameFeedback` 合成短音效，并通过 Android 系统触感接口反馈合并；开关由公共存档保存，不申请网络服务。
- `JsonSaveCodec` 使用 Unity 官方 Newtonsoft JSON 包的实际解析器，逐项校验 token 类型；`LocalSaveStore` 负责临时文件、原子替换和备份恢复。

## 2048 规则

棋盘以左上角为索引 0，按行存储 16 格。每条线从移动目标边缘处理，合并双方消耗后不会在本次移动再次合并。初始生成两个方块；每次有效移动后随机选择一个空位，并以 90% / 10% 的概率生成 2 / 4。无效移动既不生成方块，也不消耗随机数。

总分使用 64 位整数，达到 `long.MaxValue` 后饱和。最大方块为 2^30，此值不再合并，避免整数溢出。达到 2048 后可继续，胜利提示的已确认状态随对局保存。

## 2048 与公共设置文件

存放于 `Application.persistentDataPath/xanaduyonder-v1.json`。Android 正常路径为应用独占的外部文件目录；不读取其他应用的数据。`.bak` 为上一份合法存档，`.tmp` 为待提交文件。

```json
{
  "schemaVersion": 1,
  "saveKind": "xanaduyonder",
  "game": {
    "schemaVersion": 1,
    "gameId": "2048",
    "cells": [2, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    "score": "0",
    "winState": 1
  },
  "bestScore": "0",
  "soundState": 1,
  "hapticsState": 1
}
```

- 根版本、游戏版本与标识必须准确匹配。未知版本不迁移。
- 格子必须是 16 个整数，每格为 0 或 2..2^30 之间的二次幂；已保存的正常对局至少有两个非空格。
- 分数使用非负十进制字符串，避免跨工具处理时丢失大整数精度。缺失、null、数值类型、负值、溢出均拒绝。最高分不能低于当前分数。
- `winState` 仅允许 1（未确认）/2（已确认）；已确认时必须有至少一个 >=2048 的方块。
- 声音与触感状态仅允许 1（开启）/2（关闭），不把缺失字段静默解释为关闭。
- 重复键、非法类型、过深或超过 64 KiB 的文件被拒绝；不向调用方暴露部分解码结果。

每次有效移动、设置变化以及应用暂停/失焦/退出前同步保存当前模型。写入先 flush 临时文件，再替换正式文件；只有正式文件合法时才用其更新备份，避免损坏文件覆盖最后一份可恢复数据。读取优先正式文件，其次备份，均不可用时启动新对局并显示提示。写入失败继续允许游玩并提示当前未保存，后续写入成功会清除该错误。

## 旸了个旸的独立边界

`SheepGame` 拥有棋盘、7 格卡槽、移出区、三消与道具状态；`SheepBoardLayout` 根据关卡和布局编号派生几何位置；`SheepLevelGenerator` 构造初始局和一条可解取牌顺序。`SheepPage` 在取牌或使用道具后先保存模型，再播放表现；连续快速点击可接续已提交的新状态。完整挑战获胜时先更新 `CompletedRuns/WinRecorded` 再写盘，避免恢复终局后重复计数。

`SheepSaveCodec` 严格校验 schemaVersion 1、gameId `sheep`、固定字段、JSON token 类型、牌数与位置列表、道具/撤回状态和胜利计数一致性，文件限制 128 KiB、深度 8。`SheepSaveStore` 仅访问 `sheep-v1.json` 及对应 `.bak/.tmp`；损坏主档尝试合法备份，写入失败不清空旧档。详见[字段契约](sheep-game.md#独立存档)。

## 验证层次

`scripts/test-core.sh`、`scripts/test-input.sh`、`scripts/test-persistence.sh` 编译同一份生产 C# 源码；持久化测试在真实临时目录执行，使用与 Unity 包对应的 Newtonsoft.Json 13.0.2。Unity 内还执行同一组存档检查。跳一跳另有 `scripts/test-jump.sh` 和 `scripts/test-jump-persistence.sh`；旸了个旸另有 `scripts/test-sheep.sh` 和 `scripts/test-sheep-persistence.sh`，覆盖同一份生产规则与存档代码。测试中的生成解法通过真实 `SheepGame.Pick` 重放，检查遮挡与卡槽容量约束，而不只检查牌数是否为 3 的倍数。Android 真机验证负责触摸分发、滚动、图形、IL2CPP、文件系统和应用生命周期的最终集成；源码审查和独立测试不代表设备验收已完成。

本轮旸了个旸修订保留 `sheep-v1.json` 的字段协议及旧布局0..2。used 布尔改作三种道具的历史记录，配额不再由它们决定；复活仍一次。新挑战布局3..14采用多岛与错位叠层，并排除连续重开的上一布局。额外布局测试入口为 `scripts/test-sheep-layouts.sh`。

## 数独的独立边界

`SudokuGame` 拥有正式数字、候选、撤回、错误与提示配额；`SudokuControls` 将普通/快速和正式/铅笔四种输入模式映射成规则操作。`SudokuSession` 拥有暂停、计时和已消耗次数的帮助，导出时带上本局产生的完成记录；`SudokuProfile` 合并全局完成记录并管理每个难度的一场未完成对局。`SudokuPage` 负责 Unity 呈现、输入和异步请求生命周期。

内置题库有四档各 500 道题，题面与唯一答案固定版本。答案计算、唯一性搜索、逻辑难度评级和完整提示证据重放在开发机完成。客户端启动只检查资源 SHA-256、题面与答案一致性、答案行列宫合法性；填写时直接比对内置答案。题库导入与校验见[内容管线](sudoku-content.md)。

提示从当前正确盘面重新构建候选，在后台线程中进行一步逻辑推导，有取消检查和 200ms 预算。无支持技巧或超预算时明确提供直接揭晓。主线程只接受仍属于当前会话、请求、盘面 revision 且在前台未暂停的结果，并在展示时扣除一次配额；过期、取消和计算失败不扣次。帮助与扣次同次保存，重看同一帮助不重复扣次，任何盘面变化令其失效。

数独存档单独校验所有 JSON token、候选、合法动作历史、累计错误/提示、关卡 ID 和终局一致性，以临时文件 flush 后原子替换主档。原三款游戏的协议未改动。测试入口分别检查规则、真实文件持久化、会话生命周期、四档存档迁移、关卡选择策略和完整题库证明，Android 验证范围记录在[初版数独验证](verification-sudoku.md)及[本次修订验证](verification-sudoku-refinement.md)。
