using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    public sealed class SheepGame
    {
        private SheepSnapshot state;
        private readonly SheepPosition[] positions;

        public SheepGame(int level, uint seed) : this(SheepLevelGenerator.Generate(level, seed).Snapshot) { }

        public SheepGame(SheepSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string error;
            if (!snapshot.TryValidate(out error)) throw new ArgumentException(error, "snapshot");
            state = snapshot.Copy();
            positions = SheepBoardLayout.GetPositions(state.Level, state.LayoutId);
        }

        public SheepSnapshot GetSnapshot() { return state.Copy(); }

        public SheepPhase Phase
        {
            get { return RemainingCount == 0 ? SheepPhase.Won : state.Tray.Length == 7 ? SheepPhase.Lost : SheepPhase.Playing; }
        }

        public int RemainingCount
        {
            get { int count = 0; foreach (SheepLocation location in state.Locations) if (location != SheepLocation.Removed) count++; return count; }
        }

        public int ClearedCount { get { return state.Kinds.Length - RemainingCount; } }

        public bool CanPick(int id)
        {
            if (Phase != SheepPhase.Playing || id < 0 || id >= state.Kinds.Length) return false;
            if (state.Locations[id] == SheepLocation.Reserve) return true;
            return state.Locations[id] == SheepLocation.Board && SheepBoardLayout.IsExposed(positions, state.Locations, id);
        }

        public SheepMove Pick(int id)
        {
            if (!CanPick(id)) return new SheepMove(false, id, new int[0], Phase);
            var next = state.Copy();
            next.LastPickedId = id;
            next.LastSource = (int)next.Locations[id];
            next.LastReserveIndex = -1;
            if (next.Locations[id] == SheepLocation.Reserve)
            {
                var reserve = new List<int>(next.Reserve);
                next.LastReserveIndex = reserve.IndexOf(id);
                reserve.RemoveAt(next.LastReserveIndex);
                next.Reserve = reserve.ToArray();
            }
            var tray = new List<int>(next.Tray);
            int insertion = tray.Count;
            for (int i = 0; i < tray.Count; i++) if (next.Kinds[tray[i]] == next.Kinds[id]) insertion = i + 1;
            tray.Insert(insertion, id);
            next.Locations[id] = SheepLocation.Tray;
            var matched = new List<int>(3);
            foreach (int trayId in tray) if (next.Kinds[trayId] == next.Kinds[id]) matched.Add(trayId);
            if (matched.Count == 3)
            {
                foreach (int matchedId in matched) { next.Locations[matchedId] = SheepLocation.Removed; tray.Remove(matchedId); }
                ClearLastPick(next);
            }
            else matched.Clear();
            next.Tray = tray.ToArray();
            state = next;
            return new SheepMove(true, id, matched.ToArray(), Phase);
        }

        public bool CanUseTool(SheepTool tool)
        {
            SheepPhase phase = Phase;
            if (phase == SheepPhase.Won) return false;
            switch (tool)
            {
                case SheepTool.RemoveThree:
                    return !state.RemoveUsed && state.Tray.Length > 0 && state.Reserve.Length + Math.Min(3, state.Tray.Length) <= 6;
                case SheepTool.Undo:
                    return !state.UndoUsed && state.LastPickedId >= 0;
                case SheepTool.Shuffle:
                    if (phase != SheepPhase.Playing || state.ShuffleUsed) return false;
                    int boardCount = 0;
                    foreach (SheepLocation location in state.Locations) if (location == SheepLocation.Board) boardCount++;
                    return boardCount >= 2;
                case SheepTool.Revive:
                    return phase == SheepPhase.Lost && !state.ReviveUsed && state.Reserve.Length + 3 <= 6;
                default: return false;
            }
        }

        public bool UseTool(SheepTool tool)
        {
            if (!CanUseTool(tool)) return false;
            var next = state.Copy();
            switch (tool)
            {
                case SheepTool.RemoveThree:
                    MoveToReserve(next); next.RemoveUsed = true; break;
                case SheepTool.Revive:
                    MoveToReserve(next); next.ReviveUsed = true; break;
                case SheepTool.Undo:
                    var tray = new List<int>(next.Tray);
                    tray.Remove(next.LastPickedId);
                    next.Tray = tray.ToArray();
                    next.Locations[next.LastPickedId] = (SheepLocation)next.LastSource;
                    if (next.LastSource == (int)SheepLocation.Reserve)
                    {
                        var reserve = new List<int>(next.Reserve);
                        reserve.Insert(next.LastReserveIndex, next.LastPickedId);
                        next.Reserve = reserve.ToArray();
                    }
                    next.UndoUsed = true; break;
                case SheepTool.Shuffle:
                    var board = new List<int>();
                    for (int id = 0; id < next.Kinds.Length; id++) if (next.Locations[id] == SheepLocation.Board) board.Add(id);
                    for (int i = board.Count - 1; i > 0; i--)
                    {
                        int other = SheepRandom.Index(ref next.RandomState, i + 1);
                        int swap = next.Kinds[board[i]];
                        next.Kinds[board[i]] = next.Kinds[board[other]];
                        next.Kinds[board[other]] = swap;
                    }
                    next.ShuffleUsed = true; break;
            }
            ClearLastPick(next);
            state = next;
            return true;
        }

        private static void MoveToReserve(SheepSnapshot next)
        {
            var tray = new List<int>(next.Tray);
            var reserve = new List<int>(next.Reserve);
            int count = Math.Min(3, tray.Count);
            for (int i = 0; i < count; i++) { next.Locations[tray[i]] = SheepLocation.Reserve; reserve.Add(tray[i]); }
            tray.RemoveRange(0, count);
            next.Tray = tray.ToArray(); next.Reserve = reserve.ToArray();
        }

        private static void ClearLastPick(SheepSnapshot next)
        {
            next.LastPickedId = -1; next.LastSource = -1; next.LastReserveIndex = -1;
        }
    }
}
