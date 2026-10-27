using System;

namespace XanaduYonder.Core.Sheep
{
    public enum SheepMode { Challenge = 0, Endless = 1, Tutorial = 2 }
    public enum SheepDifficulty { Easy = 0, Normal = 1, Hard = 2, Extreme = 3 }
    public enum SheepLocation { Board = 0, Tray = 1, Reserve = 2, Removed = 3 }
    public enum SheepPhase { Playing, Lost, Won }
    public enum SheepTool { RemoveThree, Undo, Shuffle, Revive }

    public struct SheepPosition
    {
        public int Id, X, Y, Layer, BlindStack;
    }

    public sealed class SheepMove
    {
        public bool Changed { get; private set; }
        public int PickedId { get; private set; }
        public int[] MatchedIds { get; private set; }
        public SheepPhase Phase { get; private set; }

        internal SheepMove(bool changed, int pickedId, int[] matchedIds, SheepPhase phase)
        {
            Changed = changed; PickedId = pickedId; MatchedIds = matchedIds; Phase = phase;
        }
    }

    public sealed class GeneratedSheepBoard
    {
        public SheepSnapshot Snapshot { get; private set; }
        public int[] SolutionIds { get; private set; }

        internal GeneratedSheepBoard(SheepSnapshot snapshot, int[] solutionIds)
        {
            Snapshot = snapshot; SolutionIds = solutionIds;
        }
    }
}
