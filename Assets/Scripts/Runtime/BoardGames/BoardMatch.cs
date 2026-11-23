using System;
using System.Collections.Generic;
using System.Linq;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal enum BoardGameId { Army, Xiangqi, Chess, Checkers }

    internal static class BoardCatalog
    {
        private static readonly string[] Ids = { "army-chess", "xiangqi", "chess", "chinese-checkers" };
        private static readonly string[] Names = { "军棋", "象棋", "国际象棋", "跳棋" };
        private static readonly string[] ArmyPieces = { "", "工兵", "排长", "连长", "营长", "团长", "旅长", "师长", "军长", "司令", "炸弹", "地雷", "军旗" };
        private static readonly string[] XiangqiRedPieces = { "", "帅", "仕", "相", "马", "车", "炮", "兵" };
        private static readonly string[] XiangqiBlackPieces = { "", "将", "士", "象", "马", "车", "炮", "卒" };
        private static readonly string[] ChessPieces = { "", "兵", "马", "象", "车", "后", "王" };
        internal static readonly int[] CheckersSeats = { 2, 3, 4, 6 };
        internal static string Id(BoardGameId game) => Ids[(int)game];
        internal static string Name(BoardGameId game) => Names[(int)game];
        internal static string Difficulty(BoardDifficulty difficulty) => new[] { "简单", "普通", "困难" }[(int)difficulty];

        internal static string PieceName(BoardGameId game, int kind, int side = 0)
        {
            if (kind < 0) return "暗棋";
            if (kind == 0) return "";
            switch (game)
            {
                case BoardGameId.Army: return ArmyPieces[kind];
                case BoardGameId.Xiangqi: return (side == 0 ? XiangqiRedPieces : XiangqiBlackPieces)[kind];
                case BoardGameId.Chess: return ChessPieces[kind];
                default: return "棋子";
            }
        }

        private static readonly string[] XiangqiRanks = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };

        internal static string XiangqiRankName(int rank) => XiangqiRanks[rank - 1];

        internal static string Coordinate(BoardGameId game, int point)
        {
            switch (game)
            {
                case BoardGameId.Chess: return ((char)('a' + point % 8)).ToString() + (point / 8 + 1);
                case BoardGameId.Xiangqi: return (point % 9 + 1) + "路" + XiangqiRankName(10 - point / 9) + "行";
                case BoardGameId.Army: return (point % 5 + 1) + "路" + (12 - point / 5) + "行";
                default: return (ChineseCheckersGame.RowAt(point) + 1) + "行" + (ChineseCheckersGame.DoubledXAt(point) + 13) + "位";
            }
        }

        internal static string SideName(BoardGameId game, BoardMatchOptions options, int side)
        {
            string sideName;
            switch (game)
            {
                case BoardGameId.Chess: sideName = side == 0 ? "白方" : "黑方"; break;
                case BoardGameId.Xiangqi: sideName = side == 0 ? "红方" : "黑方"; break;
                case BoardGameId.Army: sideName = side == 0 ? "红方" : "绿方"; break;
                default: sideName = "席位 " + (side + 1); break;
            }
            if (options.Local) return sideName + " · 玩家 " + (side + 1);
            if (side == options.Human) return sideName + " · 你";
            return sideName + (game == BoardGameId.Checkers ? " · 电脑 " + (side < options.Human ? side + 1 : side) : " · 电脑");
        }
    }

    internal readonly struct BoardMatchOptions
    {
        internal bool Local { get; }
        internal BoardDifficulty Difficulty { get; }
        internal int Seats { get; }
        internal int Human { get; }
        internal bool Flip { get; }
        internal int Slot => Local ? 0 : Array.IndexOf(BoardCatalog.CheckersSeats, Seats) + 1;

        internal BoardMatchOptions(bool local, BoardDifficulty difficulty, int seats, int human = 0, bool flip = false)
        { Local = local; Difficulty = difficulty; Seats = seats; Human = human; Flip = flip; }

        internal static BoardMatchOptions Default(BoardGameId game) =>
            new BoardMatchOptions(false, BoardDifficulty.Normal, game == BoardGameId.Checkers ? 3 : 2);

        internal bool ValidFor(BoardGameId game) =>
            Enum.IsDefined(typeof(BoardDifficulty), Difficulty) && Array.IndexOf(BoardCatalog.CheckersSeats, Seats) >= 0 &&
            ((game == BoardGameId.Checkers && !Local) || Seats == 2) && Human >= 0 && Human < Seats &&
            (!Local || Human == 0) && (!Flip || (Local && (game == BoardGameId.Xiangqi || game == BoardGameId.Chess)));
    }

    internal sealed class BoardMatch
    {
        internal const int HintLimit = 3;
        private readonly int[] armyFirst, armySecond;
        private readonly List<BoardMove> history = new List<BoardMove>();
        private readonly List<int> actors = new List<int>();
        internal BoardGameId Game { get; }
        internal BoardMatchOptions Options { get; }
        internal IBoardPosition Position { get; private set; }
        internal int Revision { get; private set; }
        internal int HintsUsed { get; private set; }
        internal bool Terminal => Position.Winner != BoardResult.Ongoing;
        internal bool Computer => !Options.Local && Position.CurrentSide != Options.Human;
        internal int MoveCount => history.Count;
        internal BoardMove LastMove => MoveCount == 0 ? BoardMove.None : history[MoveCount - 1];
        internal IEnumerable<BoardMove> History => history.AsReadOnly();
        internal int[] ArmyFirst => armyFirst == null ? Array.Empty<int>() : (int[])armyFirst.Clone();
        internal int[] ArmySecond => armySecond == null ? Array.Empty<int>() : (int[])armySecond.Clone();
        internal bool CanUndo => Game != BoardGameId.Army && !Terminal && MoveCount > 0 &&
            (Options.Local || actors.Contains(Options.Human));

        internal BoardMatch(BoardGameId game, BoardMatchOptions options, int[] armyFirst = null, int[] armySecond = null, int hintsUsed = 0)
        {
            if (!options.ValidFor(game)) throw new ArgumentException("Invalid board match options.");
            if (hintsUsed < 0 || hintsUsed > HintLimit) throw new ArgumentOutOfRangeException(nameof(hintsUsed));
            Game = game;
            Options = options;
            HintsUsed = hintsUsed;
            this.armyFirst = armyFirst == null ? null : (int[])armyFirst.Clone();
            this.armySecond = armySecond == null ? null : (int[])armySecond.Clone();
            Position = Opening();
        }

        private IBoardPosition Opening()
        {
            switch (Game)
            {
                case BoardGameId.Army: return new ArmyGame(armyFirst, armySecond);
                case BoardGameId.Xiangqi: return new XiangqiGame();
                case BoardGameId.Chess: return new ChessGame();
                case BoardGameId.Checkers: return new ChineseCheckersGame(Options.Seats);
                default: throw new ArgumentOutOfRangeException();
            }
        }

        internal bool Play(BoardMove move)
        {
            int actor = Position.CurrentSide;
            if (!Position.TryPlay(move)) return false;
            actors.Add(actor);
            history.Add(move);
            Revision++;
            return true;
        }

        internal bool Undo()
        {
            if (!CanUndo) return false;
            int keep = Options.Local ? MoveCount - 1 : actors.LastIndexOf(Options.Human);
            var restored = Opening();
            for (int i = 0; i < keep; i++)
                BoardReplay.PlayValidated(restored, history[i]);
            history.RemoveRange(keep, MoveCount - keep);
            actors.RemoveRange(keep, actors.Count - keep);
            Position = restored;
            Revision++;
            return true;
        }

        internal bool TryUseHint()
        {
            if (HintsUsed == HintLimit) return false;
            HintsUsed++;
            return true;
        }

        internal BoardReplay CreateReplay()
        {
            if (MoveCount == 0 || (Game == BoardGameId.Army && !Terminal)) return null;
            return new BoardReplay(Game, Options, Opening(), history, Terminal);
        }

        internal IReadOnlyList<string> DescribeHistory(int armyViewer = -1)
        {
            if (MoveCount == 0) return Array.Empty<string>();
            var replay = new BoardReplay(Game, Options, Opening(), history, Terminal, armyViewer);
            var descriptions = new string[replay.TotalMoves];
            while (replay.Advance()) descriptions[replay.MoveIndex - 1] = replay.LastDescription;
            return Array.AsReadOnly(descriptions);
        }

        internal string SideName(int side) => BoardCatalog.SideName(Game, Options, side);
    }

    internal sealed class BoardReplay
    {
        private readonly BoardGameId game;
        private readonly BoardMatchOptions options;
        private readonly IBoardPosition opening;
        private readonly BoardMove[] history;
        private readonly string[] descriptions;
        private readonly bool revealArmy;
        private readonly int armyViewer;

        internal IBoardPosition Position { get; private set; }
        internal int MoveIndex { get; private set; }
        internal int TotalMoves => history.Length;
        internal BoardMove LastMove => MoveIndex == 0 ? BoardMove.None : history[MoveIndex - 1];
        internal BoardMove NextMove => MoveIndex == TotalMoves ? BoardMove.None : history[MoveIndex];
        internal int LastActor { get; private set; }
        internal string LastDescription => MoveIndex == 0 ? "" : descriptions[MoveIndex - 1];

        internal BoardReplay(BoardGameId game, BoardMatchOptions options, IBoardPosition opening,
            IEnumerable<BoardMove> moves, bool revealArmy, int armyViewer = -1)
        {
            this.game = game;
            this.options = options;
            this.opening = opening.Copy();
            this.revealArmy = revealArmy;
            this.armyViewer = armyViewer;
            history = moves.ToArray();
            descriptions = new string[history.Length];
            Reset();
        }

        internal bool Advance()
        {
            if (MoveIndex == TotalMoves) return false;
            BoardMove move = history[MoveIndex];
            int actor = Position.CurrentSide;
            if (descriptions[MoveIndex] == null) descriptions[MoveIndex] = PlayAndDescribe(move);
            else PlayValidated(Position, move);
            LastActor = actor;
            MoveIndex++;
            return true;
        }

        internal bool Back()
        {
            if (MoveIndex == 0) return false;
            int target = MoveIndex - 1;
            var restored = opening.Copy();
            int actor = -1;
            for (int i = 0; i < target; i++)
            {
                actor = restored.CurrentSide;
                PlayValidated(restored, history[i]);
            }
            Position = restored;
            MoveIndex = target;
            LastActor = actor;
            return true;
        }

        internal void Reset()
        {
            Position = opening.Copy();
            MoveIndex = 0;
            LastActor = -1;
        }

        internal static void PlayValidated(IBoardPosition position, BoardMove move)
        {
            if (!position.TryPlay(move)) throw new InvalidOperationException("Stored legal turns cannot be replayed.");
        }

        private string PlayAndDescribe(BoardMove move)
        {
            int actor = Position.CurrentSide;
            string caption = BoardCatalog.SideName(game, options, actor) + " · ";
            if (move == BoardMove.Pass || move == BoardMove.ClaimDraw)
            {
                PlayValidated(Position, move);
                return caption + (move == BoardMove.Pass ? "跳过" : "申请和棋");
            }

            bool concealed = game == BoardGameId.Army && !revealArmy;
            BoardPiece mover = Position.PieceAt(move.From, concealed ? armyViewer : actor);
            BoardPiece target = Position.PieceAt(move.To, concealed ? armyViewer : game == BoardGameId.Army ? 1 - actor : actor);
            bool anonymous = concealed && armyViewer < 0;
            int moverKind = anonymous ? -1 : mover.Kind;
            int targetKind = anonymous && target.Kind != 0 ? -1 : target.Kind;
            string targetName = BoardCatalog.PieceName(game, targetKind, target.Side);
            caption += BoardCatalog.PieceName(game, moverKind, actor) + " " +
                BoardCatalog.Coordinate(game, move.From) + " → " + BoardCatalog.Coordinate(game, move.To);
            PlayValidated(Position, move);

            if (Position is ArmyGame army) return caption + DescribeArmyCombat(army.LastOutcome, targetName);
            if (game == BoardGameId.Checkers && move.PathLength != 0) return caption + " · " + move.PathLength + " 跳";
            if (targetKind > 0) caption += " · 吃" + targetName;
            if (game != BoardGameId.Chess) return caption;

            int fileChange = Math.Abs(move.To % 8 - move.From % 8);
            if (moverKind == ChessGame.Pawn && targetKind == 0 && fileChange != 0) caption += " · 吃过路兵";
            if (moverKind == ChessGame.King && fileChange == 2) caption += " · 王车易位";
            if (move.Choice != 0) caption += " · 升变为" + BoardCatalog.PieceName(game, move.Choice, actor);
            return caption;
        }

        private static string DescribeArmyCombat(ArmyCombatOutcome outcome, string targetName)
        {
            switch (outcome)
            {
                case ArmyCombatOutcome.AttackerSurvives: return " · 吃" + targetName;
                case ArmyCombatOutcome.DefenderSurvives: return " · 遇" + targetName + "后进攻方离场";
                case ArmyCombatOutcome.BothRemoved: return " · 与" + targetName + "同归于尽";
                default: return "";
            }
        }
    }
}
