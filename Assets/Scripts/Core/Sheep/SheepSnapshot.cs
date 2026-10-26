using System;

namespace XanaduYonder.Core.Sheep
{
    [Serializable]
    public sealed class SheepSnapshot
    {
        public int Level, LayoutId;
        public int[] Kinds;
        public SheepLocation[] Locations;
        public int[] Tray, Reserve;
        public uint RandomState;
        public bool RemoveUsed, UndoUsed, ShuffleUsed, ReviveUsed;
        public int LastPickedId = -1, LastSource = -1, LastReserveIndex = -1;

        public SheepSnapshot Copy()
        {
            return new SheepSnapshot {
                Level = Level, LayoutId = LayoutId,
                Kinds = Kinds == null ? null : (int[])Kinds.Clone(),
                Locations = Locations == null ? null : (SheepLocation[])Locations.Clone(),
                Tray = Tray == null ? null : (int[])Tray.Clone(),
                Reserve = Reserve == null ? null : (int[])Reserve.Clone(),
                RandomState = RandomState, RemoveUsed = RemoveUsed, UndoUsed = UndoUsed,
                ShuffleUsed = ShuffleUsed, ReviveUsed = ReviveUsed,
                LastPickedId = LastPickedId, LastSource = LastSource, LastReserveIndex = LastReserveIndex
            };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (Level < 1 || Level > 2) return Invalid("Unsupported level.", out error);
            if (LayoutId < 0 || LayoutId > 2) return Invalid("Unsupported layout.", out error);
            if (RandomState == 0) return Invalid("Random state must be nonzero.", out error);
            int total = Level == 1 ? 18 : 144, kindCount = Level == 1 ? 3 : 16;
            if (Kinds == null || Locations == null || Tray == null || Reserve == null) return Invalid("Card arrays are required.", out error);
            if (Kinds.Length != total || Locations.Length != total) return Invalid("Unexpected card count.", out error);
            if (Tray.Length > 7 || Reserve.Length > 6) return Invalid("Storage capacity exceeded.", out error);
            var totals = new int[kindCount];
            var removed = new int[kindCount];
            var trayKinds = new int[kindCount];
            int outsideBoard = 0;
            for (int id = 0; id < total; id++)
            {
                int kind = Kinds[id];
                if (kind < 0 || kind >= kindCount) return Invalid("Unknown card kind.", out error);
                if (Locations[id] < SheepLocation.Board || Locations[id] > SheepLocation.Removed) return Invalid("Unknown card location.", out error);
                totals[kind]++;
                if (Locations[id] == SheepLocation.Removed) removed[kind]++;
                if (Locations[id] == SheepLocation.Tray) trayKinds[kind]++;
                if (Locations[id] != SheepLocation.Board) outsideBoard++;
            }
            for (int kind = 0; kind < kindCount; kind++)
            {
                if (totals[kind] != (Level == 1 ? 6 : 9)) return Invalid("Kind totals do not match the level.", out error);
                if (removed[kind] % 3 != 0) return Invalid("Removed cards must form complete triples.", out error);
                if (trayKinds[kind] >= 3) return Invalid("A settled tray cannot contain an unresolved triple.", out error);
            }
            var seen = new bool[total];
            if (!ValidateList(Tray, SheepLocation.Tray, seen) || !ValidateList(Reserve, SheepLocation.Reserve, seen))
                return Invalid("Card lists contain duplicate, missing, or misplaced ids.", out error);
            for (int id = 0; id < total; id++)
                if ((Locations[id] == SheepLocation.Tray || Locations[id] == SheepLocation.Reserve) != seen[id])
                    return Invalid("Card locations disagree with storage lists.", out error);
            int reserveLimit = (RemoveUsed ? 3 : 0) + (ReviveUsed ? 3 : 0);
            if (Reserve.Length > reserveLimit || (RemoveUsed && outsideBoard == 0) || (ReviveUsed && (Level == 1 || outsideBoard < 7)))
                return Invalid("Tool flags disagree with moved cards.", out error);
            if (LastPickedId == -1)
            {
                if (LastSource != -1 || LastReserveIndex != -1) return Invalid("Empty undo state contains a source.", out error);
            }
            else
            {
                if (LastPickedId < 0 || LastPickedId >= total || Locations[LastPickedId] != SheepLocation.Tray)
                    return Invalid("Undo card must still be in the tray.", out error);
                if (LastSource == (int)SheepLocation.Board)
                {
                    if (LastReserveIndex != -1 || !SheepBoardLayout.IsExposed(SheepBoardLayout.GetPositions(Level, LayoutId), Locations, LastPickedId))
                        return Invalid("Undo board source is covered or has a reserve index.", out error);
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
