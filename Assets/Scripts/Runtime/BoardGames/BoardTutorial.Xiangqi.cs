using System;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal static partial class BoardTutorial
    {
        private static BoardLesson[] Xiangqi()
        {
            const string king = "3k5/9/9/9/p8/9/9/9/5K3/9 w";
            const string advisor = "3k5/9/9/9/p8/9/9/9/9/4KA3 w";
            const string elephant = "5k3/9/9/9/9/2B6/3R5/9/9/4K4 w";
            const string horse = "3k5/9/9/9/9/9/9/2N6/2R6/4K4 w";
            const string cannon = "4k4/9/9/9/4p4/9/4P4/1C2p2r1/9/4K4 w";
            const string crossedPawn = "3k5/9/9/9/4P4/9/9/9/9/5K3 w";
            return new[]
            {
                new BoardLesson("红方先走", "把中路红兵向前推一个交叉点。走完这手，就轮到黑方。",
                    new XiangqiGame(), 6 * 9 + 4, 5 * 9 + 4,
                    outcome: "红方已走一手。双方每次只走一枚棋子。"),
                XiangqiLesson("帅在九宫直走", "九宫是带斜线的区域。把右侧红帅向前直走一个交叉点。",
                    king, XiangqiTurn(8, 5, 7, 5)),
                XiangqiLesson("帅不能出九宫", "把红帅向右移一格，试一试能否走出九宫。",
                    king, XiangqiTurn(8, 5, 8, 6), BoardLessonKind.Blocked,
                    "帅和将都只能留在自己的九宫。"),
                XiangqiLesson("仕在九宫斜走", "把右侧红仕斜走一格，到九宫中央。",
                    advisor, XiangqiTurn(9, 5, 8, 4)),
                XiangqiLesson("仕不能出九宫", "试着把红仕向右上方斜走一格。",
                    advisor, XiangqiTurn(9, 5, 8, 6), BoardLessonKind.Blocked,
                    "走法是斜一格，但落点已经在九宫外。"),
                XiangqiLesson("相走田字", "红相往左下斜走两格。先看斜线正中的象眼是否空着。",
                    elephant, XiangqiTurn(5, 2, 7, 0)),
                XiangqiLesson("象眼被挡", "试着把红相往右下斜走两格。注意斜线正中有一枚红车。",
                    elephant, XiangqiTurn(5, 2, 7, 4), BoardLessonKind.Blocked,
                    "红车堵住象眼，这一步走不了。"),
                XiangqiLesson("相不能过河", "试着把红相向左上斜走两格，到河对岸。",
                    elephant, XiangqiTurn(5, 2, 3, 0), BoardLessonKind.Blocked,
                    "象眼虽然空着，相和象都只能留在本方半盘。"),
                XiangqiLesson("马走日字", "把红马走到左上方。它先直走一格，再向外斜出一格。",
                    horse, XiangqiTurn(7, 2, 5, 1)),
                XiangqiLesson("马腿被挡", "试着把红马走到左下方。看看红马正下方的第一格。",
                    horse, XiangqiTurn(7, 2, 9, 1), BoardLessonKind.Blocked,
                    "红车挡住马腿，红马无法朝这一侧起步。"),
                XiangqiLesson("车沿直线走", "让红车向右横走，吃掉同一横线上的黑车。",
                    "3k5/9/9/9/9/9/1R4r2/9/9/4K4 w", XiangqiTurn(6, 1, 6, 6),
                    outcome: "黑车被移除，红车占据原来黑车的交叉点。"),
                XiangqiLesson("车不能越子", "这次红车和黑车之间有一匹马。试着让红车吃黑车。",
                    "3k5/9/9/9/9/9/1R1N2r2/9/9/4K4 w", XiangqiTurn(6, 1, 6, 6), BoardLessonKind.Blocked,
                    "红车中途不能越过红马。"),
                XiangqiLesson("炮沿空线走", "让红炮向右走两格，停在黑卒前面的空点。",
                    cannon, XiangqiTurn(7, 1, 7, 3)),
                XiangqiLesson("无炮架不能吃子", "试着用红炮吃同一横线上的黑卒。看看炮与卒之间有几枚棋子。",
                    cannon, XiangqiTurn(7, 1, 7, 4), BoardLessonKind.Blocked,
                    "炮与黑卒之间没有棋子，无法吃卒。"),
                XiangqiLesson("隔子用炮", "用红炮吃同一横线上的黑车。中间的黑卒正好可以当炮架。",
                    cannon, XiangqiTurn(7, 1, 7, 7),
                    outcome: "黑车被吃，作为炮架的黑卒仍留在原处。"),
                XiangqiLesson("己方棋子也能当炮架", "炮和黑车之间换成了红马。仍然用红炮把黑车吃掉。",
                    "4k4/9/9/9/4p4/9/4P4/1C2N2r1/9/4K4 w", XiangqiTurn(7, 1, 7, 7),
                    outcome: "红马原地不动。两方的棋子都可以作炮架。"),
                XiangqiLesson("炮不能跳到空点", "试着让红炮越过黑卒，停到黑车左边的空点。",
                    cannon, XiangqiTurn(7, 1, 7, 6), BoardLessonKind.Blocked,
                    "炮只有吃子时才能越过一枚棋子。"),
                XiangqiLesson("两个炮架挡住炮", "红炮和黑车之间多了一匹马。试着用红炮吃黑车。",
                    "4k4/9/9/9/4p4/9/4P4/1C2pN1r1/9/4K4 w", XiangqiTurn(7, 1, 7, 7), BoardLessonKind.Blocked,
                    "炮吃子时，中间必须正好隔一枚棋子。"),
                XiangqiLesson("兵未过河只直进", "试着把仍在本方河岸的红兵向右横走一格。",
                    "3k5/9/9/9/9/4P4/9/9/9/5K3 w", XiangqiTurn(5, 4, 5, 5), BoardLessonKind.Blocked,
                    "红兵还没有过河，此时只能向前直走一格。"),
                XiangqiLesson("兵跨过河界", "让河边的红兵往前走一格，到对岸的交叉点。",
                    "3k5/9/9/9/9/4P4/9/9/9/5K3 w", XiangqiTurn(5, 4, 4, 4),
                    outcome: "红兵已过河，下一次走它时也可以横走一格。"),
                XiangqiLesson("过河走兵", "这枚红兵已过河。把它向右横走一格。",
                    crossedPawn, XiangqiTurn(4, 4, 4, 5),
                    outcome: "过河后的兵可以向前、向左或向右走一格。"),
                XiangqiLesson("兵不能后退", "试着把已经过河的红兵往后走一格。",
                    crossedPawn, XiangqiTurn(4, 4, 5, 4), BoardLessonKind.Blocked,
                    "兵过河后仍然不能后退。"),
                XiangqiLesson("将帅不能照面", "两位将帅在同一纵线。试着把挡在中间的红兵向右移开。",
                    "4k4/9/9/9/4P4/9/9/9/9/4K4 w", XiangqiTurn(4, 4, 4, 5), BoardLessonKind.Blocked,
                    "移开唯一挡子会让将帅照面，所以这手不合法。"),
                XiangqiLesson("移帅应将", "黑车沿中线将军。让红帅向右走一格，避开黑车的攻击。",
                    "3k5/9/9/9/4r4/9/9/9/9/4K4 w", XiangqiTurn(9, 4, 9, 5)),
                XiangqiLesson("吃子应将", "黑车沿中线将军。让左侧红车横走，吃掉这枚黑车。",
                    "3k5/9/9/9/R3r4/9/9/9/9/4K4 w", XiangqiTurn(4, 0, 4, 4)),
                XiangqiLesson("挡子应将", "黑车沿中线将军。让左侧红车横到中线，挡在黑车与红帅之间。",
                    "3k5/9/9/9/4r4/9/R8/9/9/4K4 w", XiangqiTurn(6, 0, 6, 4)),
                XiangqiLesson("将死", "把左侧红车横到中线将军，看看黑将还有没有应手。",
                    "4k4/R4R3/2N6/4P4/9/9/9/9/9/4K4 w", XiangqiTurn(1, 0, 1, 4),
                    outcome: "黑将被将军且无解，红胜。"),
                XiangqiLesson("困毙", "把右侧红车向前走一格，和左侧红车一起封锁黑将。",
                    "4k4/3R5/5R3/4P4/9/9/9/9/9/4K4 w", XiangqiTurn(2, 5, 1, 5),
                    outcome: "黑将没被将军，但黑方无合法着法，红胜。"),
                XiangqiRepeatLesson("循环和棋", "将帅已多次各走回原位。让黑将退回黑方底线。",
                    "3k5/9/9/9/p8/9/9/9/9/5K3 w",
                    "同一局面第 4 次出现，双方都没有长将或长捉，判和棋。",
                    XiangqiTurn(9, 5, 8, 5), XiangqiTurn(0, 3, 1, 3), XiangqiTurn(8, 5, 9, 5), XiangqiTurn(1, 3, 0, 3)),
                XiangqiRepeatLesson("长将判负", "红车已多次来回将军。把它移到中线，再将一次。",
                    "4k4/4R4/9/9/4P4/9/9/9/9/4K4 b",
                    "局面第 4 次出现，红方每手都将军，红负。",
                    XiangqiTurn(0, 4, 0, 3), XiangqiTurn(1, 4, 1, 3), XiangqiTurn(0, 3, 0, 4), XiangqiTurn(1, 3, 1, 4)),
                XiangqiRepeatLesson("长捉判负", "红车已反复威胁同一匹黑马。把红车退两格，再次追黑马。",
                    "3k5/9/9/9/9/9/9/2n4R1/9/5K3 b",
                    "局面第 4 次出现，红方长捉同一匹无保护的马，红负。",
                    XiangqiTurn(7, 2, 5, 3), XiangqiTurn(7, 7, 5, 7), XiangqiTurn(5, 3, 7, 2), XiangqiTurn(5, 7, 7, 7)),
                XiangqiLesson("自然限着和棋", "本局已连续 99 手没有吃子。让左侧红车向前走一格，仍然不吃子。",
                    "3k5/9/9/9/9/9/9/9/9/R4K3 w - - 99 50", XiangqiTurn(9, 0, 8, 0),
                    outcome: "无吃子的走棋达到 100 手，按自然限着判和。"),
                XiangqiLesson("只剩将帅", "让红帅向前吃掉最后一枚黑卒，看看双方只剩将帅时的结果。",
                    "3k5/9/9/9/9/9/9/9/5p3/5K3 w", XiangqiTurn(9, 5, 8, 5),
                    outcome: "双方只剩将帅，无法取胜，判和棋。")
            };
        }

        private static BoardMove XiangqiTurn(int row, int col, int toRow, int toCol) =>
            new BoardMove(row * 9 + col, toRow * 9 + toCol);

        private static BoardLesson XiangqiLesson(string title, string instruction, string fen, BoardMove turn,
            BoardLessonKind kind = BoardLessonKind.Move, string outcome = null) =>
            new BoardLesson(title, instruction, XiangqiGame.FromFen(fen), turn.From, turn.To, kind: kind, outcome: outcome);

        private static BoardLesson XiangqiRepeatLesson(string title, string instruction, string fen,
            string outcome, params BoardMove[] cycle)
        {
            var board = XiangqiGame.FromFen(fen);
            // Leave the last turn of the third cycle for the learner to trigger the adjudication.
            for (int i = 0; i < cycle.Length * 3 - 1; i++)
                if (!board.TryPlay(cycle[i % cycle.Length]))
                    throw new InvalidOperationException("Invalid Xiangqi tutorial history for " + title + " at ply " + (i + 1) + ".");
            BoardMove finalTurn = cycle[cycle.Length - 1];
            return new BoardLesson(title, instruction, board, finalTurn.From, finalTurn.To, outcome: outcome);
        }
    }
}
