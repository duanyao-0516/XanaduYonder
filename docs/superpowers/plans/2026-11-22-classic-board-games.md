# 四款棋类游戏实施计划

**目标** 在 XanaduYonder 的逻辑策略大厅接入原创离线军棋、象棋、国际象棋和六角星跳棋。

**架构** 四款分别持有纯 C# 棋规与选招策略。棋类模块内复用页面、回合调度和严格棋谱存档，AppController 负责大厅、返回和生命周期。

**技术栈** Unity UGUI、C# 7.3 兼容规则测试、现有 Json.NET 和 Sprite 加载方式。

## 规则与界面接口

在 `Assets/Scripts/Core/BoardGames` 定义以下接口。点编号由具体棋规所有，棋谱通过初始参数与完整动作重放恢复。

```csharp
public interface IBoardPosition
{
    int CurrentSide { get; }
    int Winner { get; }
    int PointCount { get; }
    string Status { get; }
    BoardPiece PieceAt(int point, int viewer);
    IReadOnlyList<BoardMove> LegalMoves();
    bool TryPlay(BoardMove move);
    IBoardPosition Copy();
    BoardSearch CaptureSearch();
}
public delegate BoardMove BoardSearch(BoardDifficulty difficulty, int seed, CancellationToken cancellation);
```

席位从 0 开始。进行中为 `BoardResult.Ongoing = -2`，和棋为 `BoardResult.Draw = -1`。`BoardPiece.Kind` 为 0 表示空点，为 -1 表示对当前观察者隐藏的棋子。普通动作使用数字起点、终点与升变选择，跳棋另存路径，申诉和棋与跳过采用明确标记。搜索捕获不可被会话改动的副本，军棋只能捕获投影后的公开信息。

## 各任务与验收

| 任务 | 实施 | 验收 |
| --- | --- | --- |
| 国际象棋 | `Core/BoardGames/Chess` 实现 FIDE 行棋、易位、过路兵、四种升变、将死、逼和、死局及重复与步数和棋。三档共用 Negamax 与国际象棋局面评价。 | 开局 perft 前三层 20／400／8902，边界棋局、悔棋重放、各档合法应手。 |
| 象棋 | `Core/BoardGames/Xiangqi` 实现老将安全、象眼、马腿、炮架、困毙和已选 WXF 长将、长捉及自然限着。三档使用象棋自身的子力、炮马和将帅安全评价。 | 固定复盘局、规则例外、重复棋谱与 AI 战术。 |
| 军棋 | `Core/BoardGames/Army` 实现 5 列道路图、暗棋阵型、工兵铁路、行营、大本营、碰子、亮旗。三档使用公开资料启发式、身份概率、约束样本短搜索。 | 阵型、路径、对战矩阵和相同公开信息产生相同 AI 请求。 |
| 跳棋 | `Core/BoardGames/ChineseCheckers` 实现 121 孔、2／3／4／6 人、不同对角营、相邻步进、逐段连跳、完整回合、防堵门与胜负。三档逐步加入全队进度和有界多人推演。 | 走完五个独立电脑席位，选点、反跳、退一跳、换位变体和超时合法兜底。 |
| 会话与存档 | `Runtime/BoardGames` 持有唯一可变对局，按模式及跳棋人数分槽，按棋谱校验恢复。后台搜索由会话身份、盘面版本和当前席位约束。军棋双人回合开始、后台、离页续玩先遮挡；二次确认默认开。 | 确认或直接落子都能正确遮挡，暂停使在途结果失效。人机回到上一真人动作前，同机由下一席确认撤一步，暗棋从提交起锁定。坏档从有效备份恢复。 |
| 大厅与 UGUI | `AppController` 注册四个独立游戏。使用现有 Home、Book、Settings 和共享反馈。20 小图与 63 大 Logo 分别制作，象棋小图用圆圈中的「象」。竖屏使用真实安全区，跳棋保证整盘与连跳辅助，军棋可读暗棋牌，象棋留上下席位。四款各含可跳过可重进的示例教程。 | 三种大厅展示 27 个游戏可进入。窄屏棋盘几何、触控、防误触、教程、结算及上下边距用 Unity 编辑器用例检查。 |
| 验证与交付 | 在独立工作树运行 .NET 测试、Unity 编译及 UI 批处理。按实际可用设备补 Android、iOS 验证，记录完成层级。 | 保持原工作区状态，审核 diff 与新增资源许可证，记录棋力和设备尚需验证的项目。 |

每个规则模块先编写固定局面的回归测试，然后实现并运行该模块，最后在 UGUI 正常入口验收。
