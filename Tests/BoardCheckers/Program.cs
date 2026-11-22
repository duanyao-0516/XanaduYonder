using System;
using System.Linq;
using System.Threading;
using XanaduYonder.Core.BoardGames;

internal static class Program
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Main()
    {
        int from = ChineseCheckersGame.PointAt(13, -1);
        int a = ChineseCheckersGame.PointAt(11, 1);
        int b = ChineseCheckersGame.PointAt(9, 3);
        int c = ChineseCheckersGame.PointAt(7, 1);
        var path = new[] { a, b, c };
        var move = new BoardMove(from, c, path: path);
        path[0] = c;
        move.Path[0] = c;
        Require(move.Path[0] == a, "Chess move path is immutable.");
        var demo = ChineseCheckersGame.CreateTutorial();
        Require(demo.TryPlay(move), "Tutorial must support a legal three-hop turn.");
        Require(demo.PieceAt(c, 0).Side == 0 && demo.CurrentSide == 1, "A full chain advances exactly one seat.");
        var bad = ChineseCheckersGame.CreateTutorial();
        Require(!bad.TryPlay(new BoardMove(from, b, path: new[] { a, from, b })), "A chain cannot revisit its origin.");
        foreach (int seats in new[] { 2, 3, 4, 6 })
        {
            var position = new ChineseCheckersGame(seats);
            Require(position.PointCount == 121, "All seat counts use the 121-point board.");
            Require(Enumerable.Range(0, 121).Count(i => position.PieceAt(i, 0).Kind != 0) == seats * 10, "Ten pieces per seat.");
            Require(position.LegalMoves().Count != 0, "Playable opening.");
        }
        var six = new ChineseCheckersGame(6);
        foreach (BoardDifficulty difficulty in Enum.GetValues(typeof(BoardDifficulty)))
        {
            var round = (ChineseCheckersGame)six.Copy();
            for (int seat = 0; seat < 6; seat++)
            {
                Require(round.CurrentSide == seat, "Clockwise six-seat order.");
                var reply = round.CaptureSearch()(difficulty, seat, CancellationToken.None);
                Require(round.TryPlay(reply), "Computer must return a legal complete turn.");
            }
            Require(round.CurrentSide == 0, "The user regains the turn after five opponent seats.");
        }
        try
        {
            six.CaptureSearch()(BoardDifficulty.Hard, 1, new CancellationToken(true));
            throw new Exception("Search must observe cancellation.");
        }
        catch (OperationCanceledException) { }
        foreach (BoardDifficulty difficulty in Enum.GetValues(typeof(BoardDifficulty)))
        foreach (int seats in new[] { 2, 3, 4, 6 })
        {
            var game = new ChineseCheckersGame(seats);
            for (int ply = 0; ply < 350 && game.Winner == BoardResult.Ongoing; ply++)
            {
                BoardMove choice = game.CaptureSearch()(difficulty, ply, CancellationToken.None);
                Require(game.TryPlay(choice), "Self-play must produce a legal full turn.");
            }
            Require(game.Winner >= 0, difficulty + " self-play has to finish for " + seats + " seats.");
            Require(Enumerable.Range(0, game.PointCount).Count(point =>
                    ChineseCheckersGame.CampAt(point) == game.GoalCamp(game.Winner) &&
                    game.PieceAt(point, 0).Kind > 0 && game.PieceAt(point, 0).Side == game.Winner) == 10,
                "Self-play winner must occupy all ten goal holes.");
        }
        Console.WriteLine("Board Checkers geometry, routes, six-seat scheduling and three AI profiles passed.");
    }
}
