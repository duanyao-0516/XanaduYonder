using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Minesweeper
{
    public sealed class MinesweeperGame
    {
        private readonly IRandomSource random;
        private readonly bool[] mines, revealed, flags;
        private readonly int difficulty;
        private bool generated;
        private int? firstReveal, detonated;
        public MinesweeperState State { get; private set; }
        public int Width { get { return MinesweeperRules.Get(difficulty).Width; } }
        public int Height { get { return MinesweeperRules.Get(difficulty).Height; } }
        public int MineCount { get { return MinesweeperRules.Get(difficulty).MineCount; } }
        public int FlagCount { get { int count = 0; foreach (bool flag in flags) if (flag) count++; return count; } }
        public int RemainingMarkers { get { return MineCount - FlagCount; } }
        public MinesweeperGame(int difficulty, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            MinesweeperRules.Get(difficulty); this.difficulty = difficulty; this.random = random;
            mines = new bool[Width * Height]; revealed = new bool[mines.Length]; flags = new bool[mines.Length];
        }
        public MinesweeperGame(MinesweeperSnapshot snapshot, IRandomSource random)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(snapshot));
            if (random == null) throw new ArgumentNullException(nameof(random));
            difficulty = snapshot.Difficulty; this.random = random; mines = snapshot.Mines; revealed = snapshot.Revealed; flags = snapshot.Flags;
            generated = snapshot.Generated; firstReveal = snapshot.FirstRevealIndex; detonated = snapshot.DetonatedIndex; State = snapshot.State;
        }
        public MinesweeperSnapshot GetSnapshot() { return new MinesweeperSnapshot(difficulty, generated, firstReveal, State, detonated, mines, revealed, flags); }
        public MinesweeperMoveResult Reveal(int index)
        {
            if (!Allowed(index, out var rejected)) return rejected;
            if (flags[index]) return Result(MinesweeperMoveReason.Flagged);
            if (revealed[index]) return Chord(index);
            if (!generated) Generate(index);
            if (mines[index]) return Lose(index);
            var changed = Expand(new[] { index });
            CheckWin(); return Result(MinesweeperMoveReason.None, changed);
        }
        public MinesweeperMoveResult ToggleFlag(int index)
        {
            if (!Allowed(index, out var rejected)) return rejected;
            if (revealed[index]) return Result(MinesweeperMoveReason.None);
            flags[index] = !flags[index]; return Result(MinesweeperMoveReason.None, new[] { index });
        }
        public MinesweeperMoveResult Chord(int index)
        {
            if (!Allowed(index, out var rejected)) return rejected;
            if (!revealed[index]) return Result(MinesweeperMoveReason.None);
            int number = Adjacent(index);
            if (number == 0) return Result(MinesweeperMoveReason.None);
            int flagCount = 0; var targets = new List<int>();
            foreach (int neighbor in MinesweeperRules.Neighbors(index, Width, Height))
            { if (flags[neighbor]) flagCount++; else if (!revealed[neighbor]) targets.Add(neighbor); }
            if (flagCount != number) return Result(MinesweeperMoveReason.FlagMismatch);
            // Neighbor enumeration is row-major. Resolve every mine before committing any safe reveal.
            foreach (int target in targets) if (mines[target]) return Lose(target);
            var changed = Expand(targets); CheckWin(); return Result(MinesweeperMoveReason.None, changed);
        }
        private bool Allowed(int index, out MinesweeperMoveResult rejected)
        {
            rejected = null;
            if (State == MinesweeperState.Won || State == MinesweeperState.Lost) rejected = Result(MinesweeperMoveReason.Terminal);
            else if (index < 0 || index >= mines.Length) rejected = Result(MinesweeperMoveReason.Invalid);
            return rejected == null;
        }
        private void Generate(int first)
        {
            var excluded = new bool[mines.Length]; excluded[first] = true;
            foreach (int neighbor in MinesweeperRules.Neighbors(first, Width, Height)) excluded[neighbor] = true;
            var candidates = new List<int>();
            for (int i = 0; i < mines.Length; i++) if (!excluded[i]) candidates.Add(i);
            for (int i = 0; i < MineCount; i++)
            {
                int chosen = i + random.Next(candidates.Count - i);
                int temp = candidates[i]; candidates[i] = candidates[chosen]; candidates[chosen] = temp;
                mines[candidates[i]] = true;
            }
            generated = true; firstReveal = first; State = MinesweeperState.Active;
        }
        private int Adjacent(int index)
        { int count = 0; foreach (int neighbor in MinesweeperRules.Neighbors(index, Width, Height)) if (mines[neighbor]) count++; return count; }
        private int[] Expand(IEnumerable<int> starts)
        {
            var changed = new List<int>(); var queue = new Queue<int>(); var visited = new bool[mines.Length];
            foreach (int start in starts) queue.Enqueue(start);
            while (queue.Count != 0)
            {
                int index = queue.Dequeue();
                if (visited[index]) continue; visited[index] = true;
                if (flags[index] || revealed[index] || mines[index]) continue;
                revealed[index] = true; changed.Add(index);
                if (Adjacent(index) == 0)
                    foreach (int neighbor in MinesweeperRules.Neighbors(index, Width, Height)) if (!visited[neighbor]) queue.Enqueue(neighbor);
            }
            return changed.ToArray();
        }
        private void CheckWin()
        { for (int i = 0; i < mines.Length; i++) if (!mines[i] && !revealed[i]) return; State = MinesweeperState.Won; }
        private MinesweeperMoveResult Lose(int index)
        { revealed[index] = true; detonated = index; State = MinesweeperState.Lost; return Result(MinesweeperMoveReason.None, new[] { index }); }
        private MinesweeperMoveResult Result(MinesweeperMoveReason reason, int[] changed = null)
        { return new MinesweeperMoveResult(State, reason, changed); }
    }
}
