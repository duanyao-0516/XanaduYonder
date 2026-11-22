using System;
using System.Linq;
using XanaduYonder.Core.BoardGames;
using XanaduYonder.Runtime.BoardGames;

internal static class BoardReplayChecks
{
    internal static void Run()
    {
        ReplayNavigatesAnIndependentHistory();
        DescriptionsUseAbsoluteCoordinatesAndPublicCaptures();
        ArmyReplayRequiresAnEndedMatch();
        CheckersReplayRetainsRoute();
        HintsStaySpentAfterUndo();
        Console.WriteLine("5 board replay and hint count scenarios passed.");
    }

    private static void ReplayNavigatesAnIndependentHistory()
    {
        var match = Local(BoardGameId.Chess);
        Check(match.CreateReplay() == null && match.DescribeHistory().Count == 0, "empty history has no replay");
        var first = new BoardMove(12, 28);
        var second = new BoardMove(52, 36);
        Play(match, first, second);
        var replay = match.CreateReplay();
        Check(replay.MoveIndex == 0 && replay.TotalMoves == 2 && replay.LastMove == BoardMove.None && replay.LastDescription == "",
            "replay starts at the opening with no move");
        Check(!replay.Back() && replay.Position.PieceAt(12, 0).Kind == ChessGame.Pawn, "opening pieces and lower boundary");
        Play(match, new BoardMove(6, 21));
        Check(replay.TotalMoves == 2 && replay.Advance() && replay.LastMove == first, "new live moves do not change the captured history");
        Check(replay.LastDescription.Contains("白方") && replay.LastDescription.Contains("兵") && replay.LastDescription.Contains("e2 → e4"),
            "turn caption includes actor, piece, and absolute squares");
        Check(replay.Advance() && replay.LastMove == second && !replay.Advance(), "upper boundary preserves the last ply");
        Check(replay.Back() && replay.MoveIndex == 1 && replay.LastMove == first && replay.Position.PieceAt(52, 1).Kind == ChessGame.Pawn,
            "back reconstructs the earlier board and caption");
        replay.Reset();
        Check(replay.MoveIndex == 0 && replay.LastMove == BoardMove.None && replay.LastDescription == "" && replay.Advance(), "reset restarts from the opening");
        Check(match.MoveCount == 3 && match.Position.PieceAt(21, 0).Kind == ChessGame.Knight, "replay never changes the live position");
    }

    private static void DescriptionsUseAbsoluteCoordinatesAndPublicCaptures()
    {
        var xiangqi = Local(BoardGameId.Xiangqi);
        Play(xiangqi, new BoardMove(54, 45), new BoardMove(27, 36), new BoardMove(45, 36));
        string capture = xiangqi.DescribeHistory()[2];
        Check(capture.Contains("兵") && capture.Contains("1路五行 → 1路六行") && capture.Contains("吃卒"), "xiangqi caption combines numeric files and Chinese ranks");

        var chess = Local(BoardGameId.Chess);
        Play(chess, new BoardMove(12, 28), new BoardMove(48, 40), new BoardMove(28, 36), new BoardMove(51, 35), new BoardMove(36, 43));
        Check(chess.DescribeHistory()[4].Contains("吃过路兵"), "en passant includes a capture with an initially empty destination");

        var promotion = new BoardReplay(BoardGameId.Chess, BoardMatchOptions.Default(BoardGameId.Chess),
            ChessGame.FromFen("7k/P7/8/8/8/8/8/4K3 w - - 0 1"), new[] { new BoardMove(48, 56, ChessGame.Queen) }, false);
        Check(promotion.Advance() && promotion.LastDescription.Contains("a7 → a8") && promotion.LastDescription.Contains("升变为后"),
            "chess caption identifies the promoted piece");
    }

    private static void ArmyReplayRequiresAnEndedMatch()
    {
        var formation = ArmyGame.CreateFormation(9);
        var army = new BoardMatch(BoardGameId.Army, new BoardMatchOptions(true, BoardDifficulty.Normal, 2), formation, formation);
        var cycle = new[] { new BoardMove(31, 36), new BoardMove(26, 21), new BoardMove(36, 31), new BoardMove(21, 26) };
        Play(army, cycle[0]);
        var concealed = army.DescribeHistory();
        Check(army.CreateReplay() == null && concealed[0].Contains("暗棋") && concealed[0].Contains("2路6行 → 2路5行"),
            "ongoing Junqi permits only public captions, with absolute ranks");
        string name = BoardCatalog.PieceName(BoardGameId.Army, formation[1]);
        Check(!concealed[0].Contains(name), "ongoing caption conceals even the moving piece's true rank");
        Check(army.DescribeHistory(0)[0].Contains(name) && army.DescribeHistory(1)[0].Contains("暗棋"),
            "ongoing history reveals the moving rank only to its owner");
        for (int ply = 1; ply < 60; ply++) Play(army, cycle[ply % cycle.Length]);
        var replay = army.CreateReplay();
        Check(army.Terminal && replay != null && replay.TotalMoves == 60, "terminal Junqi exposes a separate full replay");
        Check(replay.Advance() && replay.LastDescription.Contains(name) && army.DescribeHistory()[0].Contains(name), "terminal captions identify the moved piece");
        Check(!concealed[0].Contains(name), "prior public caption snapshot never changes");
        while (replay.Advance()) { }
        Check(replay.Position.Winner == BoardResult.Draw, "terminal replay reproduces the actual result");
    }

    private static void CheckersReplayRetainsRoute()
    {
        var checkers = Local(BoardGameId.Checkers);
        var hop = checkers.Position.LegalMoves().First(move => move.PathLength > 0);
        Play(checkers, hop);
        var replay = checkers.CreateReplay();
        Check(replay != null && replay.MoveIndex == 0, "checkers replay starts from the opening layout");
        Check(replay.Advance() && replay.LastMove.Path.SequenceEqual(hop.Path) && replay.LastDescription.Contains("玩家 1"),
            "checkers replay preserves the seat and every landing");
    }

    private static void HintsStaySpentAfterUndo()
    {
        var options = new BoardMatchOptions(true, BoardDifficulty.Normal, 2);
        var match = new BoardMatch(BoardGameId.Chess, options, hintsUsed: 1);
        Play(match, new BoardMove(12, 28));
        Check(match.TryUseHint() && match.Undo() && match.HintsUsed == 2, "undo preserves spent hints");
        Check(match.TryUseHint() && !match.TryUseHint() && match.HintsUsed == BoardMatch.HintLimit, "hint count is bounded");
        try
        {
            new BoardMatch(BoardGameId.Chess, options, hintsUsed: BoardMatch.HintLimit + 1);
            throw new Exception("invalid saved hint count must fail at the model boundary");
        }
        catch (ArgumentOutOfRangeException) { }
    }

    private static BoardMatch Local(BoardGameId game) => new BoardMatch(game, new BoardMatchOptions(true, BoardDifficulty.Normal, 2));

    private static void Play(BoardMatch match, params BoardMove[] moves)
    {
        foreach (var move in moves) Check(match.Play(move), "fixture follows normal legal play");
    }

    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }
}
