using System;
using System.Collections.Generic;
using System.Text;

namespace XanaduYonder.Core.Sokoban
{
    /// <summary>The sole grid-rule owner. History contains one entry for every committed action.</summary>
    public sealed class SokobanGame
    {
        public const int MaximumMoves = 50000;
        private struct ActionRecord { internal int Player, BoxFrom, BoxTo; internal char Direction; }
        private readonly List<ActionRecord> history = new List<ActionRecord>();
        private readonly bool[] boxes;
        public SokobanLevel Level { get; private set; }
        public int Player { get; private set; }
        public int Moves { get { return history.Count; } }
        public int Pushes { get; private set; }
        public bool Won { get { return GoalsFilled == Level.BoxCount; } }
        public bool CanUndo { get { return history.Count > 0 && !Won; } }
        public bool AtMoveLimit { get { return Moves >= MaximumMoves; } }
        public int GoalsFilled { get; private set; }
        public string Actions { get { var result = new StringBuilder(history.Count); foreach (var action in history) result.Append(action.Direction); return result.ToString(); } }
        public SokobanGame(SokobanLevel level)
        { Level = level ?? throw new ArgumentNullException(nameof(level)); boxes = new bool[level.Width * level.Height]; Restart(); }
        public bool HasBox(int index) { return index >= 0 && index < boxes.Length && boxes[index]; }
        public SokobanMoveResult Move(Direction direction)
        {
            if (Won) return new SokobanMoveResult(SokobanMoveReason.Terminal);
            if (AtMoveLimit) return new SokobanMoveResult(SokobanMoveReason.MoveLimit);
            int next = Level.Neighbor(Player, direction); if (Level.IsWall(next)) return new SokobanMoveResult(SokobanMoveReason.Blocked);
            bool push = HasBox(next); int destination = push ? Level.Neighbor(next, direction) : -1;
            if (push && (Level.IsWall(destination) || HasBox(destination))) return new SokobanMoveResult(SokobanMoveReason.Blocked);
            history.Add(new ActionRecord { Player = Player, BoxFrom = push ? next : -1, BoxTo = destination, Direction = ToChar(direction) });
            if (push) { boxes[next] = false; boxes[destination] = true; Pushes++; if (Level.IsGoal(next)) GoalsFilled--; if (Level.IsGoal(destination)) GoalsFilled++; }
            Player = next;
            return new SokobanMoveResult(SokobanMoveReason.Moved, push, push && Level.IsGoal(destination), push && Level.IsStaticCorner(destination));
        }
        public bool Undo()
        {
            if (!CanUndo) return false; var action = history[history.Count - 1]; history.RemoveAt(history.Count - 1); Player = action.Player;
            if (action.BoxFrom >= 0) { boxes[action.BoxTo] = false; boxes[action.BoxFrom] = true; Pushes--; if (Level.IsGoal(action.BoxTo)) GoalsFilled--; if (Level.IsGoal(action.BoxFrom)) GoalsFilled++; }
            return true;
        }
        public void Restart()
        { Array.Clear(boxes, 0, boxes.Length); history.Clear(); Pushes = GoalsFilled = 0; Player = Level.InitialPlayer; foreach (int box in Level.InitialBoxes) { boxes[box] = true; if (Level.IsGoal(box)) GoalsFilled++; } }
        public SokobanSnapshot GetSnapshot()
        { var positions = new int[Level.BoxCount]; int n = 0; for (int i = 0; i < boxes.Length; i++) if (boxes[i]) positions[n++] = i; return new SokobanSnapshot(Level, Player, positions, Moves, Pushes, Won, Actions); }
        public static Direction FromChar(char direction)
        { switch (direction) { case 'U': return Direction.Up; case 'D': return Direction.Down; case 'L': return Direction.Left; case 'R': return Direction.Right; default: throw new ArgumentException("Unknown direction."); } }
        private static char ToChar(Direction direction)
        { switch (direction) { case Direction.Up: return 'U'; case Direction.Down: return 'D'; case Direction.Left: return 'L'; case Direction.Right: return 'R'; default: throw new ArgumentException("Unknown direction."); } }
    }
}
