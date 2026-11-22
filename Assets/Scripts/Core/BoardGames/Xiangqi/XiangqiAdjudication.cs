using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class XiangqiGame
    {
        private int RuleResult(out string message)
        {
            if (pieceCount == 2)
            {
                message = "无胜势和棋";
                return BoardResult.Draw;
            }
            int current = history.Count - 1;
            int first = current;
            int previousOccurrences = 0;
            // WXF 3.2.B uses four occurrences. Three completed cycles likewise give the
            // prohibited side time to vary before this offline game assigns responsibility.
            for (int i = current - 4; i >= lastIrreversible; i -= 2)
            {
                if (history[i].Key != positionKey || ++previousOccurrences != 3) continue;
                first = i;
                break;
            }
            if (previousOccurrences == 3)
            {
                int red = RepetitionResponsibility(first, current, 0);
                int black = RepetitionResponsibility(first, current, 1);
                message = red == black ? "循环和棋" : Math.Max(red, black) == 2 ? "长将判负" : "长捉判负";
                return red == black ? BoardResult.Draw : red > black ? 1 : 0;
            }

            // Under WXF Article 8 the claiming side can count at most ten of its checks.
            int excessChecks = Math.Max(0, Math.Min(redChecks, blackChecks) - 10);
            if (naturalMoves - excessChecks >= NaturalLimit)
            {
                message = "自然限着和棋";
                return BoardResult.Draw;
            }
            message = null;
            return BoardResult.Ongoing;
        }

        // Checks take precedence over chases (WXF 3.1.H and 20.1). A mix of checks
        // and non-checks on its own assigns no responsibility under WXF 20.2.
        private int RepetitionResponsibility(int first, int last, int side)
        {
            bool checkedOnAllTurns = true;
            bool checkedAtLeastOnce = false;
            for (int i = first + (side == CurrentSide ? 1 : 2); i <= last; i += 2)
            {
                bool check = history[i].Check;
                checkedOnAllTurns &= check;
                checkedAtLeastOnce |= check;
            }
            if (checkedOnAllTurns) return 2;
            if (checkedAtLeastOnce) return 0;
            return HasPerpetualChase(first, last, side) ? 1 : 0;
        }

        private bool HasPerpetualChase(int first, int last, int side)
        {
            var replay = new XiangqiGame(this);
            while (replay.history.Count - 1 > first) replay.PopMove();
            // A physical ID survives shuffles of identical pieces in a repeated diagram.
            var ids = new int[Squares];
            for (int i = 0; i < Squares; i++) ids[i] = i;
            HashSet<int> repeatedTargets = null;
            var existingThreats = new HashSet<int>();
            var currentThreats = new HashSet<int>();
            var resolvedTargets = new HashSet<int>();
            for (int i = first + 1; i <= last; i++)
            {
                BoardMove move = history[i].Move;
                bool hunting = replay.CurrentSide == side;
                if (hunting) replay.CollectChases(side, ids, existingThreats);
                replay.PushMove(move);
                MoveId(ids, move);
                if (!hunting) continue;
                replay.CollectChases(side, ids, currentThreats);
                currentThreats.ExceptWith(existingThreats);
                if (currentThreats.Count == 0) return false;
                // At the loop boundary the first move is the actual repeated reply.
                BoardMove reply = history[i == last ? first + 1 : i + 1].Move;
                sbyte piece = replay.board[reply.From];
                sbyte captured = replay.board[reply.To];
                int savedId = ids[reply.To];
                replay.MovePiece(reply.From, reply.To, piece);
                MoveId(ids, reply);
                replay.CollectChases(side, ids, existingThreats);
                replay.RestorePiece(reply.From, reply.To, piece, captured);
                ids[reply.From] = ids[reply.To];
                ids[reply.To] = savedId;
                currentThreats.ExceptWith(existingThreats);
                resolvedTargets.Clear();
                foreach (int chase in currentThreats) resolvedTargets.Add(chase % Squares);
                if (repeatedTargets == null) repeatedTargets = new HashSet<int>(resolvedTargets);
                else repeatedTargets.IntersectWith(resolvedTargets);
                if (repeatedTargets.Count == 0) return false;
            }
            return repeatedTargets != null && repeatedTargets.Count > 0;
        }

        private static void MoveId(int[] ids, BoardMove move)
        {
            ids[move.To] = ids[move.From];
            ids[move.From] = -1;
        }

        private void CollectChases(int side, int[] ids, HashSet<int> threats)
        {
            threats.Clear();
            for (int from = 0; from < Squares; from++)
            {
                sbyte attacker = board[from];
                int kind = Math.Abs(attacker);
                if (attacker == 0 || SideOf(attacker) != side || kind == King || kind == Pawn) continue;
                for (int to = 0; to < Squares; to++)
                    if (CountsAsChase(from, to, side, kind)) threats.Add(ids[from] * Squares + ids[to]);
            }
        }

        private bool CountsAsChase(int from, int to, int side, int attackerKind)
        {
            sbyte target = board[to];
            int targetKind = Math.Abs(target);
            int enemy = 1 - side;
            if (target == 0 || SideOf(target) != enemy || targetKind == King ||
                (targetKind == Pawn && !CrossedRiver(enemy, to / Columns)) || !IsLegalMove(from, to, side))
                return false;

            // A reciprocal legal capture is an exchange. A pin or blocked horse leg can
            // prevent the reciprocal capture, including between pieces of the same type.
            if (IsLegalMove(to, from, enemy)) return false;
            if (targetKind == Rook && (attackerKind == Horse || attackerKind == Cannon)) return true;
            sbyte attacker = board[from];
            MovePiece(from, to, attacker);
            bool rooted = false;
            for (int defender = 0; defender < Squares && !rooted; defender++)
                rooted = IsLegalMove(defender, to, enemy);
            RestorePiece(from, to, attacker, target);
            return !rooted;
        }
    }
}
