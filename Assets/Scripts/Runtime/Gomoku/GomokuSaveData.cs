using System;
using System.Collections.Generic;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    /// <summary>Boundary DTO. Sessions copy and validate it before taking ownership.</summary>
    public sealed class GomokuSessionData
    {
        public string SessionId;
        public GomokuMode Mode;
        public GomokuDifficulty Difficulty;
        public GomokuStone HumanStone;
        public int BlackPlayer;
        public int[] Moves;

        public GomokuSessionData Copy()
        {
            return new GomokuSessionData { SessionId = SessionId, Mode = Mode, Difficulty = Difficulty,
                HumanStone = HumanStone, BlackPlayer = BlackPlayer, Moves = Moves == null ? null : (int[])Moves.Clone() };
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (!Guid.TryParseExact(SessionId, "D", out var id) || id == Guid.Empty || id.ToString("D") != SessionId)
                return Fail("Session ID must be a canonical nonempty GUID.", out reason);
            if (!ValidMode(Mode) || !ValidDifficulty(Difficulty) || !ValidStone(HumanStone) || !ValidPlayer(BlackPlayer))
                return Fail("Unknown Gomoku session options.", out reason);
            if (Moves == null || Moves.Length > 225) return Fail("Missing or oversized Gomoku move history.", out reason);
            try { GomokuGame.Restore(Moves); }
            catch (ArgumentException exception) { return Fail(exception.Message, out reason); }
            return true;
        }

        internal static bool ValidMode(GomokuMode mode) { return mode == GomokuMode.HumanVsAi || mode == GomokuMode.Local; }
        internal static bool ValidDifficulty(GomokuDifficulty difficulty) { return difficulty >= GomokuDifficulty.Easy && difficulty <= GomokuDifficulty.Hard; }
        internal static bool ValidStone(GomokuStone stone) { return stone == GomokuStone.Black || stone == GomokuStone.White; }
        internal static bool ValidPlayer(int player) { return player == 1 || player == 2; }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }

    public sealed class GomokuSaveData
    {
        public GomokuMode SelectedMode = GomokuMode.HumanVsAi;
        public GomokuDifficulty PreferredDifficulty = GomokuDifficulty.Normal;
        public GomokuStone PreferredHumanStone = GomokuStone.Black;
        public int PreferredBlackPlayer = 1;
        public GomokuSessionData[] Slots = new GomokuSessionData[2];

        public GomokuSaveData Copy()
        {
            var copy = new GomokuSaveData { SelectedMode = SelectedMode, PreferredDifficulty = PreferredDifficulty,
                PreferredHumanStone = PreferredHumanStone, PreferredBlackPlayer = PreferredBlackPlayer,
                Slots = Slots == null ? null : new GomokuSessionData[Slots.Length] };
            if (Slots != null) for (int i = 0; i < Slots.Length; i++) copy.Slots[i] = Slots[i] == null ? null : Slots[i].Copy();
            return copy;
        }

        public bool TryValidate(out string reason)
        {
            reason = null;
            if (!GomokuSessionData.ValidMode(SelectedMode) || !GomokuSessionData.ValidDifficulty(PreferredDifficulty) ||
                !GomokuSessionData.ValidStone(PreferredHumanStone) || !GomokuSessionData.ValidPlayer(PreferredBlackPlayer) || Slots == null || Slots.Length != 2)
            { reason = "Invalid Gomoku preferences or slot shape."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                if (slot == null) continue;
                if (!slot.TryValidate(out reason)) return false;
                if ((int)slot.Mode != i || !ids.Add(slot.SessionId))
                { reason = "Wrong Gomoku slot mode or duplicate session ID."; return false; }
            }
            return true;
        }
    }
}
