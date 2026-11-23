using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.BoardGames;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private BoardGamePage boardPage;

        private void EnterArmyChess() => EnterClassicBoard(BoardGameId.Army);
        private void EnterXiangqi() => EnterClassicBoard(BoardGameId.Xiangqi);
        private void EnterChess() => EnterClassicBoard(BoardGameId.Chess);
        private void EnterChineseCheckers() => EnterClassicBoard(BoardGameId.Checkers);

        private void EnterClassicBoard(BoardGameId game)
        {
            StopAnimation();
            CloseModal();
            ResetBackExitConfirmation();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            if (boardPage == null)
            {
                var root = UiFactory.Rect(frame, "Classic board game page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                boardPage = root.gameObject.AddComponent<BoardGamePage>();
                boardPage.Initialize(root, Application.persistentDataPath, ShowLobby,
                    () => activePage == Page.ClassicBoards && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, SetFeedbackSetting, feedback);
            }
            else boardPage.Leave();
            activePage = Page.ClassicBoards;
            lobbyPage.SetActive(false);
            gamePage.SetActive(false);
            if (status != null) status.gameObject.SetActive(false);
            boardPage.Enter(game, !paused && focused);
        }

        private static LobbyEntry ClassicBoardEntry(BoardGameId game, string objectName, string number, string tagline,
            string description, System.Action enter)
        {
            string id = BoardCatalog.Id(game);
            return LobbyGame(id, objectName, BoardCatalog.Name(game), number, "逻辑策略", tagline, description,
                game == BoardGameId.Checkers ? "本地双人 · 最多 5 台电脑" : "人机 / 本地双人 · 三档难度",
                game == BoardGameId.Checkers ? "双人 / 多人机" : "人机 / 双人", enter, parent => AddClassicBoardPreview(parent, id));
        }

        private static void AddClassicBoardPreview(Transform parent, string id)
        {
            var mask = LobbyPreviews.RoundedLogoFrame(parent, id);
            var graphic = UiFactory.Rect(mask, "Board game artwork", 0, 0, 96, 96).gameObject.AddComponent<Image>();
            graphic.sprite = Resources.Load<Sprite>("Lobby/Icons/" + id + "-logo");
            graphic.preserveAspect = true;
            graphic.raycastTarget = false;
        }
    }
}
