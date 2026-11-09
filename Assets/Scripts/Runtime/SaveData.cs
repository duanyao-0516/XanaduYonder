using System;
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
        // Persisted together with both settings. Never clear on native ACK: an
        // ACK failure must not let the same journal replay after a cold start.
        public string WebGameFeedbackRevision;

        internal bool ImportWebGameFeedback(string revision,bool soundEnabled,bool hapticsEnabled)
        {
            if(!Guid.TryParseExact(revision,"D",out _))throw new ArgumentException("Invalid web feedback revision.",nameof(revision));
            if(WebGameFeedbackRevision==revision)return false;
            SoundEnabled=soundEnabled;HapticsEnabled=hapticsEnabled;WebGameFeedbackRevision=revision;
            return true;
        }
    }
}
