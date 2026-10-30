using UnityEngine;

namespace XanaduYonder.Runtime
{
    // A presentation preference, deliberately independent of every game's save store.
    internal static class LobbyDisplayPreferences
    {
        public const string Key = "xanadu.lobby.display.v1";

        public static LobbyDisplayMode Load()
        {
            return Normalize((LobbyDisplayMode)PlayerPrefs.GetInt(Key, (int)LobbyDisplayMode.Double));
        }

        public static void Save(LobbyDisplayMode mode)
        {
            PlayerPrefs.SetInt(Key, (int)Normalize(mode));
            PlayerPrefs.Save();
        }

        internal static LobbyDisplayMode Normalize(LobbyDisplayMode mode)
        {
            return mode == LobbyDisplayMode.Directory || mode == LobbyDisplayMode.Single
                ? mode : LobbyDisplayMode.Double;
        }
    }
}
