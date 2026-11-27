namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void SetActivePage(Page page)
        {
            activePage = page;
            lobbyPage.SetActive(page == Page.Lobby);
            gamePage.SetActive(page == Page.Game2048);
        }
    }
}
