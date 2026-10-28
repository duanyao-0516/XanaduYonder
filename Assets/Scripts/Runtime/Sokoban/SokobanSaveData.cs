using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    public sealed class SokobanScore
    {
        public int Moves, Pushes;
        public SokobanScore Copy() { return new SokobanScore { Moves = Moves, Pushes = Pushes }; }
        internal bool Valid { get { return Pushes > 0 && Moves >= Pushes && Moves <= SokobanGame.MaximumMoves; } }
    }
    public sealed class SokobanRecord
    {
        public string LevelId;
        public SokobanScore BestMoves, BestPushes;
        public SokobanRecord Copy() { return new SokobanRecord { LevelId = LevelId, BestMoves = BestMoves == null ? null : BestMoves.Copy(), BestPushes = BestPushes == null ? null : BestPushes.Copy() }; }
        public bool Consider(int moves, int pushes)
        {
            var score = new SokobanScore { Moves = moves, Pushes = pushes }; if (!score.Valid) throw new ArgumentException("Invalid result tuple.");
            bool bestMoves = BestMoves == null || CompareMoves(score, BestMoves) < 0;
            bool bestPushes = BestPushes == null || ComparePushes(score, BestPushes) < 0;
            if (bestMoves) BestMoves = score.Copy(); if (bestPushes) BestPushes = score.Copy(); return bestMoves || bestPushes;
        }
        internal bool Valid { get { return !string.IsNullOrWhiteSpace(LevelId) && BestMoves != null && BestPushes != null && BestMoves.Valid && BestPushes.Valid && CompareMoves(BestMoves, BestPushes) <= 0 && ComparePushes(BestPushes, BestMoves) <= 0; } }
        internal static int CompareMoves(SokobanScore a, SokobanScore b) { int first = a.Moves.CompareTo(b.Moves); return first != 0 ? first : a.Pushes.CompareTo(b.Pushes); }
        internal static int ComparePushes(SokobanScore a, SokobanScore b) { int first = a.Pushes.CompareTo(b.Pushes); return first != 0 ? first : a.Moves.CompareTo(b.Moves); }
    }
    /// <summary>Boundary DTOs are copied when crossing into or out of the owned live state.</summary>
    public sealed class SokobanSessionData
    {
        public string AttemptId, LevelId, Actions;
        public int Player, Moves, Pushes;
        public int[] Boxes;
        public bool Won, ResultRecorded;
        public double Zoom = 1, CenterX = .5, CenterY = .5;
        public SokobanSessionData Copy() { return new SokobanSessionData { AttemptId = AttemptId, LevelId = LevelId, Actions = Actions, Player = Player, Moves = Moves, Pushes = Pushes, Boxes = Boxes == null ? null : (int[])Boxes.Clone(), Won = Won, ResultRecorded = ResultRecorded, Zoom = Zoom, CenterX = CenterX, CenterY = CenterY }; }
        internal bool ValidateShape(out string reason, bool allowUnrecordedTerminal = false)
        {
            reason = null;
            if (!Guid.TryParseExact(AttemptId, "D", out var id) || id == Guid.Empty || id.ToString("D") != AttemptId || string.IsNullOrWhiteSpace(LevelId)) return Fail("Invalid attempt or level identity.", out reason);
            if (Actions == null || Actions.Length > SokobanGame.MaximumMoves || Moves != Actions.Length || Pushes < 0 || Pushes > Moves) return Fail("Invalid action history or counts.", out reason);
            foreach (char c in Actions) if ("LURD".IndexOf(c) < 0) return Fail("Invalid direction in history.", out reason);
            if (Player < 0 || Player >= 140 || Boxes == null || Boxes.Length == 0 || Boxes.Length > 140) return Fail("Invalid player or boxes.", out reason);
            var seen = new HashSet<int>(); foreach (int box in Boxes) if (box < 0 || box >= 140 || box == Player || !seen.Add(box)) return Fail("Invalid or duplicate box.", out reason);
            if ((!Won && ResultRecorded) || (Won && !ResultRecorded && !allowUnrecordedTerminal) || (Won && Pushes == 0)) return Fail("Result accounting disagrees with terminal state.", out reason);
            if (!Finite(Zoom) || !Finite(CenterX) || !Finite(CenterY) || Zoom < 1 || Zoom > 2 || CenterX < 0 || CenterX > 1 || CenterY < 0 || CenterY > 1 || (Zoom == 1 && (CenterX != .5 || CenterY != .5))) return Fail("Invalid viewport state.", out reason);
            return true;
        }
        internal bool TryReplay(SokobanCatalog catalog, out SokobanGame game, out string reason, bool allowUnrecordedTerminal = false)
        {
            game = null; if (!ValidateShape(out reason, allowUnrecordedTerminal)) return false;
            if (!catalog.TryGet(LevelId, out var level)) return Fail("Unknown active level.", out reason);
            var replay = new SokobanGame(level);
            foreach (char action in Actions) if (!replay.Move(SokobanGame.FromChar(action)).Changed) return Fail("Saved history includes an impossible or post-victory action.", out reason);
            var snapshot = replay.GetSnapshot(); var sorted = (int[])Boxes.Clone(); Array.Sort(sorted);
            if (snapshot.Player != Player || snapshot.Moves != Moves || snapshot.Pushes != Pushes || snapshot.Won != Won || snapshot.Boxes.Length != sorted.Length) return Fail("Replayed state disagrees with saved state.", out reason);
            var replayBoxes = snapshot.Boxes; for (int i = 0; i < sorted.Length; i++) if (sorted[i] != replayBoxes[i]) return Fail("Replayed boxes disagree with saved boxes.", out reason);
            game = replay; return true;
        }
        internal void NormalizeViewport()
        { Zoom = Math.Max(1, Math.Min(2, Zoom)); CenterX = Math.Max(0, Math.Min(1, CenterX)); CenterY = Math.Max(0, Math.Min(1, CenterY)); if (Zoom == 1) CenterX = CenterY = .5; }
        internal static bool Finite(double number) { return !double.IsNaN(number) && !double.IsInfinity(number); }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
    public sealed class SokobanSaveData
    {
        public string ContentVersion, SelectedLevelId;
        public bool TutorialSeen;
        public SokobanSessionData ActiveAttempt;
        public string[] CompletedLevelIds = new string[0];
        public SokobanRecord[] Records = new SokobanRecord[0];
        public string RestoreWarning;
        public SokobanSaveData Copy()
        {
            var copy = new SokobanSaveData { ContentVersion = ContentVersion, SelectedLevelId = SelectedLevelId, TutorialSeen = TutorialSeen, ActiveAttempt = ActiveAttempt == null ? null : ActiveAttempt.Copy(), CompletedLevelIds = CompletedLevelIds == null ? null : (string[])CompletedLevelIds.Clone(), Records = Records == null ? null : new SokobanRecord[Records.Length], RestoreWarning = RestoreWarning };
            if (Records != null) for (int i = 0; i < Records.Length; i++) copy.Records[i] = Records[i] == null ? null : Records[i].Copy(); return copy;
        }
        public bool TryValidate(SokobanCatalog catalog, out string reason, bool replay = true)
        {
            reason = null;
            if (catalog == null || ContentVersion != catalog.ContentVersion || !catalog.TryGet(SelectedLevelId, out var selected)) return Fail("Invalid catalog version or selection.", out reason);
            if (CompletedLevelIds == null || Records == null || CompletedLevelIds.Length != Records.Length || CompletedLevelIds.Length > catalog.Count) return Fail("Invalid completion records.", out reason);
            var completed = new HashSet<string>(StringComparer.Ordinal); foreach (string id in CompletedLevelIds) if (!catalog.TryGet(id, out var level) || !completed.Add(id)) return Fail("Unknown or duplicate completion.", out reason);
            var records = new Dictionary<string, SokobanRecord>(StringComparer.Ordinal);
            foreach (var record in Records) { if (record == null || !record.Valid || !completed.Contains(record.LevelId) || records.ContainsKey(record.LevelId)) return Fail("Missing, invalid or duplicate best record.", out reason); records.Add(record.LevelId, record); }
            if (ActiveAttempt == null) return true;
            if (ActiveAttempt.LevelId != SelectedLevelId || !catalog.TryGet(ActiveAttempt.LevelId, out var activeLevel)) return Fail("Selected level and active attempt disagree.", out reason);
            if (!ActiveAttempt.ValidateShape(out reason)) return false;
            if (activeLevel.IsWall(ActiveAttempt.Player) || ActiveAttempt.Boxes.Length != activeLevel.BoxCount) return Fail("Current occupancy disagrees with the level.", out reason);
            int covered = 0;
            foreach (int box in ActiveAttempt.Boxes)
            {
                if (activeLevel.IsWall(box)) return Fail("A box occupies a wall or lies outside the map.", out reason);
                if (activeLevel.IsGoal(box)) covered++;
            }
            if (ActiveAttempt.Won != (covered == activeLevel.BoxCount)) return Fail("Terminal state disagrees with occupied goals.", out reason);
            if (replay && !ActiveAttempt.TryReplay(catalog, out var game, out reason)) return false;
            if (ActiveAttempt.Won)
            {
                if (!records.TryGetValue(ActiveAttempt.LevelId, out var record)) return Fail("Completed attempt has no record.", out reason);
                var score = new SokobanScore { Moves = ActiveAttempt.Moves, Pushes = ActiveAttempt.Pushes };
                if (SokobanRecord.CompareMoves(record.BestMoves, score) > 0 || SokobanRecord.ComparePushes(record.BestPushes, score) > 0) return Fail("Recorded best is worse than the completed attempt.", out reason);
            }
            return true;
        }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
    }
}
