using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.Snake;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private SnakePage snakePage;

        private static void AddSnakePreview(Transform parent)
        {
            var root=LobbyPreviews.RoundedLogoFrame(parent,"Snake");
            var image=UiFactory.Rect(root,"Snake logo",0,0,96,96).gameObject.AddComponent<Image>();
            image.sprite=Resources.Load<Sprite>("Snake/Logo");
            image.preserveAspect=true;image.raycastTarget=false;
        }

        private void BuildSnake()
        {
            var root=UiFactory.Rect(frame,"Snake page",0,0,720,1320);
            UiFactory.Stretch(root);
            snakePage=root.gameObject.AddComponent<SnakePage>();
            snakePage.Initialize(root,ShowLobby,after=>OpenSettings(after),
                ()=>activePage==Page.Snake&&modal==null&&!paused&&focused,
                ()=>data.SoundEnabled,()=>data.HapticsEnabled,feedback,SetStatus);
            snakePage.Changed+=RefreshSnakeRecord;
            snakePage.Leave();
        }

        private void RefreshSnakeRecord()
        {
            if(snakePage==null)return;
            lobbyView?.SetRecord("snake","最高分 "+snakePage.BestScore+" · 闯关 "+snakePage.CompletedCount+"/12",
                "最高 "+snakePage.BestScore+" 分 · "+snakePage.CompletedCount+"/12 关");
        }

        private void EnterSnake()
        {
            LeaveAircraftFire();linkMatchPage?.Leave();tetrisPage?.Leave();LeaveGoldMiner();
            slidingPuzzlePage?.Leave();flappyBirdPage?.Leave();sokobanPage?.Leave();
            happyBallPage?.Leave();gomokuPage?.Leave();dinoPage?.Leave();
            StopAnimation();CloseModal();sudokuPage.Leave();jumpPage.Leave();sheepPage.Leave();
            minesweeperPage?.Leave();idiomPage?.Leave();watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly=false;
            activePage=Page.Snake;lobbyPage.SetActive(false);gamePage.SetActive(false);
            if(status!=null)status.gameObject.SetActive(false);
            snakePage.SetHostSaveWarning(saveFailed?saveWarning:null);
            snakePage.Enter();
        }
    }
}
