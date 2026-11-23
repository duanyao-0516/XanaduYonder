using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using XanaduYonder.Core.BoardGames;

internal static class Program
{
    private static int Main()
    {
        AssertOpening();
        AssertPieceRestrictions();
        AssertEndings();
        AssertRepetition();
        AssertNaturalLimit();
        AssertSearch();
        AssertOpeningSearch();
        Console.WriteLine("All Xiangqi fixtures passed.");
        return 0;
    }

    private static void AssertOpening()
    {
        var game = new XiangqiGame();
        Equal(90, game.PointCount, "board size");
        Equal(0, game.CurrentSide, "Red opens");
        Equal(1, game.PieceAt(Point(9, 4), 1).Kind, "red king is public");
        Equal(0, game.PieceAt(Point(9, 4), 1).Side, "red king side");
        Equal(1, game.PieceAt(Point(0, 4), 0).Side, "black king side");
        Equal(32, Enumerable.Range(0, 90).Count(point => game.PieceAt(point, 0).Kind != 0), "initial pieces");
        Equal(44, Perft(game, 1), "opening perft 1");
        Equal(1920, Perft(game, 2), "opening perft 2");
        Equal(79666, Perft(game, 3), "opening perft 3");
    }

    private static void AssertPieceRestrictions()
    {
        var cannon = XiangqiGame.FromFen("4k4/9/9/9/4p4/9/4P4/1C2p2r1/9/4K4 w");
        Equal(false, cannon.TryPlay(Move(7, 1, 7, 6)), "cannon cannot jump to an empty point");
        Equal(false, cannon.TryPlay(Move(7, 1, 7, 4)), "cannon cannot capture without a screen");
        Equal(true, cannon.TryPlay(Move(7, 1, 7, 7)), "cannon can capture after one screen");
        var horse = XiangqiGame.FromFen("4k4/9/9/9/4p4/9/4P4/2N6/2P6/2B1KA3 w");
        Equal(false, horse.TryPlay(Move(7, 2, 9, 1)), "horse leg is blocked");
        Equal(true, horse.LegalMoves().Contains(Move(7, 2, 5, 1)), "horse with an open leg");
        Equal(true, horse.LegalMoves().Contains(Move(9, 2, 7, 4)), "elephant with an open eye");
        Equal(false, horse.LegalMoves().Contains(Move(9, 5, 8, 6)), "advisor stays in its palace");
        var elephant = XiangqiGame.FromFen("3k5/9/9/9/9/2B6/3P5/9/9/4K4 w");
        Equal(false, elephant.LegalMoves().Contains(Move(5, 2, 3, 0)), "elephant cannot cross the river");
        Equal(false, elephant.LegalMoves().Contains(Move(5, 2, 7, 4)), "elephant eye is blocked");
        Equal(true, elephant.LegalMoves().Contains(Move(5, 2, 7, 0)), "open elephant diagonal");
        var pawns = XiangqiGame.FromFen("3k5/9/9/9/4P4/4P4/9/9/9/4K4 w");
        Equal(true, pawns.LegalMoves().Contains(Move(4, 4, 4, 5)), "crossed pawn can move sideways");
        Equal(false, pawns.LegalMoves().Contains(Move(5, 4, 5, 5)), "uncrossed pawn cannot move sideways");
        Equal(false, pawns.LegalMoves().Contains(Move(4, 4, 5, 4)), "pawn cannot go backwards");
        var facing = XiangqiGame.FromFen("4k4/9/9/9/4P4/9/9/9/9/4K4 w");
        Equal(false, facing.TryPlay(Move(4, 4, 4, 5)), "moving the screen cannot expose facing kings");
        var terminalCapture = XiangqiGame.FromFen("4k4/9/9/9/4R4/9/9/9/9/4K4 w");
        Equal(false, terminalCapture.TryPlay(Move(4, 4, 0, 4)), "play ends at mate without capturing the king");
        Equal(false, facing.TryPlay(BoardMove.Pass), "pass is illegal");
    }

    private static void AssertEndings()
    {
        var stalemate = XiangqiGame.FromFen("4k4/3R1R3/9/4P4/9/9/9/9/9/4K4 b");
        Equal(0, stalemate.Winner, "side without a move loses in stalemate");
        Equal(0, stalemate.LegalMoves().Count, "stalemate has no moves");
        var mate = XiangqiGame.FromFen("4k4/4RR3/2N6/4P4/9/9/9/9/9/4K4 b");
        Equal(0, mate.Winner, "checkmate");
        Equal(0, mate.LegalMoves().Count, "checkmate ends the game");
        var bareKings = XiangqiGame.FromFen("3k5/9/9/9/9/9/9/9/9/5K3 w");
        Equal(BoardResult.Draw, bareKings.Winner, "bare kings cannot win");
    }

    private static void AssertRepetition()
    {
        var peaceful = XiangqiGame.FromFen("3k5/9/9/9/p8/9/9/9/9/5K3 w");
        BoardMove[] idle = { Move(9, 5, 8, 5), Move(0, 3, 1, 3), Move(8, 5, 9, 5), Move(1, 3, 0, 3) };
        Cycle(peaceful, idle, 2);
        Equal(BoardResult.Ongoing, peaceful.Winner, "the starting position plus two returns is still playable");
        var copy = (XiangqiGame)peaceful.Copy();
        Cycle(copy, idle, 1);
        Equal(BoardResult.Draw, copy.Winner, "three idle returns adjudicate a draw");
        Equal(BoardResult.Ongoing, peaceful.Winner, "copy preserves but does not change live repetition history");

        var checking = XiangqiGame.FromFen("4k4/3R5/9/9/4P4/9/9/9/9/4K4 w");
        BoardMove[] checks = { Move(1, 3, 1, 4), Move(0, 4, 0, 3), Move(1, 4, 1, 3), Move(0, 3, 0, 4) };
        Cycle(checking, checks, 3);
        Equal(1, checking.Winner, "unilateral perpetual check loses");

        var alternateCheck = XiangqiGame.FromFen("4k4/3R5/9/9/4P4/9/9/9/9/4K4 w");
        BoardMove[] mixed = { Move(1, 3, 1, 4), Move(0, 4, 0, 5), Move(1, 4, 1, 3), Move(0, 5, 0, 4) };
        Cycle(alternateCheck, mixed, 3);
        Equal(BoardResult.Draw, alternateCheck.Winner, "alternating check and a non-check repeats neutrally");

        // WXF 2018, Chapter 5, Diagram 4: both moves in each pair are cross-checks.
        var crossChecks = XiangqiGame.FromFen("3akr3/5c3/1P2e4/4R4/9/9/9/9/9/4CK3 w");
        Play(crossChecks, Move(3, 4, 2, 4));
        Play(crossChecks, Move(1, 5, 1, 4));
        BoardMove[] crossCycle = { Move(2, 4, 2, 5), Move(1, 4, 1, 5), Move(2, 5, 2, 4), Move(1, 5, 1, 4) };
        Cycle(crossChecks, crossCycle, 2);
        foreach (var move in crossCycle.Take(3)) Play(crossChecks, move);
        Equal(BoardResult.Draw, crossChecks.Winner, "WXF Diagram 4 mutual perpetual checks draw");

        var directChase = XiangqiGame.FromFen("3k5/9/9/9/9/7R1/9/2n6/9/5K3 w");
        BoardMove[] chaseCycle = { Move(5, 7, 7, 7), Move(7, 2, 5, 3), Move(7, 7, 5, 7), Move(5, 3, 7, 2) };
        Cycle(directChase, chaseCycle, 3);
        Equal(1, directChase.Winner, "a rook repeatedly chasing the same unrooted horse loses");

        var protectedHorse = XiangqiGame.FromFen("3k5/9/9/9/9/2r4R1/9/2n6/9/5K3 w");
        Cycle(protectedHorse, chaseCycle, 3);
        Equal(BoardResult.Draw, protectedHorse.Winner, "chasing a horse with a legal recapture is permitted");

        BoardMove[] chaseRook = { Move(5, 7, 7, 7), Move(7, 2, 5, 2), Move(7, 7, 5, 7), Move(5, 2, 7, 2) };
        var exchange = XiangqiGame.FromFen("3k5/9/9/9/9/7R1/9/2r6/9/5K3 w");
        Cycle(exchange, chaseRook, 3);
        Equal(BoardResult.Draw, exchange.Winner, "rooks that can legally take each other may repeat");

        var pinnedRook = XiangqiGame.FromFen("3k5/9/3r5/9/7R1/9/9/3R5/9/5K3 w");
        BoardMove[] pin = { Move(4, 7, 2, 7), Move(2, 3, 4, 3), Move(2, 7, 4, 7), Move(4, 3, 2, 3) };
        Cycle(pinnedRook, pin, 3);
        Equal(1, pinnedRook.Winner, "a pinned rook cannot offer a reciprocal exchange");

        var protectedRook = XiangqiGame.FromFen("3k5/9/9/9/2r6/4N2C1/9/2r1N4/9/5K3 w");
        Cycle(protectedRook, chaseRook, 3);
        Equal(1, protectedRook.Winner, "cannon chase of a rooted rook remains prohibited");
    }

    private static void AssertNaturalLimit()
    {
        var quiet = XiangqiGame.FromFen("3k5/9/9/9/9/9/9/9/9/R4K3 w - - 99 1");
        Play(quiet, Move(9, 0, 8, 0));
        Equal(BoardResult.Draw, quiet.Winner, "fifty moves each with no capture");
        var capture = XiangqiGame.FromFen("3k5/9/9/9/9/9/9/9/p8/R4K3 w - - 99 1");
        Play(capture, Move(9, 0, 8, 0));
        Equal(BoardResult.Ongoing, capture.Winner, "capture resets progress toward the no-capture draw");
    }

    private static void AssertSearch()
    {
        var game = XiangqiGame.FromFen("4k4/R4R3/2N6/4P4/9/9/9/9/9/4K4 w");
        var mate = Move(1, 0, 1, 4);
        var initial = game.LegalMoves();
        var choose = game.CaptureSearch();
        Play(game, mate);
        Equal(0, game.Winner, "the fixture has mate in one");
        foreach (BoardDifficulty difficulty in Enum.GetValues(typeof(BoardDifficulty)))
        {
            var timer = Stopwatch.StartNew();
            var move = choose(difficulty, 37, CancellationToken.None);
            Equal(true, initial.Contains(move), difficulty + " searches its snapshot");
            Equal(move, choose(difficulty, 37, CancellationToken.None), difficulty + " uses a deterministic seed");
            if (difficulty != BoardDifficulty.Easy)
            {
                var replay = XiangqiGame.FromFen("4k4/R4R3/2N6/4P4/9/9/9/9/9/4K4 w");
                Play(replay, move);
                Equal(0, replay.Winner, difficulty + " finds a forced win in one move");
            }
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                try
                {
                    choose(difficulty, 37, stop.Token);
                    throw new InvalidOperationException(difficulty + " ignored cancellation");
                }
                catch (OperationCanceledException)
                {
                }
            }
            Console.WriteLine(difficulty + " tactical fixture " + timer.ElapsedMilliseconds + "ms");
        }
    }

    private static void AssertOpeningSearch()
    {
        var game = new XiangqiGame();
        var moves = game.LegalMoves();
        var choose = game.CaptureSearch();
        foreach (BoardDifficulty difficulty in Enum.GetValues(typeof(BoardDifficulty)))
        {
            var timer = Stopwatch.StartNew();
            Equal(true, moves.Contains(choose(difficulty, 11, CancellationToken.None)), difficulty + " opening move is legal");
            Console.WriteLine(difficulty + " opening fixture " + timer.ElapsedMilliseconds + "ms");
        }
        Equal(0, game.CurrentSide, "search does not advance the live board");
        Equal(44, game.LegalMoves().Count, "search does not mutate its snapshot or live position");
    }

    private static int Perft(IBoardPosition position, int depth)
    {
        var moves = position.LegalMoves();
        if (depth == 1) return moves.Count;
        var count = 0;
        foreach (var move in moves)
        {
            var next = position.Copy();
            Play(next, move);
            count += Perft(next, depth - 1);
        }
        return count;
    }

    private static void Cycle(IBoardPosition game, BoardMove[] moves, int times)
    {
        for (var i = 0; i < times; i++)
            foreach (var move in moves) Play(game, move);
    }

    private static int Point(int row, int column) => row * 9 + column;
    private static BoardMove Move(int row, int column, int toRow, int toColumn) =>
        new BoardMove(Point(row, column), Point(toRow, toColumn));

    private static void Play(IBoardPosition position, BoardMove move) =>
        Equal(true, position.TryPlay(move), "play " + move.From + "->" + move.To + ", " + position.Status);

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(description + ": expected " + expected + ", got " + actual);
    }
}
