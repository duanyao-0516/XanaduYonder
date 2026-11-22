using System;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal static partial class BoardTutorial
    {
        private static BoardLesson[] Army()
        {
            const int redLeftFront = 30, redLeftCamp = 36;
            const int greenLeftFront = 25, redMiddleFront = 32, greenMiddleFront = 27;
            var opening = new ArmyGame(ArmyGame.CreateFormation(121), ArmyGame.CreateFormation(245));
            return new[]
            {
                new BoardLesson("秘密布阵",
                    "练习使用推荐或随机阵型。点两枚己方棋子试着换位。",
                    opening.Copy(), kind: BoardLessonKind.Formation,
                    outcome: "每方 25 枚，行营留空。军旗放大本营，地雷放后两排，炸弹避开前排。"),
                ArmyFormationRule("军旗只能在大本营", "试把军旗和前排亮点的棋子交换。", ArmyPieceKind.Flag,
                    "军旗离开大本营时，阵型不合法，交换没有发生。"),
                ArmyFormationRule("地雷只能放后两排", "试把地雷换到前排标记位置。", ArmyPieceKind.Mine,
                    "地雷必须放在最后两排，刚才的交换被拒绝。"),
                ArmyFormationRule("炸弹避开首排", "试把炸弹换到首排标记位置。", ArmyPieceKind.Bomb,
                    "炸弹不能布在第一排，交换没有发生。"),
                ArmyMove("公路走一站",
                    "细线是公路。沿细线把红棋向前移到相邻车站。",
                    ArmyExample(Red(7, 2, ArmyPieceKind.Regiment)), ArmyPoint(7, 2), redMiddleFront,
                    "矩形车站可以停棋，公路一次只走一站。"),
                ArmyBlocked("公路不能连走",
                    "试着从起点沿公路一次走到远处的标记站。",
                    ArmyExample(Red(7, 2, ArmyPieceKind.Regiment)), ArmyPoint(7, 2), ArmyPoint(10, 2),
                    "这条路不是一段连续的铁路，只能先到相邻点。"),
                ArmyMove("斜走进入行营",
                    "把左侧前排红棋沿斜线移入圆形行营。",
                    opening.Copy(), redLeftFront, redLeftCamp,
                    "接入行营的斜线也是公路。红方先走，对方棋子以暗子显示。", revealArmy: false),
                ArmyMove("走出行营",
                    "轮到红方，把行营里的红棋斜移回左侧空车站。",
                    ArmyExample(Red(7, 1, ArmyPieceKind.Battalion)), redLeftCamp, redLeftFront,
                    "行营保护驻守棋子，己方仍可在自己的回合走出。"),
                ArmyBlocked("行营不能被攻击",
                    "从左侧前排试着撞向行营里的绿棋。",
                    ArmyExample(Red(6, 0, ArmyPieceKind.Regiment), Green(7, 1, ArmyPieceKind.Engineer)), redLeftFront, redLeftCamp,
                    "行营内的绿棋受保护，即使红棋军阶更高也不能攻击。"),
                ArmyMove("铁路直走多站",
                    "沿左侧有短纹的粗线，把红棋从后排直走到前排。",
                    ArmyExample(Red(10, 0, ArmyPieceKind.General)), ArmyPoint(10, 0), redLeftFront,
                    "铁路沿同一直线畅通时，可以连续经过多个空站。"),
                ArmyMove("从中路过界",
                    "选红方前排中间的棋子，沿铁路走到对岸的空站。",
                    ArmyExample(Red(6, 2, ArmyPieceKind.Platoon)), redMiddleFront, greenMiddleFront,
                    "中间隔带没有停靠站。只有左、中、右三条铁路连接两岸。"),
                ArmyBlocked("隔带不能横越",
                    "从第二路的红棋试着直接走到对岸正对的空站。",
                    ArmyExample(Red(6, 1, ArmyPieceKind.Platoon)), ArmyPoint(6, 1), ArmyPoint(5, 1),
                    "第二路隔带没有连线。要过界，先走到有铁路的路口。"),
                ArmyBlocked("铁路不能越子",
                    "从左侧后排选红军长，试着越过同列红棋到前排。",
                    ArmyExample(Red(10, 0, ArmyPieceKind.General), Red(8, 0, ArmyPieceKind.Platoon)), ArmyPoint(10, 0), redLeftFront,
                    "铁路遇到棋子就停。可攻击挡在前方的敌棋，不能越过任何棋子，也不能落在己方棋子上。"),
                ArmyBlocked("普通棋子不能转弯",
                    "选红连长，试着沿铁路拐过左侧路口到对岸。",
                    ArmyExample(Red(6, 1, ArmyPieceKind.Company)), ArmyPoint(6, 1), greenLeftFront,
                    "司令到排长走铁路都只能直走，炸弹也不能转弯。"),
                ArmyMove("工兵可以转弯",
                    "换成工兵，沿铁路拐过左侧路口到达对岸。",
                    ArmyExample(Red(6, 1, ArmyPieceKind.Engineer)), ArmyPoint(6, 1), greenLeftFront,
                    "工兵能沿畅通的铁路转弯，但同样不能越过棋子。"),
                ArmyMove("进入大本营",
                    "把红团长走进底端较深的空大本营。",
                    ArmyExample(Red(10, 3, ArmyPieceKind.Regiment)), ArmyPoint(10, 3), ArmyPoint(11, 3),
                    "可以进入空的大本营，进去的棋子从此不能再移动。"),
                ArmyBlocked("大本营不能走出",
                    "试着把已经进入大本营的红团长移到上方空站。",
                    ArmyExample(Red(11, 3, ArmyPieceKind.Regiment)), ArmyPoint(11, 3), ArmyPoint(10, 3),
                    "团长被留在大本营。任何占据大本营的棋子都不能离开。"),
                ArmyBlocked("地雷不能移动",
                    "试着把红方后排的地雷移到前面的空行营。",
                    ArmyExample(Red(10, 1, ArmyPieceKind.Mine)), ArmyPoint(10, 1), ArmyPoint(9, 1),
                    "地雷留在布阵的位置，靠对方碰子触发。"),
                ArmyBlocked("军旗不能移动",
                    "试着把红方的大本营军旗移到前面的空站。",
                    ArmyExample(), ArmyPoint(11, 1), ArmyPoint(10, 1),
                    "军旗始终留在大本营，由其他棋子保护。"),
                ArmyFight("大棋吃小棋", "让红团长碰对岸的绿营长。",
                    ArmyPieceKind.Regiment, ArmyPieceKind.Battalion,
                    "团长留下。军阶依次是司令、军长、师长、旅长、团长、营长、连长、排长、工兵。"),
                ArmyFight("小棋碰上大棋", "让红旅长碰对岸的绿师长，看看哪枚棋子留下。",
                    ArmyPieceKind.Brigade, ArmyPieceKind.Division,
                    "师长大于旅长。进攻方的旅长离场，师长留在原位。"),
                ArmyFight("同级一起移除", "让红连长碰对岸的绿连长。",
                    ArmyPieceKind.Company, ArmyPieceKind.Company,
                    "两枚棋子军阶相同，一起移除。"),
                ArmyFight("靠军阶取胜", "让红军长碰对岸的绿旅长。",
                    ArmyPieceKind.General, ArmyPieceKind.Brigade,
                    "军长大于旅长。普通棋子走法相同，碰子依军阶裁决。"),
                ArmyFight("工兵的军阶", "让红排长碰对岸的绿工兵。",
                    ArmyPieceKind.Platoon, ArmyPieceKind.Engineer,
                    "工兵能转弯和排雷，和普通棋子碰子时仍是最低军阶。"),
                ArmyFight("司令阵亡亮旗", "让红司令撞上对岸的绿炸弹。留意红旗的位置。",
                    ArmyPieceKind.Marshal, ArmyPieceKind.Bomb,
                    "现在是绿方视角。红方司令已离场，红旗公开，其余红棋仍扣着。", revealArmy: false, showReplyView: true),
                ArmyMove("工兵排雷",
                    "让红工兵走到绿方地雷所在的位置。",
                    ArmyExample(Red(2, 0, ArmyPieceKind.Engineer), Green(1, 0, ArmyPieceKind.Mine)), ArmyPoint(2, 0), ArmyPoint(1, 0),
                    "工兵排掉地雷并留下。地雷本身不能移动。"),
                ArmyMove("普通棋子碰雷",
                    "这次让红师长走到绿地雷所在的位置。",
                    ArmyExample(Red(2, 0, ArmyPieceKind.Division), Green(1, 0, ArmyPieceKind.Mine)), ArmyPoint(2, 0), ArmyPoint(1, 0),
                    "师长离场，地雷留在原位。军阶再高也不能代替工兵排雷。"),
                ArmyMove("炸弹遇上地雷",
                    "让红炸弹走到绿地雷所在的位置。",
                    ArmyExample(Red(2, 0, ArmyPieceKind.Bomb), Green(1, 0, ArmyPieceKind.Mine)), ArmyPoint(2, 0), ArmyPoint(1, 0),
                    "炸弹与地雷一起移除。炸弹每次碰子都会和对方棋子一起离场。"),
                ArmyMove("普通棋子也能夺旗",
                    "把已到对岸底线附近的红排长走到绿军旗的位置。",
                    ArmyExample(Red(1, 1, ArmyPieceKind.Platoon)), ArmyPoint(1, 1), ArmyPoint(0, 1),
                    "只要能合法走到军旗所在位置，任何可移动的棋子都能夺旗获胜。"),
                ArmyMove("炸弹碰军旗",
                    "把红炸弹走到绿军旗的位置。",
                    ArmyExample(Red(1, 1, ArmyPieceKind.Bomb)), ArmyPoint(1, 1), ArmyPoint(0, 1),
                    "炸弹和军旗一起移除。红方夺旗获胜。"),
                ArmyMove("对手无棋可走",
                    "让红团长吃掉绿方最后还能动的连长。",
                    ArmyGame.CreateTutorialPosition(Red(6, 0, ArmyPieceKind.Regiment), Green(5, 0, ArmyPieceKind.Company),
                        Green(1, 0, ArmyPieceKind.Mine), Green(0, 3, ArmyPieceKind.General)), redLeftFront, greenLeftFront,
                    "绿方只剩地雷、军旗和困在大本营的军长，轮到绿方时无棋可走，红方获胜。"),
                ArmyMove("第 60 手未碰子",
                    "前 59 手都没有碰子。把绿棋从行营移回前排空站。",
                    ArmyQuietDraw(opening), ArmyPoint(4, 1), ArmyPoint(5, 1),
                    "双方连续 60 手未碰子，判和棋。每走一方算一手。"),
                new BoardLesson("同机交接",
                    "同机双人每走一手都遮住棋盘。把设备交给下一位，再确认接手。",
                    opening.Copy(), kind: BoardLessonKind.Handoff,
                    outcome: "接手后只显示当前玩家的棋子身份。关闭「单击落子」时，点同一个落点两次确认。")
            };
        }

        private static BoardLesson ArmyMove(string title, string instruction, IBoardPosition position,
            int from, int to, string outcome, bool revealArmy = true, bool showReplyView = false) =>
            new BoardLesson(title, instruction, position, from, to, kind: BoardLessonKind.Move, outcome: outcome,
                revealArmy: revealArmy, showReplyView: showReplyView);

        private static BoardLesson ArmyBlocked(string title, string instruction, ArmyGame position,
            int from, int to, string outcome) =>
            new BoardLesson(title, instruction, position, from, to, kind: BoardLessonKind.Blocked, outcome: outcome, revealArmy: true);

        private static BoardLesson ArmyFight(string title, string instruction, ArmyPieceKind attacker,
            ArmyPieceKind defender, string outcome, bool revealArmy = true, bool showReplyView = false) =>
            ArmyMove(title, instruction, ArmyExample(Red(6, 0, attacker), Green(5, 0, defender)), 30, 25, outcome, revealArmy, showReplyView);

        private static BoardLesson ArmyFormationRule(string title, string instruction, ArmyPieceKind rank, string outcome)
        {
            int[] red = ArmyGame.CreateFormation(1709);
            int from = ArmyGame.FormationPoint(0, Array.IndexOf(red, (int)rank));
            return new BoardLesson(title, instruction, new ArmyGame(red, ArmyGame.CreateFormation(2619)), from, ArmyGame.FormationPoint(0, 0),
                kind: BoardLessonKind.FormationRestriction, outcome: outcome);
        }

        private static ArmyGame ArmyExample(params (int Point, int Side, ArmyPieceKind Kind)[] pieces)
        {
            // Keep an out-of-the-way movable piece for each side so ordinary examples do not end the match.
            var board = new (int Point, int Side, ArmyPieceKind Kind)[pieces.Length + 2];
            Array.Copy(pieces, board, pieces.Length);
            board[pieces.Length] = Red(8, 4, ArmyPieceKind.Platoon);
            board[pieces.Length + 1] = Green(3, 4, ArmyPieceKind.Platoon);
            return ArmyGame.CreateTutorialPosition(board);
        }

        private static ArmyGame ArmyQuietDraw(ArmyGame opening)
        {
            var position = (ArmyGame)opening.Copy();
            var cycle = new[] { new BoardMove(31, 36), new BoardMove(26, 21), new BoardMove(36, 31), new BoardMove(21, 26) };
            for (int turn = 0; turn < ArmyGame.QuietPlyLimit - 1; turn++)
                if (!position.TryPlay(cycle[turn % cycle.Length]))
                    throw new InvalidOperationException("Could not replay the quiet Junqi tutorial turns.");
            return position;
        }

        private static int ArmyPoint(int row, int col) => row * 5 + col;
        private static (int Point, int Side, ArmyPieceKind Kind) Red(int row, int col, ArmyPieceKind kind) => (ArmyPoint(row, col), 0, kind);
        private static (int Point, int Side, ArmyPieceKind Kind) Green(int row, int col, ArmyPieceKind kind) => (ArmyPoint(row, col), 1, kind);
    }
}
