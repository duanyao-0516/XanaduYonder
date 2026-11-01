using UnityEngine;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private CanvasGroup externalGameLobbyInput;
        private bool IsExternalGameRunning => IsKingdomRushRunning || IsLevelDevilRunning || IsCutTheRopeRunning || IsZhaoyunAdouRunning || IsClassicGoldMinerRunning;

        private void SetExternalGameLobbyInput(bool enabled)
        {
            if (lobbyPage == null) return;
            if (externalGameLobbyInput == null)
            {
                externalGameLobbyInput = lobbyPage.GetComponent<CanvasGroup>();
                if (externalGameLobbyInput == null)
                    externalGameLobbyInput = lobbyPage.AddComponent<CanvasGroup>();
            }
            // The lobby root survives orientation and display-mode changes underneath
            // an external Activity. A late return must not unlock another host's session.
            bool available = enabled && !IsExternalGameRunning;
            externalGameLobbyInput.interactable = available;
            externalGameLobbyInput.blocksRaycasts = available;
        }
    }
}
