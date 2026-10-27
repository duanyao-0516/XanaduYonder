using System;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>The sole owner of result accounting; board, frozen time and records commit together.</summary>
    public sealed class MinesweeperProfile
    {
        private MinesweeperSaveData data;
        public int SelectedDifficulty
        {
            get { return data.SelectedDifficulty; }
            set { MinesweeperRules.Get(value); data.SelectedDifficulty = value; }
        }
        public bool TutorialSeen { get { return data.TutorialSeen; } set { data.TutorialSeen = value; } }
        public MinesweeperProfile() { data = new MinesweeperSaveData(); }
        public MinesweeperProfile(MinesweeperSaveData saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (!saved.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(saved));
            data = saved.Copy();
        }
        public MinesweeperSessionData GetSlot(int difficulty)
        { MinesweeperRules.Get(difficulty); return data.Slots[difficulty] == null ? null : data.Slots[difficulty].Copy(); }
        public MinesweeperRecord GetRecord(int difficulty) { MinesweeperRules.Get(difficulty); return data.Records[difficulty].Copy(); }
        public MinesweeperSaveData Export() { return data.Copy(); }
        public MinesweeperSaveData Capture(MinesweeperSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var slot = session.Export(); int difficulty = slot.Board.Difficulty;
            var next = data.Copy(); bool newBest = false;
            bool settleResult = session.IsTerminal && !slot.ResultRecorded;
            // Also recognize a separately restored instance of this already-accounted session.
            var existing = next.Slots[difficulty];
            if (existing != null && existing.SessionId == slot.SessionId && existing.ResultRecorded) settleResult = false;
            if (settleResult)
            {
                var record = next.Records[difficulty];
                if (slot.Board.State == MinesweeperState.Won)
                {
                    record.Wins = MinesweeperSession.SaturatingAdd(record.Wins, 1);
                    newBest = !record.BestMilliseconds.HasValue || slot.ElapsedMilliseconds < record.BestMilliseconds.Value;
                    if (newBest) record.BestMilliseconds = slot.ElapsedMilliseconds;
                }
                else record.Losses = MinesweeperSession.SaturatingAdd(record.Losses, 1);
            }
            if (session.IsTerminal) slot.ResultRecorded = true;
            next.Slots[difficulty] = slot;
            if (!next.TryValidate(out var reason)) throw new InvalidOperationException(reason);
            data = next;
            if (session.IsTerminal && !session.Export().ResultRecorded) session.MarkResultRecorded(newBest);
            return data.Copy();
        }
    }
}
