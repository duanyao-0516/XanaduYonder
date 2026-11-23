using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    public enum ArmyPieceKind
    {
        Empty = 0,
        Engineer = 1,
        Platoon = 2,
        Company = 3,
        Battalion = 4,
        Regiment = 5,
        Brigade = 6,
        Division = 7,
        General = 8,
        Marshal = 9,
        Bomb = 10,
        Mine = 11,
        Flag = 12
    }

    public enum ArmyCombatOutcome { None, AttackerSurvives, DefenderSurvives, BothRemoved }

    // Two-player concealed Junqi. The 60-ply quiet draw follows Hi8's two-player rules.
    // https://hi8.com/g/erguojunqi
    public sealed class ArmyGame : IBoardPosition
    {
        public const int FormationSlotCount = 30;
        public const int QuietPlyLimit = 60;

        private readonly int[] kinds = new int[ArmyBoard.PointCount];
        private readonly int[] sides = new int[ArmyBoard.PointCount];
        private readonly int[] slots = new int[ArmyBoard.PointCount];
        private readonly int[] flagPoints = new int[2];
        private readonly bool[] revealedFlags = new bool[2];
        private readonly int[][] observedEnemyRanks = { new int[FormationSlotCount], new int[FormationSlotCount] };
        private readonly BoardMove[] previousMoves = { BoardMove.None, BoardMove.None };
        private int quietPlies;
        private bool wonByFlag;

        public int CurrentSide { get; private set; }
        public int Winner { get; private set; } = BoardResult.Ongoing;
        public int PointCount => ArmyBoard.PointCount;
        public ArmyCombatOutcome LastOutcome { get; private set; }

        public string Status
        {
            get
            {
                if (Winner == BoardResult.Draw) return "连续 60 步未碰子，和棋";
                if (Winner >= 0) return (wonByFlag ? "夺取军旗，" : "对方无棋可走，") + SideName(Winner) + "获胜";

                string turn = "轮到" + SideName(CurrentSide) + "行棋";
                switch (LastOutcome)
                {
                    case ArmyCombatOutcome.AttackerSurvives: return "进攻方存活 · " + turn;
                    case ArmyCombatOutcome.DefenderSurvives: return "防守方存活 · " + turn;
                    case ArmyCombatOutcome.BothRemoved: return "双方棋子移除 · " + turn;
                    default: return turn;
                }
            }
        }

        public ArmyGame(int[] first, int[] second)
        {
            if (!IsFormationValid(first)) throw new ArgumentException("Invalid first formation.", nameof(first));
            if (!IsFormationValid(second)) throw new ArgumentException("Invalid second formation.", nameof(second));
            for (int point = 0; point < PointCount; point++) Remove(point);
            LoadFormation(0, first);
            LoadFormation(1, second);
        }

        private ArmyGame(ArmyGame game)
        {
            game.kinds.CopyTo(kinds, 0);
            game.sides.CopyTo(sides, 0);
            game.slots.CopyTo(slots, 0);
            game.flagPoints.CopyTo(flagPoints, 0);
            game.revealedFlags.CopyTo(revealedFlags, 0);
            game.observedEnemyRanks[0].CopyTo(observedEnemyRanks[0], 0);
            game.observedEnemyRanks[1].CopyTo(observedEnemyRanks[1], 0);
            game.previousMoves.CopyTo(previousMoves, 0);
            CurrentSide = game.CurrentSide;
            Winner = game.Winner;
            LastOutcome = game.LastOutcome;
            quietPlies = game.quietPlies;
            wonByFlag = game.wonByFlag;
        }

        public static bool IsCamp(int point) => ArmyBoard.IsCamp(point);
        public static bool IsHeadquarters(int point) => ArmyBoard.IsHeadquarters(point);
        public static bool IsRoad(int from, int to) => ArmyBoard.IsRoad(from, to);
        public static bool IsRail(int from, int to) => ArmyBoard.IsRail(from, to);

        public static int FormationPoint(int side, int slot)
        {
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side));
            if (slot < 0 || slot >= FormationSlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            int rank = slot / ArmyBoard.Columns;
            int row = side == 0 ? 6 + rank : 5 - rank;
            return row * ArmyBoard.Columns + slot % ArmyBoard.Columns;
        }

        public static bool IsFormationValid(int[] formation)
        {
            if (formation == null || formation.Length != FormationSlotCount) return false;
            var counts = new int[ArmyBoard.Inventory.Length];

            for (int slot = 0; slot < formation.Length; slot++)
            {
                int kind = formation[slot];
                int domain = ArmyBoard.FormationDomain(slot);
                if (domain == 0)
                {
                    if (kind != 0) return false;
                    continue;
                }
                if (kind < 1 || kind > ArmyBoard.HighestKind || (domain & (1 << kind)) == 0) return false;
                if (++counts[kind] > ArmyBoard.Inventory[kind]) return false;
            }

            for (int kind = 1; kind < counts.Length; kind++)
                if (counts[kind] != ArmyBoard.Inventory[kind]) return false;
            return true;
        }

        public static int[] CreateFormation(int seed) => FillFormation(new int[FormationSlotCount], new Random(seed));

        // Creates a reduced board for tutorial exercises, with both flags in the left headquarters.
        public static ArmyGame CreateTutorialPosition(params (int Point, int Side, ArmyPieceKind Kind)[] pieces)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            const int leftHeadquartersSlot = FormationSlotCount - ArmyBoard.Columns + 1;
            var formations = new[] { new int[FormationSlotCount], new int[FormationSlotCount] };
            var supply = new[] { (int[])ArmyBoard.Inventory.Clone(), (int[])ArmyBoard.Inventory.Clone() };
            var occupied = new bool[ArmyBoard.PointCount];
            var mineSlots = new int[ArmyBoard.PointCount];
            for (int side = 0; side < 2; side++)
            {
                formations[side][leftHeadquartersSlot] = (int)ArmyPieceKind.Flag;
                occupied[FormationPoint(side, leftHeadquartersSlot)] = true;
            }
            foreach (var piece in pieces)
            {
                int kind = (int)piece.Kind;
                if (!ArmyBoard.IsPoint(piece.Point) || piece.Side < 0 || piece.Side > 1 || occupied[piece.Point] ||
                    kind <= 0 || kind >= (int)ArmyPieceKind.Flag || --supply[piece.Side][kind] < 0)
                    throw new ArgumentException("Invalid tutorial piece or placement.", nameof(pieces));
                occupied[piece.Point] = true;
                if (piece.Kind != ArmyPieceKind.Mine) continue;

                int row = piece.Point / ArmyBoard.Columns;
                int depth = piece.Side == 0 ? row - 6 : 5 - row;
                if (depth < 4 || depth >= 6) throw new ArgumentException("A tutorial mine must stay in its own back two rows.", nameof(pieces));
                int slot = depth * ArmyBoard.Columns + piece.Point % ArmyBoard.Columns;
                mineSlots[piece.Point] = slot;
                formations[piece.Side][slot] = kind;
            }

            var demo = new ArmyGame(FillFormation(formations[0], new Random(121)), FillFormation(formations[1], new Random(245)));
            for (int point = 0; point < demo.PointCount; point++)
                if (point != demo.flagPoints[0] && point != demo.flagPoints[1]) demo.Remove(point);

            foreach (var piece in pieces)
            {
                int kind = (int)piece.Kind;
                int slot = piece.Kind == ArmyPieceKind.Mine ? mineSlots[piece.Point] : Array.IndexOf(formations[piece.Side], kind);
                demo.kinds[piece.Point] = kind;
                demo.sides[piece.Point] = piece.Side;
                demo.slots[piece.Point] = slot;
                if (piece.Point != FormationPoint(piece.Side, slot))
                    demo.observedEnemyRanks[1 - piece.Side][slot] &= ArmyBoard.MovableKinds;
                formations[piece.Side][slot] = 0;
            }
            if (!ArmyBoard.HasLegalMove(demo.kinds, demo.sides, 0) || !ArmyBoard.HasLegalMove(demo.kinds, demo.sides, 1))
                throw new ArgumentException("Both sides need an opening tutorial move.", nameof(pieces));
            return demo;
        }

        private static int[] FillFormation(int[] formation, Random random)
        {
            var remaining = (int[])ArmyBoard.Inventory.Clone();
            foreach (int kind in formation) if (kind != 0) remaining[kind]--;
            for (int kind = ArmyBoard.HighestKind; kind >= 1; kind--)
            {
                var available = new List<int>();
                for (int slot = 0; slot < formation.Length; slot++)
                    if (formation[slot] == 0 && (ArmyBoard.FormationDomain(slot) & (1 << kind)) != 0) available.Add(slot);

                for (int count = 0; count < remaining[kind]; count++)
                {
                    int next = random.Next(available.Count);
                    formation[available[next]] = kind;
                    available.RemoveAt(next);
                }
            }
            return formation;
        }

        public static ArmyCombatOutcome CombatOutcome(ArmyPieceKind attacker, ArmyPieceKind defender)
        {
            if (defender == ArmyPieceKind.Empty) return ArmyCombatOutcome.None;
            if (attacker == ArmyPieceKind.Bomb || defender == ArmyPieceKind.Bomb || attacker == defender)
                return ArmyCombatOutcome.BothRemoved;
            if (defender == ArmyPieceKind.Flag || defender == ArmyPieceKind.Mine && attacker == ArmyPieceKind.Engineer)
                return ArmyCombatOutcome.AttackerSurvives;
            if (defender == ArmyPieceKind.Mine || attacker < defender) return ArmyCombatOutcome.DefenderSurvives;
            return ArmyCombatOutcome.AttackerSurvives;
        }

        public bool IsFlagRevealed(int side) => side >= 0 && side < revealedFlags.Length && revealedFlags[side];

        public BoardPiece PieceAt(int point, int viewer)
        {
            if (!ArmyBoard.IsPoint(point) || sides[point] < 0) return new BoardPiece(-1, 0);
            int side = sides[point];
            bool publicFlag = revealedFlags[side] && flagPoints[side] == point;
            return new BoardPiece(side, viewer == side || publicFlag ? kinds[point] : -1);
        }

        public IReadOnlyList<BoardMove> LegalMoves()
        {
            if (Winner != BoardResult.Ongoing) return Array.Empty<BoardMove>();
            return ArmyBoard.LegalMoves(kinds, sides, CurrentSide);
        }

        public bool TryPlay(BoardMove move)
        {
            int from = move.From;
            int to = move.To;
            if (Winner != BoardResult.Ongoing || move.Choice != 0 || move.PathLength != 0 ||
                !ArmyBoard.IsPoint(from) || !ArmyBoard.IsPoint(to) || sides[from] != CurrentSide ||
                (ArmyBoard.Destinations(from, kinds[from], sides, CurrentSide) & (1UL << to)) == 0) return false;

            int attackerSide = CurrentSide;
            int defenderSide = 1 - attackerSide;
            ArmyPieceKind attackKind = (ArmyPieceKind)kinds[from];
            ArmyPieceKind defendKind = (ArmyPieceKind)kinds[to];
            int attackerSlot = slots[from];
            bool requiresEngineer = ArmyBoard.RequiresEngineer(from, to, sides, attackerSide);
            observedEnemyRanks[defenderSide][attackerSlot] &= requiresEngineer ? ArmyBoard.KindBit(ArmyPieceKind.Engineer) :
                ArmyBoard.MovableKinds;

            LastOutcome = CombatOutcome(attackKind, defendKind);
            quietPlies = LastOutcome == ArmyCombatOutcome.None ? quietPlies + 1 : 0;
            previousMoves[attackerSide] = move;

            if (LastOutcome != ArmyCombatOutcome.None)
            {
                bool attackerDied = LastOutcome != ArmyCombatOutcome.AttackerSurvives;
                bool defenderDied = LastOutcome != ArmyCombatOutcome.DefenderSurvives;
                bool attackMarshalLost = attackerDied && attackKind == ArmyPieceKind.Marshal;
                bool defendMarshalLost = defenderDied && defendKind == ArmyPieceKind.Marshal;
                RecordCombat(defenderSide, attackerSlot, true, defendKind, attackerDied, attackMarshalLost);
                RecordCombat(attackerSide, slots[to], false, attackKind, defenderDied, defendMarshalLost);
                if (attackMarshalLost) RevealFlag(attackerSide);
                if (defendMarshalLost) RevealFlag(defenderSide);
                wonByFlag = defendKind == ArmyPieceKind.Flag;
            }

            if (LastOutcome == ArmyCombatOutcome.None || LastOutcome == ArmyCombatOutcome.AttackerSurvives)
            {
                kinds[to] = kinds[from];
                sides[to] = sides[from];
                slots[to] = slots[from];
            }
            else if (LastOutcome == ArmyCombatOutcome.BothRemoved)
            {
                Remove(to);
            }
            Remove(from);

            CurrentSide = defenderSide;
            if (wonByFlag || !ArmyBoard.HasLegalMove(kinds, sides, defenderSide)) Winner = attackerSide;
            else if (quietPlies >= QuietPlyLimit) Winner = BoardResult.Draw;
            return true;
        }

        public IBoardPosition Copy() => new ArmyGame(this);

        public BoardSearch CaptureSearch()
        {
            if (Winner != BoardResult.Ongoing) return ArmySearch.NoMove;

            int viewer = CurrentSide;
            var visibleRanks = new int[PointCount];
            for (int point = 0; point < PointCount; point++) visibleRanks[point] = PieceAt(point, viewer).Kind;

            // Piece IDs come from the visible initial occupancy and public move coordinates.
            // The worker receives the projection and deductions of this seat only.
            var snapshot = new ArmySearch(viewer, visibleRanks, (int[])sides.Clone(), (int[])slots.Clone(),
                (int[])observedEnemyRanks[viewer].Clone(), previousMoves[viewer]);
            return snapshot.Choose;
        }

        private void LoadFormation(int side, int[] formation)
        {
            for (int slot = 0; slot < formation.Length; slot++)
            {
                observedEnemyRanks[1 - side][slot] = ArmyBoard.FormationDomain(slot);
                int kind = formation[slot];
                if (kind == 0) continue;
                int point = FormationPoint(side, slot);
                kinds[point] = kind;
                sides[point] = side;
                slots[point] = slot;
                if (kind == (int)ArmyPieceKind.Flag) flagPoints[side] = point;
            }
        }

        private void RecordCombat(int viewer, int enemySlot, bool enemyAttacked, ArmyPieceKind ownKind,
            bool enemyDied, bool enemyMarshalLost)
        {
            int possible = observedEnemyRanks[viewer][enemySlot];
            for (int kind = 1; kind <= ArmyBoard.HighestKind; kind++)
            {
                int bit = 1 << kind;
                if ((possible & bit) == 0) continue;
                var enemy = (ArmyPieceKind)kind;
                var outcome = enemyAttacked ? CombatOutcome(enemy, ownKind) : CombatOutcome(ownKind, enemy);
                if (outcome != LastOutcome || enemyDied && (enemy == ArmyPieceKind.Marshal) != enemyMarshalLost)
                    possible &= ~bit;
            }
            observedEnemyRanks[viewer][enemySlot] = possible;
        }

        private void RevealFlag(int side)
        {
            revealedFlags[side] = true;
            int slot = slots[flagPoints[side]];
            observedEnemyRanks[1 - side][slot] = ArmyBoard.KindBit(ArmyPieceKind.Flag);
        }

        private void Remove(int point)
        {
            kinds[point] = 0;
            sides[point] = -1;
            slots[point] = -1;
        }

        private static string SideName(int side) => side == 0 ? "红方" : "绿方";
    }
}
