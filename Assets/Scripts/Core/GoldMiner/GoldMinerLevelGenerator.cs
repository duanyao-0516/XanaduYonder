using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
namespace XanaduYonder.Core.GoldMiner
{
    [Serializable]
    public sealed class GoldMinerWitnessInput
    {
        public int Tick, PlayerId;
        public string Action = "launch";
    }
    [Serializable]
    public sealed class GoldMinerWitness
    {
        public long RequiredIncome, ExpectedIncome;
        public int EndTick;
        public GoldMinerWitnessInput[] Inputs;
    }
    [Serializable]
    public sealed class GoldMinerLayout
    {
        public string Id, PatternId;
        public uint VariantSeed;
        public GoldMinerEntity[] Entities;
        public GoldMinerWitness Witness;
    }
    public sealed class GoldMinerGenerationAttempt
    {
        public int CandidateIndex, ExpandedStates;
        public uint Seed;
        public string FailureReason;
        public double PlanningElapsedSeconds;
    }
    /// <summary>Offline only. The runtime selects the shipped, proven catalog instead.</summary>
    public static class GoldMinerLevelGenerator
    {
        public const int CandidateBudget = 32;
        public static readonly string[] Patterns = { "balanced", "left", "right" };
        static readonly GoldMinerKind[] StaticOrder = { GoldMinerKind.RockLarge, GoldMinerKind.GoldLarge, GoldMinerKind.GoldMedium, GoldMinerKind.RockSmall, GoldMinerKind.GoldSmall, GoldMinerKind.Diamond, GoldMinerKind.MysteryBag };
        sealed class Cell { internal int Column; internal double X, Y; }
        public static GoldMinerLayout Generate(GoldMinerMode mode, int level, string pattern, int variant, uint seed, out int candidates, out int expanded,
            int expansionBudget = GoldMinerPlanner.ExpansionBudget, Action<GoldMinerGenerationAttempt> onAttempt = null)
        {
            if (expansionBudget < 0 || expansionBudget > GoldMinerPlanner.ExpansionBudget) throw new ArgumentOutOfRangeException("expansionBudget");
            if (seed == 0) throw new ArgumentOutOfRangeException("seed");
            candidates = 0; expanded = 0;
            for (int attempt = 0; attempt < CandidateBudget; attempt++)
            {
                candidates++;
                uint candidateSeed = GoldMinerRules.NextUInt(ref seed);
                var layout = CreateCandidate(mode, level, pattern, variant, candidateSeed);
                if (layout == null)
                {
                    if (onAttempt != null) onAttempt(new GoldMinerGenerationAttempt { CandidateIndex = candidates, Seed = candidateSeed, FailureReason = "geometry-placement-rejected" });
                    continue;
                }
                int used;
                var planning = Stopwatch.StartNew();
                layout.Witness = GoldMinerPlanner.FindWitness(mode, level, layout, expansionBudget, out used);
                planning.Stop();
                expanded += used;
                if (onAttempt != null) onAttempt(new GoldMinerGenerationAttempt {
                    CandidateIndex = candidates, Seed = candidateSeed, ExpandedStates = used,
                    PlanningElapsedSeconds = planning.Elapsed.TotalSeconds,
                    FailureReason = layout.Witness != null ? null : used >= expansionBudget ? "search-expansion-budget-exhausted" : "no-witness-within-enumerated-actions"
                });
                if (layout.Witness != null) return layout;
            }
            throw new InvalidOperationException("No proven layout within 32 candidates: " + Identity(mode, level, pattern, variant));
        }
        public static string Identity(GoldMinerMode mode, int level, string pattern, int variant)
        {
            GoldMinerRules.Target(mode, level);
            if (Array.IndexOf(Patterns, pattern) < 0 || variant < 0 || variant > 3) throw new ArgumentException("Invalid pattern or variant.");
            return GoldMinerRules.ModeId(mode) + "-" + level.ToString("00", CultureInfo.InvariantCulture) + "-" + pattern + "-" + variant;
        }
        public static GoldMinerLayout CreateCandidate(GoldMinerMode mode, int level, string pattern, int variant, uint seed)
        {
            string id = Identity(mode, level, pattern, variant);
            if (seed == 0) throw new ArgumentOutOfRangeException("seed");
            uint rng = seed;
            int columns = mode == GoldMinerMode.Solo ? 6 : 10;
            double width = GoldMinerRules.Width(mode), height = GoldMinerRules.Height(mode);
            var shuffled = new List<Cell>();
            for (int row = 0; row < 5; row++) for (int column = 0; column < columns; column++)
                shuffled.Add(new Cell { Column = column, X = (column + .5) * width / columns, Y = 1.2 + row * (height - 1.8) / 4 });
            for (int i = shuffled.Count - 1; i > 0; i--) { int j = (int)(GoldMinerRules.NextUInt(ref rng) % (uint)(i + 1)); var c = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = c; }
            // Jitter is drawn once for each shuffled cell, never used to reshuffle a pattern.
            foreach (var cell in shuffled) { cell.X += Jitter(ref rng); cell.Y += Jitter(ref rng); }
            var order = new List<Cell>();
            if (pattern == "balanced")
            {
                var even = shuffled.FindAll(c => c.Column % 2 == 0); var odd = shuffled.FindAll(c => c.Column % 2 != 0);
                for (int i = 0; i < Math.Max(even.Count, odd.Count); i++) { if (i < even.Count) order.Add(even[i]); if (i < odd.Count) order.Add(odd[i]); }
            }
            else order.AddRange(shuffled);
            var heavy = new List<Cell>(shuffled);
            // Stable insertion ordering keeps each column's Fisher-Yates order intact.
            heavy.Sort((a, b) => { int n = (pattern == "right" ? -1 : 1) * a.Column.CompareTo(b.Column); return n != 0 ? n : shuffled.IndexOf(a).CompareTo(shuffled.IndexOf(b)); });
            var used = new HashSet<Cell>(); var entities = new List<GoldMinerEntity>();
            foreach (var kind in StaticOrder)
            {
                var points = pattern != "balanced" && (kind == GoldMinerKind.RockLarge || kind == GoldMinerKind.GoldLarge) ? heavy : order;
                for (int n = 0; n < GoldMinerRules.Count(mode, level, kind); n++)
                {
                    GoldMinerEntity entity = null;
                    foreach (var cell in points)
                    {
                        if (used.Contains(cell)) continue;
                        var trial = new GoldMinerEntity { Id = entities.Count + 1, Kind = kind, Status = GoldMinerEntityStatus.Available, X = cell.X, Y = cell.Y, BagValue = kind == GoldMinerKind.MysteryBag ? (long?)100 : null };
                        if (!Fits(trial, entities, mode, .08, .04)) continue;
                        used.Add(cell); entity = trial; break;
                    }
                    if (entity == null) return null;
                    entities.Add(entity);
                }
            }
            int animals = GoldMinerRules.Count(mode, level, GoldMinerKind.Mole) + GoldMinerRules.Count(mode, level, GoldMinerKind.DiamondMole);
            for (int i = 0; i < animals; i++)
            {
                var kind = i < GoldMinerRules.Count(mode, level, GoldMinerKind.Mole) ? GoldMinerKind.Mole : GoldMinerKind.DiamondMole;
                double radius = GoldMinerRules.Radius(kind); GoldMinerEntity entity = null;
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    var trial = new GoldMinerEntity { Id = entities.Count + 1, Kind = kind, Status = GoldMinerEntityStatus.Available, MoveDirection = i % 2 == 0 ? -1 : 1, Y = 1.8 + i * (height - 2.4) / Math.Max(1, animals - 1), X = radius + Unit(ref rng) * (width - 2 * radius) };
                    if (Fits(trial, entities, mode, 0, 0)) { entity = trial; break; }
                }
                if (entity == null) return null;
                entities.Add(entity);
            }
            var result = new GoldMinerLayout { Id = id, PatternId = pattern, VariantSeed = seed, Entities = entities.ToArray() };
            string reason;
            return ValidateGeometry(mode, level, result, out reason) ? result : null;
        }
        public static bool ValidateGeometry(GoldMinerMode mode, int level, GoldMinerLayout layout, out string reason)
        {
            reason = null;
            if (layout == null || layout.Entities == null || layout.Entities.Length == 0 || layout.Entities.Length > GoldMinerRules.MaxEntities) { reason = "Missing entities."; return false; }
            var placed = new List<GoldMinerEntity>(); var counts = new int[9];
            foreach (var entity in layout.Entities)
            {
                if (entity == null || (int)entity.Kind < 0 || (int)entity.Kind > 8 || entity.Id != placed.Count + 1 || entity.Status != GoldMinerEntityStatus.Available || entity.OwnerPlayerId != null) { reason = "Invalid entity identity/state."; return false; }
                bool animal = GoldMinerRules.IsAnimal(entity.Kind);
                if (!GoldMinerRules.IsFinite(entity.X) || !GoldMinerRules.IsFinite(entity.Y) || !Fits(entity, placed, mode, animal ? 0 : .08, animal ? 0 : .04)) { reason = "Boundary or initial overlap."; return false; }
                bool reachable = false;
                for (int player = 0; player < GoldMinerRules.PlayerCount(mode); player++)
                    reachable |= Math.Abs(Math.Atan2(entity.X - GoldMinerRules.OriginX(mode, player), entity.Y)) <= GoldMinerRules.MaxAngle;
                if (!reachable) { reason = "Outside hook angular reach."; return false; }
                if (entity.Kind == GoldMinerKind.MysteryBag ? entity.BagValue != 100 : entity.BagValue != null) { reason = "Baseline bag value."; return false; }
                if (animal ? (entity.MoveDirection != 1 && entity.MoveDirection != -1) : entity.MoveDirection != 0) { reason = "Invalid movement."; return false; }
                counts[(int)entity.Kind]++; placed.Add(entity);
            }
            for (int kind = 0; kind < 9; kind++) if (counts[kind] != GoldMinerRules.Count(mode, level, (GoldMinerKind)kind)) { reason = "Wrong stock count."; return false; }
            return true;
        }
        static bool Fits(GoldMinerEntity e, List<GoldMinerEntity> placed, GoldMinerMode mode, double gap, double inset)
        {
            double r = GoldMinerRules.Radius(e.Kind);
            if (e.X < r + inset || e.X > GoldMinerRules.Width(mode) - r - inset || e.Y < r + inset || e.Y > GoldMinerRules.Height(mode) - r - inset) return false;
            foreach (var old in placed) { double x = old.X - e.X, y = old.Y - e.Y, minimum = r + GoldMinerRules.Radius(old.Kind) + gap; if (x * x + y * y < minimum * minimum) return false; }
            return true;
        }
        static double Unit(ref uint state) { return GoldMinerRules.NextUInt(ref state) / 4294967296.0; }
        static double Jitter(ref uint state) { return Unit(ref state) * .08 - .04; }
        public static GoldMinerRunSnapshot CreateProofRun(GoldMinerMode mode, int level, GoldMinerLayout layout)
        {
            long opening = Math.Max(0, GoldMinerRules.Target(mode, level) - GoldMinerRules.RequiredIncome(mode, level));
            var players = new GoldMinerOpeningPlayer[GoldMinerRules.PlayerCount(mode)];
            for (int i = 0; i < players.Length; i++) players[i] = new GoldMinerOpeningPlayer { PlayerId = i, Bombs = 0, TotalCollected = i == 0 ? opening : 0 };
            var start = new GoldMinerLevelStart { LevelIndex = level, CatalogVersion = 1, LayoutProfileId = GoldMinerRules.LayoutProfileId(mode), LayoutId = layout.Id, LayoutContentHash = new string('0', 64), PatternId = layout.PatternId, PreviousPatternId = level == 1 ? null : layout.PatternId == "balanced" ? "left" : "balanced", LevelLayoutSeed = layout.VariantSeed, LayoutRng = 1, BagRng = 1, OpeningCash = opening, OpeningPlayers = players, ActiveBuffs = new GoldMinerBuffs(), InitialHooks = GoldMinerRules.InitialHooks(mode), InitialEntities = GoldMinerCopies.Array(layout.Entities, e => e.Copy()) };
            return new GoldMinerRunSnapshot { RunId = new string('1', 32), Mode = mode, RunSeed = layout.VariantSeed, Checkpoint = start, Current = start.CreateState(mode) };
        }
    }
}
