using XanaduYonder.Core;

namespace XanaduYonder.Runtime
{
    public sealed class SaveData
    {
        public GameSnapshot Game;
        public long BestScore;
        public bool SoundEnabled = true;
        public bool HapticsEnabled = true;
    }
}
