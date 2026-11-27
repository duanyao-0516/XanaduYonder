namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void OnApplicationPause(bool value)
        {
            if (value) ResetBackExitConfirmation();
            paused = value;
            UpdatePageForeground(losingFocusOrPausing: value);
            if (value)
            {
                if (activePage == Page.Feihualing) feihualingPage?.PauseForBackground();
                idiomPage?.PauseForBackground();
                StopAnimation();
                feedback?.Stop();
                Save();
            }
            else CheckEndState();
        }

        private void OnApplicationFocus(bool value)
        {
            if (!value) ResetBackExitConfirmation();
            focused = value;
            UpdatePageForeground(losingFocusOrPausing: !value);
            if (!value)
            {
                // Android's soft input dialog sends focus(false) while the Activity
                // remains resumed. Real backgrounding is handled by OnApplicationPause.
                if (activePage != Page.Idiom && activePage != Page.Feihualing) StopAnimation();
                feedback?.Stop();
                Save();
            }
            else CheckEndState();
        }

        private void UpdatePageForeground(bool losingFocusOrPausing)
        {
            bool foreground = !paused && focused;
            goldMinerPage?.SetForeground(focused, paused);
            zumaPage?.HostActivity(foreground);
            if (losingFocusOrPausing && (activePage == Page.Minesweeper || activePage == Page.Gomoku ||
                activePage == Page.HappyBall || activePage == Page.Sokoban || activePage == Page.SlidingPuzzle ||
                activePage == Page.Tetris || activePage == Page.FlappyBird || activePage == Page.LinkMatch ||
                activePage == Page.AircraftFire || activePage == Page.Snake)) CloseModal();
            aircraftFirePage?.SetForeground(foreground);
            tetrisPage?.SetForeground(foreground);
            flappyBirdPage?.SetForeground(foreground);
            happyBallPage?.SetForeground(foreground);
            minesweeperPage?.SetForeground(foreground);
            snakePage?.SetForeground(foreground);
            sokobanPage?.SetForeground(foreground);
            slidingPuzzlePage?.SetForeground(foreground);
            linkMatchPage?.SetForeground(foreground);
            gomokuPage?.SetForeground(foreground);
            if (activePage == Page.ClassicBoards && losingFocusOrPausing) CloseModal();
            boardPage?.SetForeground(foreground);
            sudokuPage?.SetForeground(foreground);
        }

        private void OnDestroy()
        {
            ResetBackExitConfirmation();
            ReleaseAircraftFire();
            ReleaseLevelDevilLauncher();
            ReleaseBombItLauncher();
            ReleaseCutTheRopeLauncher();
            ReleaseZhaoyunAdouLauncher();
            if (flappyLobbySprite != null) Destroy(flappyLobbySprite);
        }

        private void OnApplicationQuit()
        {
            if (activePage == Page.Feihualing) feihualingPage?.PauseForBackground();
            snakePage?.Suspend();
            aircraftFirePage?.Suspend();
            zumaPage?.Suspend();
            linkMatchPage?.Suspend();
            Save();
        }
    }
}
