using UnityEngine;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private CanvasGroup externalGameLobbyInput;
        private bool IsExternalGameRunning => IsKingdomRushRunning || IsLevelDevilRunning || IsCutTheRopeRunning;

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
            // an external Activity. Never retain an individual card's input component.
            externalGameLobbyInput.interactable = enabled;
            externalGameLobbyInput.blocksRaycasts = enabled;
        }
    }
}
