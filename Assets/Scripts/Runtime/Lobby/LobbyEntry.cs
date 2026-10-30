using System;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    internal enum LobbyDisplayMode
    {
        Double = 0,
        Directory = 1,
        Single = 2
    }

    // The host supplies the existing game actions and live records. The view owns presentation only.
    internal sealed class LobbyEntry
    {
        public string Id;
        public string ObjectName;
        public string Name;
        public string Number;
        public string Category;
        public string Tagline;
        public string Description;
        public string Record;
        public string CompactRecord;
        public Action Enter;
        public Action<RectTransform> BuildLogo;
    }
}
