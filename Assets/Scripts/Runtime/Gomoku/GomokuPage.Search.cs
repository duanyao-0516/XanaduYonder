using System;
using System.Threading;
using System.Threading.Tasks;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    internal sealed partial class GomokuPage
    {
        private Task<GomokuAiResult> search;
        private CancellationTokenSource searchCancellation;
        private GomokuAiRequest request;

        private void EnsureSearch()
        {
            if (!Allowed || session == null || !session.NeedsAi || search != null || aiError != null) return;
            request = session.BeginAi();
            if (request == null) return;
            var snapshot = request.Game;
            var difficulty = session.Difficulty;
            searchCancellation = new CancellationTokenSource();
            var token = searchCancellation.Token;
            // The worker owns only a copied rules board. Unity and the session remain on the main thread.
            search = Task.Run(() => GomokuAi.FindMove(snapshot, difficulty, token), token);
            Refresh();
        }
        private void Update()
        {
            if (search == null || !search.IsCompleted) return;
            var completed = search; var identity = request;
            search = null; request = null;
            searchCancellation.Dispose(); searchCancellation = null;
            if (completed.IsCanceled) { session?.CancelAi(); Refresh(); return; }
            if (completed.IsFaulted)
            {
                var error = completed.Exception; // Observe even a stale worker failure.
                if (Allowed) SearchFailed(error == null ? "Unknown search failure" : error.GetBaseException().Message);
                return;
            }
            if (!Allowed) { session?.CancelAi(); return; }
            if (session != null && session.AcceptAi(identity, completed.Result.Index)) Committed();
            else SearchFailed("AI returned an obsolete or illegal move.");
        }
        private void SearchFailed(string detail)
        {
            UnityEngine.Debug.LogWarning("Gomoku search failed: " + detail);
            session?.Pause(); aiError = "电脑暂时无法落子，点击下方重试。"; Refresh();
        }
        private void CancelSearch()
        {
            session?.CancelAi();
            if (search == null) return;
            var retired = search; var cancellation = searchCancellation;
            search = null; request = null; searchCancellation = null;
            cancellation.Cancel();
            // Never block Unity waiting for a worker; dispose its token only after it has stopped.
            retired.ContinueWith(task => { if (task.IsFaulted) { var observed = task.Exception; } cancellation.Dispose(); }, TaskScheduler.Default);
        }
    }
}
