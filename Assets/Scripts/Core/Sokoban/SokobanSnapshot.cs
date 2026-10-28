using System;

namespace XanaduYonder.Core.Sokoban
{
    public sealed class SokobanSnapshot
    {
        private readonly int[] boxes;
        public SokobanLevel Level { get; private set; }
        public int Player { get; private set; }
        public int[] Boxes { get { return (int[])boxes.Clone(); } }
        public int Moves { get; private set; }
        public int Pushes { get; private set; }
        public bool Won { get; private set; }
        public string Actions { get; private set; }
        internal SokobanSnapshot(SokobanLevel level, int player, int[] boxes, int moves, int pushes, bool won, string actions)
        { Level = level; Player = player; this.boxes = (int[])boxes.Clone(); Moves = moves; Pushes = pushes; Won = won; Actions = actions; }
        public bool HasBox(int index) { return Array.IndexOf(boxes, index) >= 0; }
    }
    public enum SokobanMoveReason { Moved, Blocked, Terminal, MoveLimit }
    public sealed class SokobanMoveResult
    {
        public bool Changed { get; private set; }
        public bool Pushed { get; private set; }
        public bool ReachedGoal { get; private set; }
        public bool StaticCorner { get; private set; }
        public SokobanMoveReason Reason { get; private set; }
        public SokobanMoveResult(SokobanMoveReason reason, bool pushed = false, bool reachedGoal = false, bool staticCorner = false)
        { Reason = reason; Changed = reason == SokobanMoveReason.Moved; Pushed = pushed; ReachedGoal = reachedGoal; StaticCorner = staticCorner; }
    }
}
