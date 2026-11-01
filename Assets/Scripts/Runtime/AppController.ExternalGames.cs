using UnityEngine;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private CanvasGroup externalGameLobbyInput;
        private bool IsExternalGameRunning => IsKingdomRushRunning || IsZhaoyunAdouRunning;

        private void SetExternalGameLobbyInput(bool enabled)
        {
            if (lobbyPage == null) return;
            if (externalGameLobbyInput == null)
            {
                externalGameLobbyInput = lobbyPage.GetComponent<CanvasGroup>();
                if (externalGameLobbyInput == null) externalGameLobbyInput = lobbyPage.AddComponent<CanvasGroup>();
            }
            // The stable lobby root survives display-mode rebuilds. A late return from
            // one host must not unlock the lobby while another native session owns it.
            bool available = enabled && !IsExternalGameRunning;
            externalGameLobbyInput.interactable = available;
            externalGameLobbyInput.blocksRaycasts = available;
        }
    }
}
