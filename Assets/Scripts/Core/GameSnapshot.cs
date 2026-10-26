using System;

namespace XanaduYonder.Core
{
    [Serializable]
    public sealed class GameSnapshot
    {
        public int schemaVersion = 1;
        public string gameId = "2048";
        public int[] cells;
        public long score;
        public bool winAcknowledged;

        public static bool TryValidate(GameSnapshot snapshot, out string reason)
        {
            if (snapshot == null)
            {
                reason = "Snapshot is missing.";
                return false;
            }
            if (snapshot.schemaVersion != 1)
            {
                reason = "Unsupported snapshot schema version.";
                return false;
            }
            if (snapshot.gameId != "2048")
            {
                reason = "Snapshot belongs to a different game.";
                return false;
            }
            if (snapshot.cells == null || snapshot.cells.Length != 16)
            {
                reason = "Snapshot must contain exactly 16 cells.";
                return false;
            }
            if (snapshot.score < 0)
            {
                reason = "Snapshot score cannot be negative.";
                return false;
            }

            var hasWon = false;
            foreach (var value in snapshot.cells)
            {
                if (value != 0 && (value < 2 || value > Game2048.MaximumTile || (value & (value - 1)) != 0))
                {
                    reason = "Snapshot contains an unsupported tile value.";
                    return false;
                }
                if (value >= 2048) hasWon = true;
            }
            if (snapshot.winAcknowledged && !hasWon)
            {
                reason = "A win cannot be acknowledged without a winning tile.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
