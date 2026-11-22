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
        internal static readonly int[] CheckersSeats = { 2, 3, 4, 6 };
        internal static string Id(BoardGameId game) => Ids[(int)game];
        internal static string Name(BoardGameId game) => Names[(int)game];
        internal static string Difficulty(BoardDifficulty difficulty) => new[] { "简单", "普通", "困难" }[(int)difficulty];
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
        private readonly int[] armyFirst, armySecond;
        private readonly List<BoardMove> history = new List<BoardMove>();
        private readonly List<int> actors = new List<int>();
        internal BoardGameId Game { get; }
        internal BoardMatchOptions Options { get; }
        internal IBoardPosition Position { get; private set; }
        internal int Revision { get; private set; }
        internal bool Terminal => Position.Winner != BoardResult.Ongoing;
        internal bool Computer => !Options.Local && Position.CurrentSide != Options.Human;
        internal int MoveCount => history.Count;
        internal BoardMove LastMove => MoveCount == 0 ? BoardMove.None : history[MoveCount - 1];
        internal IEnumerable<BoardMove> History => history.AsReadOnly();
        internal int[] ArmyFirst => armyFirst == null ? Array.Empty<int>() : (int[])armyFirst.Clone();
        internal int[] ArmySecond => armySecond == null ? Array.Empty<int>() : (int[])armySecond.Clone();
        internal bool CanUndo => Game != BoardGameId.Army && !Terminal && MoveCount > 0 &&
            (Options.Local || actors.Contains(Options.Human));

        internal BoardMatch(BoardGameId game, BoardMatchOptions options, int[] armyFirst = null, int[] armySecond = null)
        {
            if (!options.ValidFor(game)) throw new ArgumentException("Invalid board match options.");
            Game = game;
            Options = options;
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
                if (!restored.TryPlay(history[i])) throw new InvalidOperationException("Stored legal turns cannot be replayed.");
            history.RemoveRange(keep, MoveCount - keep);
            actors.RemoveRange(keep, actors.Count - keep);
            Position = restored;
            Revision++;
            return true;
        }

        internal string SideName(int side)
        {
            string sideName;
            switch (Game)
            {
                case BoardGameId.Chess: sideName = side == 0 ? "白方" : "黑方"; break;
                case BoardGameId.Xiangqi: sideName = side == 0 ? "红方" : "黑方"; break;
                case BoardGameId.Army: sideName = side == 0 ? "红方" : "绿方"; break;
                default: sideName = "席位 " + (side + 1); break;
            }
            if (Options.Local) return sideName + " · 玩家 " + (side + 1);
            if (side == Options.Human) return sideName + " · 你";
            return sideName + (Game == BoardGameId.Checkers ? " · 电脑 " + (side < Options.Human ? side + 1 : side) : " · 电脑");
        }
    }
}
