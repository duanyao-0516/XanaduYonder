using XanaduYonder.Core.AgeOfWar;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarSaveData
    {
        public int SchemaVersion = 1;
        public int Edition;
        // Null is an intentional tombstone: no battle is available to resume.
        public byte[] BattleState;
        public double CameraX;
        public double CameraY;
        public string Menu = "main";
        public string Quality = "HIGH";
        public float MusicVolume = 1;
        public float SoundVolume = 1;
        public bool ShowDamage;
        public int FrameRate = 150;
    }

    internal enum AgeOfWarLoadStatus { Missing, Loaded, Recovered, Corrupt, UnsupportedVersion, IoError }

    internal sealed class AgeOfWarLoadResult
    {
        public AgeOfWarLoadStatus Status;
        public AgeOfWarSaveData Data;
        public IAgeOfWarBattle Battle;
        public string Warning;
        public bool CanWrite;
        public bool HasBattle { get { return Battle != null; } }
    }
}
