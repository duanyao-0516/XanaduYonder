using System;
using System.Linq;
using System.Threading;
using XanaduYonder.Core.BoardGames;

internal static class Program
{
    private static void Main()
    {
        Run("starting position perft", () =>
        {
            var game = new ChessGame();
            Assert(game.CurrentSide == ChessGame.White && game.PointCount == 64, "white moves first on 64 squares");
            Assert(game.PieceAt(Square("a1"), 1).Kind == ChessGame.Rook, "a1 is white's rook");
            Assert(Perft(game, 1) == 20 && Perft(game, 2) == 400 && Perft(game, 3) == 8902, "starting perft should be 20/400/8902");
            Assert(!game.TryPlay(Move("e2e5")) && !game.TryPlay(BoardMove.Pass), "reject illegal actions");
        });

        Run("castling, checks and pins perft", () =>
        {
            var game = ChessGame.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            Assert(Perft(game, 1) == 48 && Perft(game, 2) == 2039 && Perft(game, 3) == 97862, "Kiwipete perft should be 48/2039/97862");
        });

        Run("castling relocates rook and rejects attacked transit", () =>
        {
            var game = ChessGame.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert(game.TryPlay(Move("e1g1")) && game.PieceAt(5, 0).Kind == ChessGame.Rook, "short castle");
            Assert(game.TryPlay(Move("e8c8")) && game.PieceAt(59, 0).Kind == ChessGame.Rook, "long castle");
            var attacked = ChessGame.FromFen("r3kr2/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Assert(!attacked.TryPlay(Move("e1g1")) && attacked.TryPlay(Move("e1c1")), "f1 attacked");
        });

        Run("legal en passant removes the passed pawn", () =>
        {
            var game = ChessGame.FromFen("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2");
            Assert(game.TryPlay(Move("e5d6")), "en passant was missing");
            Assert(game.PieceAt(Square("d5"), 0).Kind == 0 && game.PieceAt(Square("d6"), 0).Kind == ChessGame.Pawn, "en passant removal");
        });

        Run("en passant cannot expose one's own king", () =>
        {
            var game = ChessGame.FromFen("k7/8/8/r4pPK/8/8/8/8 w - f6 0 1");
            Assert(!game.TryPlay(Move("g5f6")), "vacating both pawn squares exposes the king");
        });

        Run("all four promotion choices have the stated kind", () =>
        {
            var game = ChessGame.FromFen("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");
            int[] choices = { ChessGame.Knight, ChessGame.Bishop, ChessGame.Rook, ChessGame.Queen };
            Assert(game.LegalMoves().Where(move => move.From == 48).Select(move => move.Choice).OrderBy(kind => kind).SequenceEqual(choices), "promotion choices");
            Assert(!game.TryPlay(Move("a7a8")), "promotion must be explicit");
            foreach (int kind in choices)
            {
                var copy = (ChessGame)game.Copy();
                Assert(copy.TryPlay(Move("a7a8", kind)) && copy.PieceAt(56, 0).Kind == kind, "promotion placement");
            }
            Assert(game.PieceAt(48, 0).Kind == ChessGame.Pawn, "copy does not change source");
        });

        Run("checkmate precedes the automatic 75-move draw", () =>
        {
            var game = ChessGame.FromFen("7k/8/5KQ1/8/8/8/8/8 w - - 149 75");
            Assert(game.TryPlay(Move("g6g7")) && game.Winner == ChessGame.White && game.LegalMoves().Count == 0, "mate at halfmove 150");
        });

        Run("stalemate and dead material stop play", () =>
        {
            foreach (string fen in new[]
            {
                "7k/5K2/6Q1/8/8/8/8/8 b - - 0 1",
                "7k/8/5K2/8/8/8/8/8 w - - 0 1",
                "7k/8/5K2/8/3N4/8/8/8 w - - 0 1",
                "7k/8/5K2/8/3B4/8/1b6/8 w - - 0 1"
            })
            {
                var game = ChessGame.FromFen(fen);
                Assert(game.Winner == BoardResult.Draw && game.LegalMoves().Count == 0, "expected adjudicated draw: " + fen);
            }
            Assert(ChessGame.FromFen("7k/8/5K2/8/3N4/8/1N6/8 w - - 0 1").Winner == BoardResult.Ongoing, "two knights can cooperate to mate");
        });

        Run("a forced capture can make the current position dead", () =>
        {
            var game = ChessGame.FromFen("8/8/8/8/8/1n6/B1k5/K7 w - - 0 1");
            Assert(game.Winner == BoardResult.Draw, "only Bxb3 is possible and king and bishop cannot mate");
        });

        Run("permanently blocked pawn wall is dead", () =>
        {
            var wall = ChessGame.FromFen("6k1/8/8/p1p1p1p1/P1P1P1P1/8/8/6K1 w - - 0 1");
            Assert(wall.Winner == BoardResult.Draw, "kings cannot breach the pawn wall");
            var breach = ChessGame.FromFen("6k1/8/8/p1p1p3/P1P1P1P1/8/8/6K1 w - - 0 1");
            Assert(breach.Winner == BoardResult.Ongoing, "a pawn can advance through a gap");
        });

        Run("50-move claim from a legal intended move and automatic 75 moves", () =>
        {
            var game = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 99 50");
            Assert(game.CanClaimDraw(Move("a1a2")) && !game.CanClaimDraw(Move("a1b2")), "only a legal intended move can support a claim");
            Assert(game.TryPlay(BoardMove.ClaimDraw) && game.Winner == BoardResult.Draw && game.PieceAt(0, 0).Kind == ChessGame.Rook, "claim ends play before moving the rook");
            var automatic = ChessGame.FromFen("4k3/8/8/8/8/8/8/R3K3 w - - 149 75");
            Assert(automatic.TryPlay(Move("a1a2")) && automatic.Winner == BoardResult.Draw, "75-move draw");
            var pawn = ChessGame.FromFen("4k3/8/8/8/8/8/P7/R3K3 w - - 149 75");
            Assert(pawn.TryPlay(Move("a2a3")) && pawn.Winner == BoardResult.Ongoing, "pawn move restarts the count");
        });

        Run("third repetition can be claimed before playing and fifth ends automatically", () =>
        {
            var history = new[] { "g1f3", "g8f6", "f3g1", "f6g8" };
            var game = new ChessGame();
            for (int i = 0; i < 7; i++) Assert(game.TryPlay(Move(history[i % 4])), "cycle setup");
            Assert(game.CanClaimDraw(Move("f6g8")), "about-to-occur third position");
            var throughClaim = (ChessGame)game.Copy();
            Assert(throughClaim.TryPlay(BoardMove.ClaimDraw) && throughClaim.Winner == BoardResult.Draw, "threefold claim");
            for (int i = 7; i < 16; i++) Assert(game.TryPlay(Move(history[i % 4])), "repeat through fifth");
            Assert(game.Winner == BoardResult.Draw && !game.TryPlay(Move("g1f3")), "automatic fivefold prevents further play");
        });

        Run("repetition includes legally available en passant and castling rights", () =>
        {
            var game = ChessGame.FromFen("4k1n1/8/8/3pP3/8/8/8/4K1N1 w - d6 0 1");
            var cycle = new[] { "g1f3", "g8f6", "f3g1", "f6g8" };
            for (int i = 0; i < 7; i++) Assert(game.TryPlay(Move(cycle[i % 4])), "cycle with en passant expired");
            Assert(!game.CanClaimDraw(Move("f6g8")), "the initial en passant right is distinct");

            var pinned = ChessGame.FromFen("kn6/8/8/r4pPK/8/8/8/6N1 w - f6 0 1");
            var pinnedCycle = new[] { "g1f3", "b8c6", "f3g1", "c6b8" };
            for (int i = 0; i < 7; i++) Assert(pinned.TryPlay(Move(pinnedCycle[i % 4])), "cycle with illegal en passant");
            Assert(pinned.CanClaimDraw(Move("c6b8")), "unusable en passant target does not distinguish positions");

            var castle = ChessGame.FromFen("4k1n1/8/8/8/8/8/8/4K2R w K - 0 1");
            var rookCycle = new[] { "h1h2", "g8f6", "h2h1", "f6g8" };
            for (int i = 0; i < 7; i++) Assert(castle.TryPlay(Move(rookCycle[i % 4])), "cycle with rook returned");
            Assert(!castle.CanClaimDraw(Move("f6g8")), "initial castling right distinguishes the starting position");
        });

        Run("move replay restores an intended threefold claim", () =>
        {
            var game = new ChessGame();
            BoardMove[] history = new[] { "g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1" }
                .Select(move => Move(move)).Concat(new[] { BoardMove.ClaimDraw }).ToArray();
            foreach (BoardMove move in history) Assert(game.TryPlay(move), "replay legal move");
            Assert(game.Winner == BoardResult.Draw && game.LegalMoves().Count == 0, "replay adjudication");
            Assert(game.CurrentSide == ChessGame.Black && game.PieceAt(Square("f6"), 0).Kind == ChessGame.Knight,
                "claim leaves the intended move unplayed");
        });

        Run("search snapshots stay legal through repeated calls and honor cancellation", () =>
        {
            var game = new ChessGame();
            BoardSearch search = game.CaptureSearch();
            BoardMove[] originalMoves = game.LegalMoves().ToArray();
            Assert(game.TryPlay(Move("a2a3")), "mutate live game after capture");
            foreach (BoardDifficulty level in Enum.GetValues(typeof(BoardDifficulty)))
            {
                BoardMove move = search(level, 174, CancellationToken.None);
                Assert(originalMoves.Contains(move), "snapshot returns a legal white move for " + level);
                Assert(originalMoves.Contains(search(level, 174, CancellationToken.None)), "repeat searches use a fresh snapshot for " + level);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                try
                {
                    search(BoardDifficulty.Hard, 174, cancellation.Token);
                    throw new Exception("a canceled operation must throw");
                }
                catch (OperationCanceledException) { }
            }
            Assert(game.CurrentSide == ChessGame.Black && game.PieceAt(Square("a3"), 0).Kind == ChessGame.Pawn, "source board unchanged by search");
        });

        Run("all levels find immediate checkmate", () =>
        {
            var game = ChessGame.FromFen("7k/8/5KQ1/8/8/8/8/8 w - - 0 1");
            foreach (BoardDifficulty level in Enum.GetValues(typeof(BoardDifficulty)))
            {
                var copy = game.Copy();
                BoardMove move = game.CaptureSearch()(level, 42, CancellationToken.None);
                Assert(copy.TryPlay(move) && copy.Winner == ChessGame.White, "mate in one at " + level);
            }
        });

        Run("normal and hard take a free queen", () =>
        {
            var game = ChessGame.FromFen("4k3/8/8/8/8/8/q7/R3K3 w - - 0 1");
            foreach (BoardDifficulty level in new[] { BoardDifficulty.Normal, BoardDifficulty.Hard })
            {
                BoardMove move = game.CaptureSearch()(level, 287, CancellationToken.None);
                Assert(move == Move("a1a2"), "take queen at " + level);
            }
        });

        Run("FEN rejects structurally invalid positions", () =>
        {
            foreach (string fen in new[]
            {
                "8/8/8/8/8/8/8/8 w - - 0 1",
                "7k/8/8/8/8/8/8/7K w K - 0 1",
                "7k/8/8/8/8/8/8/7K w - a3 0 1",
                "7k/8/8/8/8/8/8/7K w - - -1 1"
            })
            {
                try
                {
                    ChessGame.FromFen(fen);
                    throw new Exception("should reject " + fen);
                }
                catch (FormatException) { }
            }
        });
    }

    private static long Perft(IBoardPosition game, int depth)
    {
        if (depth == 0) return 1;
        long nodes = 0;
        foreach (BoardMove move in game.LegalMoves())
        {
            if (move.From < 0) continue;
            IBoardPosition child = game.Copy();
            Assert(child.TryPlay(move), "generated move must play on copy");
            nodes += Perft(child, depth - 1);
        }
        return nodes;
    }

    private static BoardMove Move(string move, int choice = 0)
    {
        return new BoardMove(Square(move.Substring(0, 2)), Square(move.Substring(2, 2)), choice);
    }

    private static int Square(string square)
    {
        return (square[1] - '1') * 8 + square[0] - 'a';
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine("PASS " + name);
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
}
