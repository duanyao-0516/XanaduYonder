# 数独离线题库与提示验证

## 内置内容

`Assets/Resources/Sudoku/puzzles.txt` 包含 2,000 道标准 9×9 数独，入门、简单、中等、困难各 500 关。首次启动不下载，不在客户端生成题面、重新分级或搜索唯一解。所有题的唯一解、答案和分级均在电脑导入时计算。

题库版本固定为 `sudoku-exchange-d8c8eba-v1`；稳定 ID 为 `se-v1-d{0..3}-l{001..500}`。同一版本不更换 ID 对应的题目，导入器发现已有文件与新结果不同会拒绝覆盖。改题库需要新版本及显式存档迁移。

文本首行是 `# version|count|sha256`，SHA-256 对后续规范化数据行（每行以 LF 结束）计算。每行数据为：

```
id|difficulty|level|givens81|solution81
```

空格用 `0` 表示。当前文件 367,667 字节，数据部分 SHA-256 为 `c664417fa24bf586f2bfb4fe7a663c40ed1d0508df364e4d88b130a1ede0c5f9`。

`SudokuCatalog.Parse` 在加载边界核对版本、数量、SHA-256、ID/题面/关卡重复、81 格范围、题面与答案匹配，以及答案在全部 27 个单元中满足 1–9。`SudokuPuzzle.TryValidate` 同样只作有界的数据校验，不调用回溯求解器。`Get` 与 `Puzzles` 返回深拷贝，页面或对局不能修改题库原件。

## 来源与许可

来源为 [Sudoku Exchange Puzzle Bank](https://github.com/grantm/sudoku-exchange-puzzle-bank)，固定 commit `d8c8ebaee0c08c412cfba96af1923dfa61c83317`，采用 [Unlicense](../ThirdParty/Sudoku/LICENSE.txt)，允许商业使用与再分发。

每个最终题目保留在 [selected.tsv](../ThirdParty/Sudoku/selected.tsv) 中，可追溯源文件、原始行号、上游 12 字符摘要、上游评级及原题面。下载前缀及其 SHA-256 记录于 [source-prefixes.json](../ThirdParty/Sudoku/source-prefixes.json)。原始上游标签仅为来源信息，不直接作为本应用分档。

本次只下载 1.8 MB 前缀：easy 2,000 行、medium 3,000 行、hard 12,000 行、diabolical 1,000 行。实际扫描 easy 前 2,000、medium 前 3,000、hard 前 5,462，共 10,462 题后即已筛满；未使用 diabolical。扫描中 2,702 题超出支持逻辑范围，被排除。

## 难度规则

导入时每步使用与客户端相同的逻辑引擎，重建候选、推导到一格正式填数，整题按该确定性解题顺序实际使用的最高技巧分档。它衡量当前引擎的解题路径，不声称是所有可能人类解法中的最小难度。未用空格数量分级。

| 档位 | 实际最强技巧 | 数量 |
| --- | --- | ---: |
| 0 入门 | 全程裸单 | 500 |
| 1 简单 | 需要隐藏单，未用候选消元 | 500 |
| 2 中等 | 需要区块排除、裸数对或隐藏数对 | 500 |
| 3 困难 | 需要裸三数组、隐藏三数组或基础 X-Wing | 500 |

所有题都能由这些技巧完成。需要猜测、回溯、变体规则或更高技巧的题不收录。

## 一步提示与预算

`SudokuHintEngine.Find(puzzle, values, preferredCell)` 是离线验证用的不限时入口。客户端调用带 `CancellationToken cancellation, int budgetMilliseconds` 的重载，在后台以 200 毫秒预算计算一步提示；该 API 本身同步执行，线程调度由会话层负责。

引擎只接收正式数字，忽略用户铅笔草稿。存在错误正式数字、修改给定格或无效数据时不产生付费逻辑提示。优先解释所选空格；否则选择其他可推导位置。候选消元仅在临时推理盘面发生，不修改调用者数组，不修改草稿，不逐次收费。最终仅推荐一个确定数字。

循环、各单元及候选组合检查取消与时间预算。取消抛出 `OperationCanceledException`，由会话层取消请求且不扣次。预算超限返回 `IsDirect=true` 并明确说明达到时间预算；不支持的推理也明确返回“直接揭晓”。直接结果不携带伪造的逻辑步骤。

逻辑结果包含中文解释、证据格，以及 `SudokuReasoningStep[] Steps`：技巧、来源/目标单元、模式格、候选数字掩码、被删除格与掩码，最后一项为实际填数。单元编号 0–8 是行、9–17 是列、18–26 是宫。`Copy` 深拷贝证据与步骤。

## 重复导入和验证

需要仓库 `global.json` 指定的 .NET 10 SDK；下载阶段额外使用 Python 3 标准库。无需 Unity、Java、第三方解题程序或运行时网络。

```sh
# 行为与技巧正反例
./scripts/test-sudoku-content.sh

# 完整 2,000 题：唯一解、去重、连续关卡、实际分档、独立证据重放
./scripts/test-sudoku-content.sh --bank

# 直接用仓库内 selected.tsv 重建同样题库并全量验证，不联网
python3 scripts/import-sudoku.py

# 从固定版本的有界前缀重新筛选，并全量验证
python3 scripts/import-sudoku.py --refresh
```

前缀缓存在 `Tests/SudokuContent/obj/import-cache/`，不纳入版本控制。`--easy`、`--medium`、`--hard`、`--diabolical` 可以显式增加读取行数；数量不足时导入失败，不会减少最终每档题数。

`SudokuSolver.CountSolutions` 是独立的精确回溯验证器，最多计数到 2，只供电脑导入和测试调用。它与提示逻辑解题器分离。

`Tests/SudokuContent/ProofChecker.cs` 不调用提示引擎的单元表、同单元判断或消元搜索：从当前正式盘面独立重建候选，检查每项单元约束和候选排除，再验证最终填数。这比仅对照最终答案更严格。裸单、隐藏单、区块排除、裸/隐藏数对、裸/隐藏三数组、双方向 X-Wing 都有正例、破坏结构的反例及错误消元证明拒绝测试。

全量验证结果：

| 档位 | 独立验证的逻辑填数 |
| --- | ---: |
| 入门 | 24,627 |
| 简单 | 26,403 |
| 中等 | 26,711 |
| 困难 | 26,653 |
| 总计 | 104,394 |

全程实际证明了 87,378 次裸单、17,016 次隐藏单、3,572 次区块排除、632 次裸数对、292 次隐藏数对、252 次裸三数组、18 次隐藏三数组、407 次 X-Wing。未对客户端设备帧耗时或真机交互作结论；这部分由页面集成验证负责。
