using System;
using System.Linq;
using System.Threading;
using XanaduYonder.Core.BoardGames;

internal static class Program
{
    private static void Main()
    {
        FormationsContainTheFullInventory();
        TopologyUsesCampsAndTheThreeBorderBridges();
        EngineersCanTurnWithoutPassingPieces();
        CombatResolvesRanksAndSpecialPieces();
        MarshalLossRevealsOnlyTheFlags();
        FlagCaptureEndsTheMatch();
        QuietTurnsDrawAfterSixtyPlies();
        BeliefSamplesRespectPublicConstraintsAndInventory();
        SearchCannotDistinguishEquivalentPrivateFormations();
        CapturedSearchIsStableAndCancellable();
        Console.WriteLine("10 Junqi scenarios passed.");
    }

    private static void FormationsContainTheFullInventory()
    {
        for (int seed = -16; seed <= 16; seed++)
        {
            var formation = ArmyGame.CreateFormation(seed);
            Check(ArmyGame.IsFormationValid(formation), "generated formation is valid");
            Check(formation.SequenceEqual(ArmyGame.CreateFormation(seed)), "same seed reproduces formation");
            Check(formation.Count(piece => piece == (int)ArmyPieceKind.Engineer) == 3, "three engineers");
            Check(formation.Count(piece => piece == 0) == 5, "only camps start empty");
        }

        var invalid = ArmyGame.CreateFormation(201);
        int flag = Array.IndexOf(invalid, (int)ArmyPieceKind.Flag);
        Swap(invalid, flag, 0);
        Check(!ArmyGame.IsFormationValid(invalid), "a flag cannot stand on the front line");
        Check(!ArmyGame.IsFormationValid(null), "null formation is rejected");
        Check(!ArmyGame.IsFormationValid(new int[31]), "formation length must be exact");
        Check(ArmyGame.FormationPoint(0, 0) == 30 && ArmyGame.FormationPoint(0, 29) == 59,
            "side zero advances north");
        Check(ArmyGame.FormationPoint(1, 0) == 25 && ArmyGame.FormationPoint(1, 29) == 4,
            "side one advances south");
    }

    private static void TopologyUsesCampsAndTheThreeBorderBridges()
    {
        Check(ArmyGame.IsCamp(36) && ArmyGame.IsRoad(30, 36), "diagonal road connects a camp");
        Check(ArmyGame.IsRoad(36, 42), "diagonal road can connect two camps");
        Check(!ArmyGame.IsRoad(31, 37), "ordinary points do not have diagonal roads");
        Check(ArmyGame.IsRail(25, 30) && ArmyGame.IsRail(27, 32) && ArmyGame.IsRail(29, 34),
            "three rail bridges cross the border");
        Check(!ArmyGame.IsRoad(26, 31) && !ArmyGame.IsRoad(28, 33), "mountain gaps cannot be crossed");
        Check(!ArmyGame.IsRail(1, 6) && ArmyGame.IsHeadquarters(1), "headquarters is outside the railway");
        int[] kinds = new int[60];
        int[] sides = Enumerable.Repeat(-1, 60).ToArray();
        kinds[1] = (int)ArmyPieceKind.Marshal;
        sides[1] = 0;
        kinds[16] = (int)ArmyPieceKind.General;
        sides[16] = 0;
        kinds[17] = (int)ArmyPieceKind.Engineer;
        sides[17] = 1;
        var moves = ArmyBoard.LegalMoves(kinds, sides, 0);
        Check(!moves.Any(move => move.From == 1), "a piece in headquarters cannot leave");
        Check(!moves.Contains(new BoardMove(16, 17)), "a piece in a camp cannot be attacked");

        kinds[16] = (int)ArmyPieceKind.Mine;
        Check(!ArmyBoard.HasLegalMove(kinds, sides, 0), "mine and headquarters occupant cannot make a move");
    }

    private static void FlagCaptureEndsTheMatch()
    {
        var attacker = FormationWithFront(ArmyPieceKind.Marshal, ArmyPieceKind.Company);
        var defender = new int[ArmyGame.FormationSlotCount];
        defender[0] = defender[5] = defender[10] = (int)ArmyPieceKind.Engineer;
        defender[15] = defender[20] = defender[25] = (int)ArmyPieceKind.Platoon;
        defender[26] = (int)ArmyPieceKind.Flag;
        CompleteFormation(defender);
        var game = new ArmyGame(attacker, defender);
        var leftRail = new[] { 30, 25, 20, 15, 10, 5, 0, 1 };
        for (int step = 1; step < leftRail.Length - 1; step++)
        {
            Check(game.TryPlay(new BoardMove(leftRail[step - 1], leftRail[step])), "marshal takes next guard");
            bool oddStep = step % 2 == 1;
            Check(game.TryPlay(oddStep ? new BoardMove(26, 21) : new BoardMove(21, 26)), "other side moves a separate piece");
        }

        Check(game.PieceAt(1, 0).Kind == -1, "flag is still concealed before capture");
        var position = game.Copy();
        var capture = new BoardMove(0, 1);
        Check(position.TryPlay(capture) && position.Winner == 0, "capturing flag ends a legal match");
        Check(game.Winner == BoardResult.Ongoing && game.PieceAt(1, 1).Kind == (int)ArmyPieceKind.Flag,
            "copy can be played without changing the match");
        Check(game.TryPlay(capture) && game.Winner == 0, "live match also records flag capture");
        Check(game.Status.Contains("夺取军旗") && game.LegalMoves().Count == 0 &&
            game.CaptureSearch()(BoardDifficulty.Hard, 11, CancellationToken.None).Equals(BoardMove.None),
            "terminal game gives a public outcome and rejects further play");
    }

    private static void EngineersCanTurnWithoutPassingPieces()
    {
        int[] kinds = new int[60];
        int[] sides = Enumerable.Repeat(-1, 60).ToArray();
        int from = 6;
        kinds[from] = (int)ArmyPieceKind.Engineer;
        sides[from] = 0;
        Check(ArmyBoard.LegalMoves(kinds, sides, 0).Contains(new BoardMove(from, 15)),
            "engineer can turn at rail corner");
        kinds[from] = (int)ArmyPieceKind.Marshal;
        Check(!ArmyBoard.LegalMoves(kinds, sides, 0).Contains(new BoardMove(from, 15)),
            "marshal cannot turn at rail corner");
        kinds[from] = (int)ArmyPieceKind.Engineer;
        kinds[5] = kinds[7] = (int)ArmyPieceKind.General;
        sides[5] = 0;
        sides[7] = 1;
        var moves = ArmyBoard.LegalMoves(kinds, sides, 0);
        Check(moves.Contains(new BoardMove(from, 7)), "engineer can capture the first enemy on a railway");
        Check(!moves.Contains(new BoardMove(from, 8)) && !moves.Contains(new BoardMove(from, 15)),
            "engineer cannot pass any occupied point");
    }

    private static void CombatResolvesRanksAndSpecialPieces()
    {
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Marshal, ArmyPieceKind.General) == ArmyCombatOutcome.AttackerSurvives,
            "higher rank captures lower rank");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Brigade, ArmyPieceKind.Brigade) == ArmyCombatOutcome.BothRemoved,
            "equal ranks are both removed");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Engineer, ArmyPieceKind.Mine) == ArmyCombatOutcome.AttackerSurvives,
            "engineer clears mine");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Marshal, ArmyPieceKind.Mine) == ArmyCombatOutcome.DefenderSurvives,
            "only engineers and bombs can eliminate mines");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Bomb, ArmyPieceKind.Mine) == ArmyCombatOutcome.BothRemoved,
            "bomb and mine are both removed");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Bomb, ArmyPieceKind.Flag) == ArmyCombatOutcome.BothRemoved,
            "bomb and flag are both removed");
        Check(ArmyGame.CombatOutcome(ArmyPieceKind.Engineer, ArmyPieceKind.Flag) == ArmyCombatOutcome.AttackerSurvives,
            "any movable rank can capture flag");
    }

    private static void MarshalLossRevealsOnlyTheFlags()
    {
        var initial = FormationWithFront(ArmyPieceKind.Marshal, ArmyPieceKind.Company);
        var game = new ArmyGame(initial, initial);
        int flagSlot = Array.IndexOf(initial, (int)ArmyPieceKind.Flag);
        Check(game.PieceAt(25, 0).Kind == -1 && game.PieceAt(25, 1).Kind == (int)ArmyPieceKind.Marshal,
            "only the owner sees the marshal");
        Check(!game.TryPlay(new BoardMove(25, 30)), "current player cannot move enemy pieces");
        Check(!game.TryPlay(new BoardMove(30, 25, 1)), "military chess moves cannot include a choice");
        Check(game.TryPlay(new BoardMove(30, 25)), "opposing front ranks can fight across a bridge");
        Check(game.LastOutcome == ArmyCombatOutcome.BothRemoved, "public combat outcome reports only survivors");
        Check(game.PieceAt(25, 0).Kind == 0 && game.PieceAt(30, 1).Kind == 0, "both marshals removed");
        for (int side = 0; side < 2; side++)
        {
            Check(game.IsFlagRevealed(side), "lost marshal reveals flag");
            Check(game.PieceAt(ArmyGame.FormationPoint(side, flagSlot), 1 - side).Kind == (int)ArmyPieceKind.Flag,
                "flag rank revealed to its opponent");
            Check(game.PieceAt(ArmyGame.FormationPoint(side, 1), 1 - side).Kind == -1,
                "surviving non-flag rank remains hidden");
        }
    }

    private static void QuietTurnsDrawAfterSixtyPlies()
    {
        var initial = ArmyGame.CreateFormation(9);
        var game = new ArmyGame(initial, initial);
        var cycle = new[] {new BoardMove(31, 36), new BoardMove(26, 21), new BoardMove(36, 31), new BoardMove(21, 26)};
        for (int ply = 0; ply < 60; ply++)
        {
            Check(game.Winner == BoardResult.Ongoing && game.TryPlay(cycle[ply % cycle.Length]),
                "each quiet turn is legal while both sides can move");
        }
        Check(game.Winner == BoardResult.Draw && game.LegalMoves().Count == 0, "60 quiet plies draw the game");
    }

    private static void BeliefSamplesRespectPublicConstraintsAndInventory()
    {
        var domains = Enumerable.Range(0, ArmyGame.FormationSlotCount).Select(ArmyBoard.FormationDomain).ToArray();
        domains[0] = ArmyBoard.KindBit(ArmyPieceKind.Engineer);
        domains[1] = ArmyBoard.KindBit(ArmyPieceKind.General) | ArmyBoard.KindBit(ArmyPieceKind.Division);
        domains[26] = ArmyBoard.KindBit(ArmyPieceKind.Flag);
        var beliefs = ArmyBeliefs.Estimate(domains);
        Check(beliefs[0, (int)ArmyPieceKind.Engineer] == 1, "deduced engineer has certain rank");
        Check(beliefs[1, (int)ArmyPieceKind.Marshal] == 0, "an excluded rank stays impossible");
        var unique = new System.Collections.Generic.HashSet<string>();
        for (int seed = 0; seed < 10; seed++)
        {
            var sample = ArmyBeliefs.Sample(domains, beliefs, new Random(seed));
            Check(ArmyGame.IsFormationValid(sample), "each sample allocates a complete, legal rank inventory");
            for (int slot = 0; slot < domains.Length; slot++)
                if (domains[slot] != 0)
                    Check((domains[slot] & (1 << sample[slot])) != 0, "each sampled rank agrees with public deductions");
            unique.Add(string.Join(",", sample));
        }
        Check(unique.Count > 1, "uncertain ranks produce varied sample layouts");
    }

    private static void SearchCannotDistinguishEquivalentPrivateFormations()
    {
        var mine = FormationWithFront(ArmyPieceKind.Engineer, ArmyPieceKind.Company);
        var theirs = FormationWithFront(ArmyPieceKind.General, ArmyPieceKind.Division);
        var swapped = (int[])theirs.Clone();
        Swap(swapped, 0, 1);
        var first = new ArmyGame(mine, theirs);
        var second = new ArmyGame(mine, swapped);
        AssertSearchEquivalent(first, second, "initial formation");
        foreach (var game in new[] {first, second})
        {
            Check(game.TryPlay(new BoardMove(30, 25)) && game.LastOutcome == ArmyCombatOutcome.DefenderSurvives,
                "both hidden defenders win the same public combat");
            Check(game.TryPlay(new BoardMove(25, 30)), "the same hidden piece then moves to a new square");
        }
        AssertSearchEquivalent(first, second, "public combat and enemy movement");
    }

    private static void CapturedSearchIsStableAndCancellable()
    {
        var initial = ArmyGame.CreateFormation(501);
        var game = new ArmyGame(initial, initial);
        var legal = game.LegalMoves();
        var choose = game.CaptureSearch();
        var before = choose(BoardDifficulty.Hard, 987, CancellationToken.None);
        Check(legal.Contains(before), "search returned a legal opening move");
        Check(game.TryPlay(before), "live game can advance after the capture");
        Check(before.Equals(choose(BoardDifficulty.Hard, 987, CancellationToken.None)), "snapshot is independent of live match");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            try
            {
                choose(BoardDifficulty.Hard, 987, cancel.Token);
                throw new Exception("pre-cancelled search should stop");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static void AssertSearchEquivalent(ArmyGame first, ArmyGame second, string scenario)
    {
        var firstLegal = first.LegalMoves();
        var secondLegal = second.LegalMoves();
        Check(firstLegal.SequenceEqual(secondLegal), scenario + " has identical visible legal moves");
        for (int point = 0; point < first.PointCount; point++)
        {
            var a = first.PieceAt(point, first.CurrentSide);
            var b = second.PieceAt(point, second.CurrentSide);
            Check(a.Kind == b.Kind && a.Side == b.Side, scenario + " has identical projection");
        }

        var firstSearch = first.CaptureSearch();
        var secondSearch = second.CaptureSearch();
        foreach (BoardDifficulty difficulty in Enum.GetValues(typeof(BoardDifficulty)))
        {
            for (int seed = 1; seed <= 2; seed++)
            {
                var move = firstSearch(difficulty, seed, CancellationToken.None);
                Check(firstLegal.Contains(move), scenario + " produces a legal move at " + difficulty);
                Check(move.Equals(secondSearch(difficulty, seed, CancellationToken.None)), scenario + " produces the same search result at " + difficulty);
            }
        }
    }

    private static int[] FormationWithFront(ArmyPieceKind first, ArmyPieceKind second)
    {
        var formation = ArmyGame.CreateFormation(416);
        Swap(formation, 0, Array.IndexOf(formation, (int)first));
        Swap(formation, 1, Array.IndexOf(formation, (int)second));
        Check(ArmyGame.IsFormationValid(formation), "front fixture preserves setup restrictions");
        return formation;
    }

    private static void CompleteFormation(int[] partial)
    {
        var remaining = (int[])ArmyBoard.Inventory.Clone();
        foreach (int kind in partial)
            if (kind > 0) remaining[kind]--;
        for (int kind = ArmyBoard.HighestKind; kind > 0; kind--)
        {
            for (int slot = 0; slot < partial.Length && remaining[kind] > 0; slot++)
            {
                if (partial[slot] != 0 || (ArmyBoard.FormationDomain(slot) & (1 << kind)) == 0) continue;
                partial[slot] = kind;
                remaining[kind]--;
            }
        }
        Check(ArmyGame.IsFormationValid(partial), "could complete the requested partial formation");
    }

    private static void Swap(int[] values, int a, int b)
    {
        int held = values[a];
        values[a] = values[b];
        values[b] = held;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
    }
}
