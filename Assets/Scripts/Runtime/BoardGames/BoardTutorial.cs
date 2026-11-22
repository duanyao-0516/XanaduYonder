using System;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed class BoardLesson
    {
        private readonly int[] route;

        internal string Title { get; }
        internal string Instruction { get; }
        internal IBoardPosition Position { get; }
        internal int From { get; }
        internal int To { get; }
        internal int[] Route => route.Length == 0 ? route : (int[])route.Clone();
        internal int UndoAfterHop { get; }

        internal BoardLesson(string title, string instruction, IBoardPosition position,
            int from = -1, int to = -1, int[] route = null, int undoAfterHop = 0)
        {
            Title = title;
            Instruction = instruction;
            Position = position;
            From = from;
            To = to;
            this.route = route == null ? Array.Empty<int>() : (int[])route.Clone();
            UndoAfterHop = undoAfterHop;
        }
    }

    internal static class BoardTutorial
    {
        internal static BoardLesson[] Create(BoardGameId game)
        {
            switch (game)
            {
                case BoardGameId.Army: return Army();
                case BoardGameId.Xiangqi: return Xiangqi();
                case BoardGameId.Chess: return Chess();
                case BoardGameId.Checkers: return Checkers();
                default: throw new ArgumentOutOfRangeException(nameof(game));
            }
        }

        private static BoardLesson[] Army()
        {
            int[] red = ArmyGame.CreateFormation(121);
            int[] green = ArmyGame.CreateFormation(245);
            const int engineerSlot = 1;
            int engineer = (int)ArmyPieceKind.Engineer;
            int originalSlot = Array.IndexOf(red, engineer);
            red[originalSlot] = red[engineerSlot];
            red[engineerSlot] = engineer;

            int redFront = ArmyGame.FormationPoint(0, 0);
            int redCamp = ArmyGame.FormationPoint(0, 6);
            int greenFront = ArmyGame.FormationPoint(1, 0);
            int greenCamp = ArmyGame.FormationPoint(1, 6);
            var railroad = new ArmyGame(red, green);
            // Clear both ends of the left bridge so the engineer can demonstrate a turn.
            if (!railroad.TryPlay(new BoardMove(redFront, redCamp)) ||
                !railroad.TryPlay(new BoardMove(greenFront, greenCamp)))
                throw new InvalidOperationException("The army tutorial could not clear the left railway.");

            return new[]
            {
                new BoardLesson("布阵与军阶",
                    "军旗放在大本营，地雷摆在后两排，炸弹避开最前排。普通棋子按军阶比大小，同级碰子会一起移除。夺旗即可获胜。",
                    new ArmyGame(red, green)),
                new BoardLesson("沿公路进入行营",
                    "把左侧前排的红方棋子斜走一格，移进亮起的行营。公路每次走一格，行营里的棋子不能被攻击。",
                    new ArmyGame(red, green), redFront, redCamp),
                new BoardLesson("工兵在铁路上转弯",
                    "把工兵移到对岸左端。工兵能沿空旷的铁路转弯，其他可移动棋子在铁路上只能直走。",
                    railroad, ArmyGame.FormationPoint(0, engineerSlot), greenFront),
                new BoardLesson("同机交接",
                    "同机双人每次落子后都会遮住棋盘。把设备交给下一位，等对方确认接手后再显示己方棋子。",
                    new ArmyGame(red, green))
            };
        }

        private static BoardLesson[] Xiangqi()
        {
            return new[]
            {
                new BoardLesson("隔子用炮",
                    "让红炮隔着中间的黑卒，吃掉同一横线上的黑车。炮吃子必须隔一枚棋子，走到空格时不能跳子。",
                    XiangqiGame.FromFen("4k4/9/9/9/4p4/9/4P4/1C2p2r1/9/4K4 w"),
                    XiangqiPoint(7, 1), XiangqiPoint(7, 7)),
                new BoardLesson("避开马腿",
                    "马走“日”字。红马下方的兵堵住了那一侧的马腿，请把红马走到左上方的提示点。",
                    XiangqiGame.FromFen("4k4/9/9/9/4p4/9/4P4/2N6/2P6/2B1KA3 w"),
                    XiangqiPoint(7, 2), XiangqiPoint(5, 1)),
                new BoardLesson("应将",
                    "黑车沿纵线将军。把红帅向右移一格避开黑车，每一步都必须保证己方帅安全。",
                    XiangqiGame.FromFen("3k5/9/9/9/4r4/9/9/9/9/4K4 w"),
                    XiangqiPoint(9, 4), XiangqiPoint(9, 5)),
                new BoardLesson("过河走兵",
                    "红兵过河后可以向前或横走一格，不能后退。把这个红兵向右走一格。",
                    XiangqiGame.FromFen("3k5/9/9/9/4P4/9/9/9/9/4K4 w"),
                    XiangqiPoint(4, 4), XiangqiPoint(4, 5))
            };
        }

        private static BoardLesson[] Chess()
        {
            return new[]
            {
                new BoardLesson("白兵开局",
                    "前方无子时，兵从起始格可以直进两格。平时直进一格，吃子则斜进一格。试着把白兵从 e2 推到 e4。",
                    new ChessGame(), ChessPoint('e', 2), ChessPoint('e', 4)),
                new BoardLesson("走出将军",
                    "白王被黑车将军。把白王从 e1 移到 f1，避开黑车攻击的纵线。",
                    ChessGame.FromFen("k3r3/8/8/8/8/8/8/4K3 w - - 0 1"),
                    ChessPoint('e', 1), ChessPoint('f', 1)),
                new BoardLesson("王车易位",
                    "王和车都未移动且中间无子，王的起点、经过格和终点都不受攻击，就能易位。把白王移到 g1，车会自动移到王旁。",
                    ChessGame.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"),
                    ChessPoint('e', 1), ChessPoint('g', 1)),
                new BoardLesson("兵的升变",
                    "把白兵从 a7 推到 a8。到达对方底线时，兵必须升变为后、车、象或马，请任选一种。",
                    ChessGame.FromFen("4k3/P7/8/8/8/8/8/4K3 w - - 0 1"),
                    ChessPoint('a', 7), ChessPoint('a', 8))
            };
        }

        private static BoardLesson[] Checkers()
        {
            int from = ChineseCheckersGame.PointAt(13, -1);
            int firstHop = ChineseCheckersGame.PointAt(11, 1);
            int secondHop = ChineseCheckersGame.PointAt(9, 3);
            int thirdHop = ChineseCheckersGame.PointAt(7, 1);
            return new[]
            {
                new BoardLesson("走到相邻空孔",
                    "选中底部亮起的棋子，移到相邻空孔。走一步就结束本回合。",
                    new ChineseCheckersGame(2), from, ChineseCheckersGame.PointAt(12, -2)),
                new BoardLesson("转弯连跳",
                    "跨过相邻棋子，落到它后面的空孔。沿提示连跳三次，第三跳可以转弯，最后点「结束回合」。",
                    ChineseCheckersGame.CreateTutorial(), from, thirdHop, new[] { firstHop, secondHop, thirdHop }),
                new BoardLesson("退一跳",
                    "先沿提示跳两次，点「退一跳」回到第一孔，再跳回第二孔并结束回合。退一跳只撤销尚未提交的一段。",
                    ChineseCheckersGame.CreateTutorial(), from, secondHop, new[] { firstHop, secondHop }, undoAfterHop: 2),
                new BoardLesson("搬进对角营",
                    "把己方的 10 枚棋子全部移进对角的目标营即可获胜。棋子进入目标营后只能在营内移动。",
                    new ChineseCheckersGame(2))
            };
        }

        private static int XiangqiPoint(int row, int col) => row * 9 + col;
        private static int ChessPoint(char file, int rank) => (rank - 1) * 8 + file - 'a';
    }
}
