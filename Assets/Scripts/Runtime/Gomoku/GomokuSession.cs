using System;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    /// <summary>Search input and identities captured on the main thread. Never shares a mutable board.</summary>
    public sealed class GomokuAiRequest
    {
        private readonly GomokuGame snapshot;
        internal readonly GomokuSession Owner;
        public string SessionId { get; private set; }
        public long Revision { get; private set; }
        public long Generation { get; private set; }
        public GomokuGame Game { get { return snapshot.Copy(); } }

        internal GomokuAiRequest(GomokuSession owner, string sessionId, long revision, long generation, GomokuGame game)
        {
            Owner = owner; SessionId = sessionId; Revision = revision; Generation = generation; snapshot = game.Copy();
        }
    }

    /// <summary>Main-thread owner of preview, board mutation and cancellable AI request identities.</summary>
    public sealed class GomokuSession
    {
        private readonly GomokuSessionData data;
        private readonly GomokuGame game;
        private long revision, generation;
        private bool foreground = true, blocked;
        private GomokuAiRequest pending;

        public string SessionId { get { return data.SessionId; } }
        public GomokuGame Game { get { return game.Copy(); } }
        public GomokuMode Mode { get { return data.Mode; } }
        public GomokuDifficulty Difficulty { get { return data.Difficulty; } }
        public GomokuStone HumanStone { get { return data.HumanStone; } }
        public int BlackPlayer { get { return data.BlackPlayer; } }
        public int? SelectedIndex { get; private set; }
        public bool Paused { get; private set; }
        private bool Active { get { return foreground && !blocked && !Paused && game.State == GomokuState.Playing; } }
        public bool CanSelect { get { return Active && (Mode == GomokuMode.Local || game.ToPlay == HumanStone); } }
        public bool NeedsAi { get { return Active && Mode == GomokuMode.HumanVsAi && game.ToPlay != HumanStone; } }
        public bool AiPending { get { return pending != null; } }
        public bool CanUndo { get { return Active && UndoCount() > 0; } }

        public GomokuSession(GomokuSessionData saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            data = saved.Copy();
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(saved));
            game = GomokuGame.Restore(data.Moves);
            Paused = true;
        }

        public static GomokuSession Create(GomokuMode mode, GomokuDifficulty difficulty, GomokuStone humanStone, int blackPlayer)
        {
            var session = new GomokuSession(new GomokuSessionData { SessionId = Guid.NewGuid().ToString("D"), Mode = mode,
                Difficulty = difficulty, HumanStone = humanStone, BlackPlayer = blackPlayer, Moves = new int[0] });
            session.Resume();
            return session;
        }

        public bool TrySelect(int index)
        {
            if (!CanSelect || index < 0 || index >= 225 || game.GetCell(index) != GomokuStone.Empty) return false;
            SelectedIndex = index;
            return true;
        }

        public void ClearSelection() { SelectedIndex = null; }

        public bool ConfirmSelection()
        {
            if (!CanSelect || !SelectedIndex.HasValue || !game.TryPlay(SelectedIndex.Value)) return false;
            BoardChanged();
            return true;
        }

        public bool Undo()
        {
            if (!CanUndo || !game.Undo(UndoCount())) return false;
            BoardChanged();
            return true;
        }

        private int UndoCount()
        {
            if (Mode == GomokuMode.Local) return game.MoveCount > 0 ? 1 : 0;
            // Move parity derives the last human ply. White's initial AI move remains intact.
            for (int ply = game.MoveCount - 1; ply >= 0; ply--)
                if ((ply % 2 == 0 ? GomokuStone.Black : GomokuStone.White) == HumanStone) return game.MoveCount - ply;
            return 0;
        }

        public void Pause() { Paused = true; ClearSelection(); CancelAi(); }
        public void Resume() { if (foreground && !blocked) Paused = false; }
        public void SetForeground(bool value) { foreground = value; if (!value) Pause(); }
        public void SetBlocked(bool value) { blocked = value; if (value) Pause(); }

        public GomokuAiRequest BeginAi()
        {
            if (!NeedsAi || pending != null) return null;
            pending = new GomokuAiRequest(this, SessionId, revision, ++generation, game);
            return pending;
        }

        public bool AcceptAi(GomokuAiRequest request, int index)
        {
            if (request == null || request.Owner != this || pending != request || request.SessionId != SessionId ||
                request.Revision != revision || request.Generation != generation || !NeedsAi || index < 0 || index >= 225 ||
                game.GetCell(index) != GomokuStone.Empty || !game.TryPlay(index)) return false;
            BoardChanged();
            return true;
        }

        public void CancelAi() { pending = null; generation++; }
        private void BoardChanged() { revision++; ClearSelection(); CancelAi(); }

        public GomokuSessionData Export()
        {
            var copy = data.Copy();
            copy.Moves = game.ExportMoves();
            return copy;
        }
    }
}
