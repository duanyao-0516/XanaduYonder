using System;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal enum BoardLessonKind { Move, Blocked, Inspect, Formation, FormationRestriction, Handoff, ClaimDraw, Pass }

    internal sealed class BoardLesson
    {
        private readonly int[] route;

        internal string Title { get; }
        internal string Instruction { get; }
        internal IBoardPosition Position { get; }
        internal int From { get; }
        internal int To { get; }
        internal int[] Route => route.Length == 0 ? route : (int[])route.Clone();
        internal BoardLessonKind Kind { get; }
        internal string Outcome { get; }
        internal bool RevealArmy { get; }
        internal bool ShowReplyView { get; }
        internal int Promotion { get; }
        internal bool UsesBoard => Kind == BoardLessonKind.Move || Kind == BoardLessonKind.Blocked;

        internal BoardLesson(string title, string instruction, IBoardPosition position,
            int from = -1, int to = -1, int[] route = null, BoardLessonKind kind = BoardLessonKind.Move,
            string outcome = null, bool revealArmy = false, bool showReplyView = false, int promotion = 0)
        {
            Title = title;
            Instruction = instruction;
            Position = position;
            From = from;
            To = to;
            this.route = route == null ? Array.Empty<int>() : (int[])route.Clone();
            Kind = kind == BoardLessonKind.Move && from < 0 ? BoardLessonKind.Inspect : kind;
            Outcome = outcome;
            RevealArmy = revealArmy;
            ShowReplyView = showReplyView;
            Promotion = promotion;
        }
    }

    internal static partial class BoardTutorial
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

        private static BoardLesson[] Checkers()
        {
            int from = ChineseCheckersGame.PointAt(13, -1);
            int step = ChineseCheckersGame.PointAt(12, -2);
            int firstHop = ChineseCheckersGame.PointAt(11, 1);
            int secondHop = ChineseCheckersGame.PointAt(9, 3);
            int thirdHop = ChineseCheckersGame.PointAt(7, 1);
            int entrance = ChineseCheckersGame.PointAt(3, -3);
            int approach = ChineseCheckersGame.PointAt(4, -4);
            int foreign = ChineseCheckersGame.PointAt(4, -8);
            int central = ChineseCheckersGame.PointAt(6, -6);
            int insideGoal = ChineseCheckersGame.PointAt(3, -1);
            var home = new ChineseCheckersGame(2);
            if (!home.TryPlay(new BoardMove(from, step)) || !home.TryPlay(new BoardMove(entrance, approach)))
                throw new InvalidOperationException("The Checkers home-camp lesson needs two legal opening moves.");

            return new[]
            {
                new BoardLesson("走到相邻空孔",
                    "选中亮起的棋子，点相邻的亮孔，再点同一孔确认。平走一格就结束本手。",
                    new ChineseCheckersGame(2), from, step, outcome: "相邻一格的平走已完成。下一手轮到对方。"),
                new BoardLesson("在本方营内停手",
                    "点选底部亮起的棋子，走到本方营内刚空出的相邻孔，再点落点确认。",
                    home, ChineseCheckersGame.PointAt(14, 0), from, outcome: "本方营内是可以停手的位置。"),
                new BoardLesson("转弯连跳",
                    "跨过相邻棋子，落到后面的空孔。依次连跳三次，再点最后一孔结束本手。",
                    ChineseCheckersGame.CreateTutorial(), from, thirdHop, new[] { firstHop, secondHop, thirdHop },
                    outcome: "三次跳跃合成一手。跳跃可以转弯，也可以跨过任一方的棋子。"),
                new BoardLesson("跨过第三方营区",
                    "从亮起的棋子向左跳进第三方营，再向右下跳回中央，最后点同一孔确认。",
                    ChineseCheckersGame.CreateTutorialCampCrossing(), approach, central, new[] { foreign, central },
                    outcome: "这手经过了第三方营，最后在中央合法停手。"),
                new BoardLesson("第三方营不能停手",
                    "选中亮起的棋子，试点左边金框里的孔，看看能不能把这一步停在第三方营。",
                    ChineseCheckersGame.CreateTutorialCampCrossing(), approach, foreign, kind: BoardLessonKind.Blocked,
                    outcome: "第三方营只允许经过。需要继续跳到中央、本方营或目标营才能停手。"),
                new BoardLesson("在目标营内移动",
                    "选中最上方营区下沿的亮子，向左挪进相邻空孔，再点一次确认。",
                    ChineseCheckersGame.CreateTutorialGoal(false), insideGoal, entrance, outcome: "目标营内可以调整棋子的位置。"),
                new BoardLesson("进了目标营不能离开",
                    "选中目标营下沿的亮子，试向右下方金框里的孔走一步。",
                    ChineseCheckersGame.CreateTutorialGoal(false), insideGoal, ChineseCheckersGame.PointAt(4, 0),
                    kind: BoardLessonKind.Blocked, outcome: "进入目标营后，棋子只能在营内走动。"),
                new BoardLesson("第十颗棋子进营",
                    "九颗棋子已经到目标营。把营外亮子送入入口的空孔并确认，观察胜负。",
                    ChineseCheckersGame.CreateTutorialGoal(false), approach, entrance,
                    outcome: "十颗己方棋子已全部进入对角目标营，本局获胜。"),
                new BoardLesson("目标营未满不能换位",
                    "目标营还有空孔。试着走到金框里已经被对方占据的位置。",
                    ChineseCheckersGame.CreateTutorialGoal(true, leaveAnotherHoleEmpty: true), approach, entrance,
                    kind: BoardLessonKind.Blocked, outcome: "目标营还有空孔，不能和堵在入口的对方棋子交换。"),
                new BoardLesson("末段换位",
                    "目标营已满。最后的空缺被对方原营棋子占着。点亮起的堵点并确认。",
                    ChineseCheckersGame.CreateTutorialGoal(true), approach, entrance,
                    outcome: "对方堵子交换到你的起点。目标营现在全是己方棋子，本局获胜。")
            };
        }
    }
}
