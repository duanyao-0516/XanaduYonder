using XanaduYonder.Core;

namespace XanaduYonder.Runtime
{
    public sealed class SaveData
    {
        public GameSnapshot Game;
        public long BestScore;
        // Zero is allowed only for newly constructed in-memory data; disk saves carry an initialized record.
        public int BestTile;
        public bool SoundEnabled = true;
        public bool HapticsEnabled = true;
    }
}
