using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage
    {
        private const int RulesBodySize = 32;
        private const int RulesHeadingSize = 35;

        private sealed class RulesSection
        {
            internal readonly string Heading;
            internal readonly string[] Paragraphs;

            internal RulesSection(string heading, params string[] paragraphs)
            {
                Heading = heading;
                Paragraphs = paragraphs;
            }
        }

        private static readonly RulesSection[] ArmyRules =
        {
            new RulesSection("如何开始",
                "先秘密布阵。初玩可直接用推荐阵型，也可选随机布阵，或点两枚己方棋子交换。每方 25 枚棋子，开局行营留空。军旗只放在大本营，地雷只放在最后两排，炸弹避开最前排。",
                "红方先走。点己方棋子，再点亮起的落点。关闭「单击落子」时，再点同一个落点确认。",
                "你能看见己方棋子的身份，对方棋子以暗子显示。同机双人每走一手都会遮住棋盘，交给下一位再确认接手。司令阵亡时，该方军旗的位置向双方公开。"),
            new RulesSection("棋盘与走法",
                "矩形车站是可以停棋的位置。细线是公路，每次沿线走一站，接入行营的斜线也可以走。",
                "有短纹的粗线是铁路，可以沿直线连续走几站，遇到棋子就停。可攻击挡在前方的敌棋，不能越过任何棋子。工兵可以沿畅通的铁路转弯。",
                "圆形行营是安全点，其中的棋子受保护，仍可在自己的回合走出。两端颜色较深的矩形是大本营，棋子进去后不能再移动。地雷和军旗始终不能移动。",
                "两方前排之间留有中间隔带，没有可停的站。过到对岸要走左、中、右三条相连的铁路。"),
            new RulesSection("军阶与碰子",
                "普通棋子从大到小依次是司令、军长、师长、旅长、团长、营长、连长、排长、工兵。司令到排长的走法相同，区别在军阶。司令阵亡会亮旗，工兵能在铁路上转弯和排雷。",
                "棋子走到敌方占据的位置就会碰子。大吃小，同级一起移除。例如团长遇到营长，团长留下。",
                "工兵可以排雷，其他普通棋子碰雷会被移除。炸弹和它碰到的棋子一起移除，包括地雷和军旗。能合法走到军旗所在位置的棋子都能夺旗。"),
            new RulesSection("胜负与和棋",
                "夺取对方军旗，或让对方轮到行棋时无子可走，即获胜。双方连续 60 手没有碰子，判和棋。每走一方算一手。")
        };

        private static readonly RulesSection[] XiangqiRules =
        {
            new RulesSection("如何开始",
                "红方先走，双方轮流走一枚棋子。棋子落在交叉点上。棋盘中间是河界，两端有斜线的小方框是九宫。",
                "点己方棋子，再点亮起的交叉点。关闭「单击落子」时，再点同一个落点确认。被将军时，先找能让己方将帅脱险的着法。"),
            new RulesSection("行路与棋谱",
                "红方在棋盘下方时，纵路从左到右用数字 1 到 9，横行从红方底线向上用汉字一到十。棋盘翻转时，坐标仍对应同一个交叉点。",
                "棋谱先记路、再记行。图中的落点记作 2路九行。可以在设置中开启棋盘坐标。"),
            new RulesSection("棋子怎么走",
                "帅（将）在九宫内直走一格。\n仕（士）在九宫内斜走一格。\n相（象）斜走两格成「田」字，不能过河，中间的象眼有子就走不了。\n马先直走一格，再斜出一格成「日」字。第一格是马腿，有子就走不了。\n车横竖走任意格，中途不能越子。\n炮不吃子时像车，沿空线直走。吃子时与目标之间必须刚好隔一枚棋子，双方的棋子都能当炮架。\n兵（卒）过河前只能直进一格。过河后也可横走一格，始终不能后退。"),
            new RulesSection("特殊规则",
                "将帅不能在同一条纵线上无遮挡地相对。因此，两位将帅之间若只隔一枚子，不能把它移开让将帅照面。",
                "例如，炮、任意一枚棋子、敌车排在同一横线上，且炮与车之间只有这枚棋子，炮就能隔子吃车。若中间没有棋子，炮可以走到空点。",
                "对方棋子攻击到你的将帅时，你就被将军。可移动将帅、吃掉进攻棋子，或在允许时用棋子挡住攻击。每一步走完都必须保证己方将帅安全。"),
            new RulesSection("胜负与和棋",
                "被将军且无解是将死。没有被将军但也无合法着法是困毙。在象棋中，这两种情况都判负。",
                "长将是循环中每手都将军，长捉通常是连续威胁同一枚敌棋。同一局面第 4 次出现时，本局会按双方责任裁决，责任相同则和棋。",
                "达到自然限着（连续 50 回合无吃子，并按规则计算将军的步数），或双方只剩将帅，也判和棋。")
        };

        private static readonly RulesSection[] ChessRules =
        {
            new RulesSection("如何开始",
                "白方先走，双方轮流走一枚棋子。目标是将死对方的王。棋盘横向用 a 到 h 标记，纵向用 1 到 8 标记。例如，白王前面的兵起点是 e2。",
                "点己方棋子，再点亮起的格子。关闭「单击落子」时，再点同一个落点确认。走完后，己方王必须安全。"),
            new RulesSection("棋子怎么走",
                "王横、竖、斜都可走一格，不能走到受攻击的格子。\n后横、竖、斜都可走任意格。\n车横或竖走任意格。\n象斜走任意格。\n马沿任一方向走两格，再垂直拐一格，成 L 形，可以跳过其他棋子。\n兵向前走一格，第一次走且前面两格均为空时可直进两格。吃子时斜进一格，不能后退。",
                "后、车、象中途都不能越子，其他棋子也不能落到己方占据的格子。可先试试把白兵从 e2 推到 e4。"),
            new RulesSection("特殊规则",
                "王车易位要求王和对应的车都没走过，中间没有棋子，王的起点、经过格和终点都没有受到攻击。易位时，王向车走两格，车越到王的另一边。",
                "吃过路兵只发生在对方兵刚直进两格，停在你的兵旁边时。你可以立刻斜进到它刚经过的空格并吃掉它。例如，白兵在 e5，黑兵刚从 d7 走到 d5，白兵可以在紧接的一手走到 d6，吃掉 d5 的黑兵。",
                "兵到达对方底线时，必须选择升变为后、车、象或马。"),
            new RulesSection("胜负与和棋",
                "王被攻击且没有合法着法可解围，是将死，进攻方获胜。王没被攻击却无合法着法，是逼和。双方都不可能将死对方时，是死局，也判和棋。",
                "同一局面出现 3 次，或连续 50 回合未动兵也未吃子，可在设置申请和棋。若一个合法的下一手会达到条件，也可以在走棋前申请。局面出现 5 次或达到 75 回合时自动和棋。")
        };

        private static readonly RulesSection[] CheckersRules =
        {
            new RulesSection("如何开始",
                "各方轮流移动棋珠，先让己方十颗棋珠全部进入对角目标营就获胜。点棋珠，再点落点。关闭「单击落子」时，可以再点同一落点确认。"),
            new RulesSection("平走与连跳",
                "每手可走到一个相邻空孔，也可连续跳过相邻棋珠。每次跳跃要落到被跳棋珠后面的空孔，可以转弯。经过第三方营区时不能停，最后要在本方营区、中央或目标营停手。"),
            new RulesSection("目标营与换位",
                "进入目标营的棋珠只能在营内移动。目标营满且剩余堵点由对方原营棋子占据时，允许把合法的末段落点与起点换位。目标营仍有空孔时不能换位。")
        };

        private void DrawRules(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "玩法说明");
            const float side = 28;
            const float top = 106;
            float areaWidth = width - 2 * side;
            float areaHeight = Mathf.Max(1, height - top - 122);
            const float scrollGutter = 24;
            float textWidth = areaWidth - scrollGutter;
            var scrollArea = UiFactory.Rect(sheet, "Board rules scroll", side, top, areaWidth, areaHeight);
            var viewport = UiFactory.Rect(scrollArea, "Board rules viewport", 0, 0, textWidth, areaHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var dragSurface = viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
            var content = UiFactory.Rect(viewport, "Board rules content", 0, 0, textWidth, 0);
            float contentHeight = DrawRuleSections(content, textWidth, RulesFor(game), game == BoardGameId.Xiangqi);
            content.sizeDelta = new Vector2(textWidth, Mathf.Max(areaHeight, contentHeight));

            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 46;
            if (contentHeight > areaHeight)
                scroll.verticalScrollbar = DrawRulesScrollbar(scrollArea, textWidth + 13, areaHeight);
            scroll.verticalNormalizedPosition = 1;

            GameButton(sheet, "返回", 26, height - 105, width - 52, 79, ClosePanel);
        }

        private static RulesSection[] RulesFor(BoardGameId game)
        {
            switch (game)
            {
                case BoardGameId.Army: return ArmyRules;
                case BoardGameId.Xiangqi: return XiangqiRules;
                case BoardGameId.Chess: return ChessRules;
                default: return CheckersRules;
            }
        }

        private static float DrawRuleSections(Transform content, float width, RulesSection[] sections, bool showCoordinatesDiagram)
        {
            float y = 0;
            foreach (var section in sections)
            {
                if (y > 0)
                {
                    UiFactory.Panel(content, "Rules section divider", 0, y, width, 1, PaperLine, false);
                    y += 23;
                }
                if (!string.IsNullOrEmpty(section.Heading))
                    y += RulesLabel(content, section.Heading, y, width, RulesHeadingSize, UiFactory.Accent, FontStyle.Bold) + 10;
                foreach (string paragraph in section.Paragraphs)
                    y += RulesLabel(content, paragraph, y, width, RulesBodySize, UiFactory.Ink, FontStyle.Normal) + 17;
                if (showCoordinatesDiagram && ReferenceEquals(section, XiangqiRules[1]))
                    y += DrawXiangqiCoordinates(content, width, y) + 17;
            }
            return y;
        }

        private static float DrawXiangqiCoordinates(Transform content, float width, float y)
        {
            const float height = 194;
            const float pitch = 70;
            const float firstFile = 105;
            const float firstRank = 63;
            const float rankPitch = 47;
            var card = UiFactory.Panel(content, "Xiangqi coordinate diagram", 0, y, width, height, MutedCard).transform;
            for (int index = 0; index < 3; index++)
            {
                float x = firstFile + index * pitch;
                float row = firstRank + index * rankPitch;
                UiFactory.Panel(card, "Coordinate file line", x - 1, firstRank, 2, rankPitch * 2, UiFactory.Muted, false);
                UiFactory.Panel(card, "Coordinate rank line", firstFile, row - 1, pitch * 2, 2, UiFactory.Muted, false);
                UiFactory.Label(card, (index + 1) + "路", x - 30, 11, 60, 35, 25, UiFactory.Ink, TextAnchor.MiddleCenter);
                UiFactory.Label(card, BoardCatalog.XiangqiRankName(10 - index) + "行", 15, row - 19, 78, 38, 25, UiFactory.Ink);
            }
            UiFactory.CirclePanel(card, "Coordinate example point", firstFile + pitch - 9, firstRank + rankPitch - 9, 18, UiFactory.Accent);
            UiFactory.Label(card, BoardCatalog.Coordinate(BoardGameId.Xiangqi, 10), 279, firstRank + rankPitch - 22,
                width - 293, 44, 27, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            return height;
        }

        private static float RulesLabel(Transform content, string text, float y, float width, int fontSize, Color color, FontStyle style)
        {
            var label = UiFactory.Label(content, text, 0, y, width, 1, fontSize, color, TextAnchor.UpperLeft, style);
            label.supportRichText = false;
            label.lineSpacing = 1.12f;
            float height = Mathf.Ceil(Mathf.Max(fontSize * 1.45f, label.preferredHeight)) + 3;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        private static Scrollbar DrawRulesScrollbar(Transform parent, float x, float height)
        {
            const float width = 9;
            var track = UiFactory.Panel(parent, "Board rules scrollbar", x, 0, width, height, PaperLine, false);
            track.raycastTarget = true;
            var sliding = UiFactory.Rect(track.transform, "Rules scrollbar sliding area", 0, 0, width, height);
            var handle = UiFactory.Panel(sliding, "Rules scrollbar handle", 0, 0, width, height, UiFactory.Accent, false);
            UiFactory.Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }
    }
}
