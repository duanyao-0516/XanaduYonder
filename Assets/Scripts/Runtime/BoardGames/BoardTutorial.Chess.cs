using System;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal static partial class BoardTutorial
    {
        private const string ChessCastlingPosition = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
        private const string ChessLonePawnPosition = "4k3/8/8/8/4P3/8/8/4K3 w - - 0 1";
        private const string ChessEnPassantPosition = "4k3/3p4/8/4P3/8/8/8/4K3 b - - 0 1";
        private const string ChessQuietRookPosition = "4k3/8/8/8/8/8/8/R3K3 w - - ";

        private static BoardLesson[] Chess()
        {
            return new[]
            {
                ChessAction("白兵开局",
                    "白方先走，双方轮流走一枚棋子。选 e2 的白兵，把它直推两格到 e4。兵从起始格直进两格时，前面两格都须为空。",
                    new ChessGame(), "e2e4"),
                ChessAction("兵直进一格",
                    "把 e4 的白兵推到 e5。离开起始格以后，只能直进一格。",
                    ChessGame.FromFen(ChessLonePawnPosition), "e4e5"),
                ChessAction("兵斜进吃子",
                    "白兵平时直进，吃子时斜进一格。让 e4 的白兵吃掉 d5 的黑兵。",
                    ChessGame.FromFen("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1"), "e4d5"),
                ChessAction("兵不能越子",
                    "e3 有黑兵挡路。试把白兵从 e2 推到 e4，看看为什么第一步也不能跳过棋子。",
                    ChessGame.FromFen("4k3/8/8/8/8/4p3/4P3/4K3 w - - 0 1"), "e2e4",
                    BoardLessonKind.Blocked, "前方的棋子挡住了白兵。e3 和 e4 都为空时才可直进两格。"),
                ChessAction("兵不能后退",
                    "试把 e4 的白兵退到 e3。对角吃子也只能朝对方底线。",
                    ChessGame.FromFen(ChessLonePawnPosition), "e4e3",
                    BoardLessonKind.Blocked, "兵始终不能后退。"),
                ChessAction("马跳过棋子",
                    "马走 L 形，可以越过棋子。让 g1 的白马跳过前方的白兵，吃掉 f3 的黑兵。",
                    ChessGame.FromFen("7k/7r/8/8/8/5p2/6P1/K5N1 w - - 0 1"), "g1f3"),
                ChessAction("不能落在己方棋子上",
                    "g1 到 e2 符合马的 L 形。选中白马，再试点白兵占据的 e2。",
                    new ChessGame(), "g1e2", BoardLessonKind.Blocked, "马可以越子，但落点不能有己方棋子。"),
                ChessAction("象沿斜线吃子",
                    "象沿斜线走任意格。将 c1 的白象沿空斜线移到 g5，吃掉黑兵。",
                    ChessGame.FromFen("7k/7r/8/6p1/8/8/8/K1B5 w - - 0 1"), "c1g5"),
                ChessAction("象的斜线被挡",
                    "试将 c1 的白象移到 h6。先看这一条斜线上的第一格。",
                    new ChessGame(), "c1h6", BoardLessonKind.Blocked, "d2 的白兵挡住斜线，象不能越过它。"),
                ChessAction("车沿直线吃子",
                    "车可以横走或竖走任意格。让 d4 的白车沿直线吃掉 d6 的黑兵。",
                    ChessGame.FromFen("7k/8/3p4/8/3R4/8/8/K7 w - - 0 1"), "d4d6"),
                ChessAction("车的直线被挡",
                    "试将 a1 的白车推到 a3。a2 上有一枚白兵。",
                    new ChessGame(), "a1a3", BoardLessonKind.Blocked, "车不能穿过 a2 的白兵。"),
                ChessAction("后的直线与斜线",
                    "后可以横、竖或斜走任意格。让 d4 的白后沿斜线吃掉 g7 的黑兵。",
                    ChessGame.FromFen("7k/6p1/8/8/3Q4/8/8/K7 w - - 0 1"), "d4g7"),
                ChessAction("后不能越子",
                    "试将 d1 的白后直走到 d3。中途的 d2 还停着白兵。",
                    new ChessGame(), "d1d3", BoardLessonKind.Blocked, "即使是后，也不能穿过途中棋子。"),
                ChessAction("王走一格",
                    "王可以横、竖或斜走一格。f5 的黑兵将军但没有保护，选 e4 的白王斜走一格吃掉它。",
                    ChessGame.FromFen("7k/7r/8/5p2/4K3/8/8/8 w - - 0 1"), "e4f5"),
                ChessAction("王不能走进将军",
                    "白王现在安全。试把 e1 的白王移到 f1，看看 f 线上的黑车控制了哪些格子。",
                    ChessGame.FromFen("k4r2/8/8/8/8/8/8/4K3 w - - 0 1"), "e1f1",
                    BoardLessonKind.Blocked, "f1 在黑车的攻击线上，白王不能走进去。"),
                ChessAction("走出将军",
                    "白王被 e 线上的黑车将军。把 e1 的白王移到 f1，离开攻击线。",
                    ChessGame.FromFen("k3r3/8/8/8/8/8/8/4K3 w - - 0 1"), "e1f1"),
                ChessAction("挡在王前的棋子",
                    "e2 的白车挡住黑车。试把白车横移到 d2，观察移开后白王是否安全。",
                    ChessGame.FromFen("k3r3/8/8/8/8/8/4R3/4K3 w - - 0 1"), "e2d2",
                    BoardLessonKind.Blocked, "移开白车会让己方王被将军，因此这步不能走。"),
                ChessAction("王车易位",
                    "短易位时，王向近侧的车走两格。把 e1 的白王移到 g1，观察 h1 的车怎样移到 f1。",
                    ChessGame.FromFen(ChessCastlingPosition), "e1g1",
                    outcome: "白王在 g1，白车越到王的另一侧 f1。"),
                ChessAction("长易位",
                    "长易位也让王向车走两格。把 e1 的白王移到 c1，观察 a1 的车怎样移到 d1。",
                    ChessGame.FromFen(ChessCastlingPosition), "e1c1",
                    outcome: "白王在 c1，白车越到王的另一侧 d1。"),
                ChessAction("被将军时不能易位",
                    "e1 的白王正被黑车将军。试从 e1 向 g1 做短易位。",
                    ChessGame.FromFen("k3r3/8/8/8/8/8/8/4K2R w K - 0 1"), "e1g1",
                    BoardLessonKind.Blocked, "王的起点正在受攻击。应先用其他走法解围。"),
                ChessAction("易位不能经过受攻击格",
                    "黑车攻击 f1，没攻击 e1 或 g1。试从 e1 向 g1 做短易位。",
                    ChessGame.FromFen("k4r2/8/8/8/8/8/8/4K2R w K - 0 1"), "e1g1",
                    BoardLessonKind.Blocked, "短易位时王要经过 f1。经过格受攻击，易位也不成立。"),
                ChessAction("易位终点必须安全",
                    "黑车攻击 g1，没攻击 e1 或 f1。试从 e1 向 g1 做短易位。",
                    ChessGame.FromFen("k5r1/8/8/8/8/8/8/4K2R w K - 0 1"), "e1g1",
                    BoardLessonKind.Blocked, "易位后白王会落在受攻击的 g1，这是非法走法。"),
                ChessAction("易位中间不能有子",
                    "e1 的白王和 h1 的白车都没动过，但 f1 有白象。试从 e1 向 g1 做短易位。",
                    ChessGame.FromFen("4k3/8/8/8/8/8/8/4KB1R w K - 0 1"), "e1g1",
                    BoardLessonKind.Blocked, "王和车之间有子，清空中间格后才可能易位。"),
                ChessAction("王走过就不能易位",
                    "这个局面里白王曾从 e1 走到 e2，再回到 e1。现在试从 e1 向 c1 做长易位。",
                    ChessAfter(ChessGame.FromFen(ChessCastlingPosition), "e1e2", "e8e7", "e2e1", "e7e8"), "e1c1",
                    BoardLessonKind.Blocked, "王一旦走过，两边都失去易位权，回原位也不会恢复。"),
                ChessAction("车走过就不能与它易位",
                    "h1 的白车曾走到 h2 再回来，白王没有走过。试从 e1 向 g1 做短易位。",
                    ChessAfter(ChessGame.FromFen(ChessCastlingPosition), "h1h2", "e8e7", "h2h1", "e7e8"), "e1g1",
                    BoardLessonKind.Blocked, "h1 的白车已失去易位权，没走过的 a1 白车仍可参与长易位。"),
                ChessAction("吃过路兵",
                    "黑兵刚从 d7 直进两格到 d5。让 e5 的白兵走到它经过的 d6，吃掉仍停在 d5 的黑兵。",
                    ChessAfter(ChessGame.FromFen(ChessEnPassantPosition), "d7d5"), "e5d6",
                    outcome: "白兵到了 d6，d5 的黑兵被移除。"),
                ChessAction("过路兵机会会过期",
                    "黑兵从 d7 到 d5 后，白王和黑王各走了一步。试把 e5 的白兵斜走到空着的 d6。",
                    ChessAfter(ChessGame.FromFen(ChessEnPassantPosition), "d7d5", "e1f1", "e8f8"), "e5d6",
                    BoardLessonKind.Blocked, "吃过路兵只能在对方兵直进两格后的紧接一手使用。"),
                ChessAction("吃过路兵也要保护王",
                    "黑兵刚到 f5。试让 g5 的白兵吃过路兵到 f6，留意 a5 的黑车与 h5 的白王。",
                    ChessGame.FromFen("k7/8/8/r4pPK/8/8/8/8 w - f6 0 1"), "g5f6",
                    BoardLessonKind.Blocked, "两枚兵都会离开第 5 行，黑车将攻击白王，所以这次不能吃过路兵。"),
                ChessPromotion("兵的升变", "后", ChessGame.Queen, "白兵抵达底线，并换成了白后。"),
                ChessPromotion("升变为车", "车", ChessGame.Rook, "白兵换成了白车，之后按车的方式横竖移动。"),
                ChessPromotion("升变为象", "象", ChessGame.Bishop, "白兵换成了白象，之后可以沿斜线移动。"),
                ChessPromotion("升变为马", "马", ChessGame.Knight, "白兵换成了白马，之后按 L 形移动。"),
                ChessAction("将死",
                    "将死对方就能获胜。让 g6 的白后走到 g7，在白王保护下攻击黑王。",
                    ChessGame.FromFen("7k/8/5KQ1/8/8/8/8/8 w - - 0 1"), "g6g7",
                    outcome: "黑王被将军，既吃不掉白后，也没有安全格可走。白方获胜。"),
                ChessAction("逼和",
                    "把 g5 的白后移到 g6。观察 h8 的黑王没有被攻击，但是否还有合法走法。",
                    ChessGame.FromFen("7k/5K2/8/6Q1/8/8/8/8 w - - 0 1"), "g5g6",
                    outcome: "黑王没被将军，却无合法着法，这叫逼和。"),
                new BoardLesson("死局",
                    "白方只剩王和一匹马，黑方只剩王。任意合法走法都不能使一方将死另一方。",
                    ChessGame.FromFen("7k/8/5K2/8/3N4/8/8/8 w - - 0 1"), kind: BoardLessonKind.Inspect,
                    outcome: "双方都不可能造成将死，死局立即判和。"),
                new BoardLesson("三次重复申请和棋",
                    "两边的马各往返两次，开局局面第三次出现，且仍轮到白方。点「申请和棋」。",
                    ChessKnightCycle(8), kind: BoardLessonKind.ClaimDraw,
                    outcome: "局面已经出现三次。申请成立，这一局判和。"),
                new BoardLesson("下一手会第三次重复",
                    "黑马从 f6 回 g8 就会让开局局面第三次出现。先点「申请和棋」，黑马留在原格。",
                    ChessKnightCycle(7), kind: BoardLessonKind.ClaimDraw,
                    outcome: "可以在达到三次重复的着法落子前申请。黑马仍在 f6。"),
                new BoardLesson("五十回合申请和棋",
                    "双方各走了连续 50 手没有动兵、也没有吃子。点「申请和棋」。",
                    ChessGame.FromFen(ChessQuietRookPosition + "100 100"), kind: BoardLessonKind.ClaimDraw,
                    outcome: "连续五十回合无动兵或吃子，申请和棋成立。"),
                new BoardLesson("下一手会满五十回合",
                    "双方合计已走 99 手没有动兵或吃子。若白车从 a1 到 a2，就会达到五十回合。先申请和棋。",
                    ChessGame.FromFen(ChessQuietRookPosition + "99 100"), kind: BoardLessonKind.ClaimDraw,
                    outcome: "达到五十回合的下一手可以作为申请依据。白车仍在 a1。"),
                ChessAction("五次重复自动和棋",
                    "开局局面已经出现四次。把 f6 的黑马移回 g8，看看是否还要申请和棋。",
                    ChessKnightCycle(15), "f6g8",
                    outcome: "同一局面第五次出现，自动判和。"),
                ChessAction("七十五回合自动和棋",
                    "双方合计已走 149 手没有动兵或吃子。把白车从 a1 移到 a2，完成第 75 回合。",
                    ChessGame.FromFen(ChessQuietRookPosition + "149 100"), "a1a2",
                    outcome: "连续七十五回合没有动兵或吃子，自动判和。")
            };
        }

        private static BoardLesson ChessAction(string title, string instruction, ChessGame position, string coordinates,
            BoardLessonKind kind = BoardLessonKind.Move, string outcome = null)
        {
            BoardMove move = ChessLessonMove(coordinates);
            return new BoardLesson(title, instruction, position, move.From, move.To, kind: kind, outcome: outcome);
        }

        private static BoardLesson ChessPromotion(string title, string piece, int kind, string outcome) =>
            new BoardLesson(title, "把 a7 的白兵推到 a8 并确认。兵抵达对方底线必须升变，本节请选择「" + piece + "」。",
                ChessGame.FromFen("4k3/P3p3/8/8/8/8/8/4K3 w - - 0 1"), 48, 56, outcome: outcome, promotion: kind);

        private static ChessGame ChessAfter(ChessGame position, params string[] moves)
        {
            foreach (string coordinates in moves)
            {
                if (!position.TryPlay(ChessLessonMove(coordinates)))
                    throw new InvalidOperationException("Invalid chess tutorial setup move: " + coordinates);
            }
            return position;
        }

        private static ChessGame ChessKnightCycle(int plies)
        {
            var position = new ChessGame();
            string[] cycle = { "g1f3", "g8f6", "f3g1", "f6g8" };
            for (int i = 0; i < plies; i++)
                ChessAfter(position, cycle[i % cycle.Length]);
            return position;
        }

        private static BoardMove ChessLessonMove(string coordinates)
        {
            int from = (coordinates[1] - '1') * 8 + coordinates[0] - 'a';
            int to = (coordinates[3] - '1') * 8 + coordinates[2] - 'a';
            return new BoardMove(from, to);
        }
    }
}
