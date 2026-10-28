using System;

namespace XanaduYonder.Core.Sheep
{
    [Serializable]
    public sealed class SheepSnapshot
    {
        public int Level, LayoutId;
        public SheepMode Mode;
        public SheepDifficulty Difficulty = SheepDifficulty.Normal;
        public int Wave = 1;
        public long Score;
        public int[] Kinds;
        public SheepLocation[] Locations;
        public int[] Tray, Reserve;
        public uint RandomState;
        public int RemoveLimit = 2, UndoLimit = 3, ShuffleLimit = 1;
        public int RemoveRemaining = 2, UndoRemaining = 3, ShuffleRemaining = 1;
        public bool ReviveUsed;
        public int LastPickedId = -1, LastSource = -1, LastReserveIndex = -1;

        public SheepSnapshot Copy()
        {
            return new SheepSnapshot {
                Level = Level, LayoutId = LayoutId, Mode = Mode, Difficulty = Difficulty, Wave = Wave, Score = Score,
                Kinds = Kinds == null ? null : (int[])Kinds.Clone(),
                Locations = Locations == null ? null : (SheepLocation[])Locations.Clone(),
                Tray = Tray == null ? null : (int[])Tray.Clone(),
                Reserve = Reserve == null ? null : (int[])Reserve.Clone(),
                RandomState = RandomState, ReviveUsed = ReviveUsed,
                RemoveLimit = RemoveLimit, UndoLimit = UndoLimit, ShuffleLimit = ShuffleLimit,
                RemoveRemaining = RemoveRemaining, UndoRemaining = UndoRemaining, ShuffleRemaining = ShuffleRemaining,
                LastPickedId = LastPickedId, LastSource = LastSource, LastReserveIndex = LastReserveIndex
            };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (Mode < SheepMode.Challenge || Mode > SheepMode.Tutorial ||
                Difficulty < SheepDifficulty.Easy || Difficulty > SheepDifficulty.Extreme)
                return Invalid("Unsupported mode or difficulty.", out error);
            if (Level != (Mode == SheepMode.Tutorial ? 1 : 2) ||
                (Mode != SheepMode.Challenge && Difficulty != SheepDifficulty.Normal))
                return Invalid("Mode, level and difficulty disagree.", out error);
            if (Wave < 1 || (Mode != SheepMode.Endless && Wave != 1)) return Invalid("Invalid wave.", out error);
            if (!ValidQuota(RemoveLimit, RemoveRemaining) || !ValidQuota(UndoLimit, UndoRemaining) || !ValidQuota(ShuffleLimit, ShuffleRemaining))
                return Invalid("Invalid tool quota.", out error);
            if (!SheepBoardLayout.IsSupported(Level, LayoutId)) return Invalid("Unsupported layout.", out error);
            if (RandomState == 0) return Invalid("Random state must be nonzero.", out error);
            int total = Level == 1 ? 18 : 144, kindCount = SheepLevelGenerator.GetKindCount(Mode, Difficulty);
            if (Kinds == null || Locations == null || Tray == null || Reserve == null) return Invalid("Card arrays are required.", out error);
            if (Kinds.Length != total || Locations.Length != total) return Invalid("Unexpected card count.", out error);
            if (Tray.Length > 7 || Reserve.Length > 6) return Invalid("Storage capacity exceeded.", out error);
            var totals = new int[kindCount];
            var removed = new int[kindCount];
            var trayKinds = new int[kindCount];
            int outsideBoard = 0, cleared = 0;
            for (int id = 0; id < total; id++)
            {
                int kind = Kinds[id];
                if (kind < 0 || kind >= kindCount) return Invalid("Unknown card kind.", out error);
                if (Locations[id] < SheepLocation.Board || Locations[id] > SheepLocation.Removed) return Invalid("Unknown card location.", out error);
                totals[kind]++;
                if (Locations[id] == SheepLocation.Removed) { removed[kind]++; cleared++; }
                if (Locations[id] == SheepLocation.Tray) trayKinds[kind]++;
                if (Locations[id] != SheepLocation.Board) outsideBoard++;
            }
            for (int kind = 0; kind < kindCount; kind++)
            {
                if (totals[kind] != total / kindCount) return Invalid("Kind totals do not match the level.", out error);
                if (removed[kind] % 3 != 0) return Invalid("Removed cards must form complete triples.", out error);
                if (trayKinds[kind] >= 3) return Invalid("A settled tray cannot contain an unresolved triple.", out error);
            }
            long expectedScore = cleared + (Mode == SheepMode.Endless ? ((long)Wave - 1) * 144 : 0);
            if (Score != expectedScore) return Invalid("Score disagrees with eliminated cards and wave.", out error);
            var seen = new bool[total];
            if (!ValidateList(Tray, SheepLocation.Tray, seen) || !ValidateList(Reserve, SheepLocation.Reserve, seen))
                return Invalid("Card lists contain duplicate, missing, or misplaced ids.", out error);
            for (int id = 0; id < total; id++)
                if ((Locations[id] == SheepLocation.Tray || Locations[id] == SheepLocation.Reserve) != seen[id])
                    return Invalid("Card locations disagree with storage lists.", out error);
            int removeUses = RemoveLimit - RemoveRemaining;
            int reserveLimit = Math.Min(6, removeUses * 3 + (ReviveUsed ? 3 : 0));
            if (Reserve.Length > reserveLimit || (Wave == 1 && removeUses > 0 && outsideBoard == 0) ||
                (ReviveUsed && (Mode == SheepMode.Tutorial || (Wave == 1 && outsideBoard < 7))))
                return Invalid("Tool history disagrees with moved cards.", out error);
            if (LastPickedId == -1)
            {
                if (LastSource != -1 || LastReserveIndex != -1) return Invalid("Empty undo state contains a source.", out error);
            }
            else
            {
                if (LastPickedId < 0 || LastPickedId >= total || Locations[LastPickedId] != SheepLocation.Tray)
                    return Invalid("Undo card must still be in the tray.", out error);
                int pickedIndex = Array.IndexOf(Tray, LastPickedId);
                for (int i = pickedIndex + 1; i < Tray.Length; i++)
                    if (Kinds[Tray[i]] == Kinds[LastPickedId]) return Invalid("Undo must refer to the latest card of its kind.", out error);
                if (LastSource == (int)SheepLocation.Board)
                {
                    if (LastReserveIndex != -1 || !SheepBoardLayout.IsExposed(SheepBoardLayout.GetPositions(Level, LayoutId), Locations, LastPickedId))
                        return Invalid("Undo board source is covered or has a reserve index.", out error);
                    if (Wave == 1 && ((removeUses > 0 && outsideBoard < 2) || (ReviveUsed && outsideBoard < 8)))
                        return Invalid("Undo would erase the card history required by tool usage.", out error);
                }
                else if (LastSource == (int)SheepLocation.Reserve)
                {
                    if (Reserve.Length >= reserveLimit || LastReserveIndex < 0 || LastReserveIndex > Reserve.Length)
                        return Invalid("Undo reserve source is invalid.", out error);
                }
                else return Invalid("Undo source is invalid.", out error);
            }
            return true;
        }

        private static bool ValidQuota(int limit, int remaining) { return limit >= 0 && limit <= 99 && remaining >= 0 && remaining <= limit; }

        private bool ValidateList(int[] ids, SheepLocation expected, bool[] seen)
        {
            foreach (int id in ids)
            {
                if (id < 0 || id >= Locations.Length || seen[id] || Locations[id] != expected) return false;
                seen[id] = true;
            }
            return true;
        }

        private static bool Invalid(string message, out string error) { error = message; return false; }
    }
}
