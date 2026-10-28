using System;
using System.Collections.Generic;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    public enum MinesweeperInputMode { Reveal, Flag }

    public sealed class MinesweeperSessionData
    {
        public string SessionId;
        public MinesweeperSnapshot Board;
        public long ElapsedMilliseconds;
        public MinesweeperInputMode InputMode;
        public bool Fullscreen, ResultRecorded;
        public double ZoomScale = 1, CenterColumn, CenterRow;
        public MinesweeperSessionData Copy()
        {
            return new MinesweeperSessionData { SessionId = SessionId, Board = Board == null ? null : Board.Copy(),
                ElapsedMilliseconds = ElapsedMilliseconds, InputMode = InputMode, Fullscreen = Fullscreen, ResultRecorded = ResultRecorded,
                ZoomScale = ZoomScale, CenterColumn = CenterColumn, CenterRow = CenterRow };
        }
        public bool TryValidate(out string reason, bool allowUnrecordedTerminal = false)
        {
            reason = null;
            if (!Guid.TryParseExact(SessionId, "D", out var id) || id == Guid.Empty || id.ToString("D") != SessionId)
                return Fail("Session ID must be a canonical nonempty GUID.", out reason);
            if (Board == null) return Fail("Missing board.", out reason);
            if (!Board.TryValidate(out reason)) return false;
            bool terminal = Board.State == MinesweeperState.Won || Board.State == MinesweeperState.Lost;
            if ((!terminal && ResultRecorded) || (terminal && !ResultRecorded && !allowUnrecordedTerminal))
                return Fail("Recorded result disagrees with board state.", out reason);
            if (ElapsedMilliseconds < 0 || (Board.State == MinesweeperState.Ready && ElapsedMilliseconds != 0))
                return Fail("Invalid elapsed time.", out reason);
            if (InputMode != MinesweeperInputMode.Reveal && InputMode != MinesweeperInputMode.Flag)
                return Fail("Unknown input mode.", out reason);
            if (!Finite(ZoomScale) || ZoomScale < .5 || ZoomScale > 2 || !Finite(CenterColumn) || CenterColumn < 0 || CenterColumn > Board.Width ||
                !Finite(CenterRow) || CenterRow < 0 || CenterRow > Board.Height) return Fail("Invalid normal viewport.", out reason);
            return true;
        }
        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool Fail(string value, out string reason) { reason = value; return false; }
    }

    public sealed class MinesweeperRecord
    {
        public long Wins, Losses;
        public long? BestMilliseconds;
        public MinesweeperRecord Copy() { return new MinesweeperRecord { Wins = Wins, Losses = Losses, BestMilliseconds = BestMilliseconds }; }
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (Wins < 0 || Losses < 0 || (BestMilliseconds.HasValue && BestMilliseconds.Value < 0) ||
                (Wins == 0) != !BestMilliseconds.HasValue) { reason = "Invalid minesweeper record."; return false; }
            return true;
        }
    }

    /// <summary>Mutable boundary DTO. Profile and session never expose their owned instance.</summary>
    public sealed class MinesweeperSaveData
    {
        public int SelectedDifficulty;
        public bool TutorialSeen;
        public MinesweeperSessionData[] Slots = new MinesweeperSessionData[3];
        public MinesweeperRecord[] Records = { new MinesweeperRecord(), new MinesweeperRecord(), new MinesweeperRecord() };
        public MinesweeperSaveData Copy()
        {
            var copy = new MinesweeperSaveData { SelectedDifficulty = SelectedDifficulty, TutorialSeen = TutorialSeen,
                Slots = Slots == null ? null : new MinesweeperSessionData[Slots.Length], Records = Records == null ? null : new MinesweeperRecord[Records.Length] };
            if (Slots != null) for (int i = 0; i < Slots.Length; i++) copy.Slots[i] = Slots[i] == null ? null : Slots[i].Copy();
            if (Records != null) for (int i = 0; i < Records.Length; i++) copy.Records[i] = Records[i] == null ? null : Records[i].Copy();
            return copy;
        }
        public bool TryValidate(out string reason)
        {
            reason = null;
            if (SelectedDifficulty < 0 || SelectedDifficulty > 2 || Slots == null || Slots.Length != 3 || Records == null || Records.Length != 3)
                return Fail("Invalid profile shape or difficulty.", out reason);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 3; i++)
            {
                if (Records[i] == null) return Fail("Missing difficulty record.", out reason);
                if (!Records[i].TryValidate(out reason)) return false;
                var slot = Slots[i]; if (slot == null) continue;
                if (!slot.TryValidate(out reason)) return false;
                if (slot.Board.Difficulty != i || !ids.Add(slot.SessionId)) return Fail("Wrong slot difficulty or duplicate session ID.", out reason);
                if (slot.Board.State == MinesweeperState.Won && (Records[i].Wins == 0 || Records[i].BestMilliseconds.Value > slot.ElapsedMilliseconds))
                    return Fail("Win slot disagrees with its record.", out reason);
                if (slot.Board.State == MinesweeperState.Lost && Records[i].Losses == 0) return Fail("Loss slot has no matching record.", out reason);
            }
            return true;
        }
        private static bool Fail(string value, out string reason) { reason = value; return false; }
    }
}
