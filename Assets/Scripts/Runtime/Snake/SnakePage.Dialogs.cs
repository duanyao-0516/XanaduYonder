using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    internal sealed partial class SnakePage
    {
        private RectTransform ShowDialog(string title,string message,string primary,Action primaryAction,
            string secondary=null,Action secondaryAction=null,string tertiary=null,Action tertiaryAction=null,float height=620,bool primaryEnabled=true)
        {
            input.Cancel();session.Pause();board.FinishAnimation();levelDropdown.Hide();countdown.SetActive(false);CloseDialog();Save();
            var overlay=UiFactory.Panel(root,"Snake dialog overlay",0,0,720,1320,new Color(.09f,.24f,.2f,.4f),false);
            UiFactory.Stretch(overlay.rectTransform);overlay.raycastTarget=true;dialog=overlay.gameObject;
            var panel=UiFactory.Panel(overlay.transform,"Snake dialog",0,0,596,height,UiFactory.Paper);
            UiFactory.Center(panel.rectTransform);panel.raycastTarget=true;
            UiFactory.Label(panel.transform,title,32,30,532,72,36,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            float actions=tertiary!=null?270:secondary!=null?182:94;
            var body=UiFactory.Label(panel.transform,message,32,116,532,height-actions-132,26,UiFactory.Muted,TextAnchor.MiddleCenter);body.name="Dialog message";body.supportRichText=false;
            float top=height-actions-8;
            var primaryButton=UiFactory.Button(panel.transform,primary,32,top,532,78,()=>{CloseDialog();primaryAction?.Invoke();},true);
            primaryButton.interactable=primaryEnabled;
            if(secondary!=null)UiFactory.Button(panel.transform,secondary,32,top+90,532,72,()=>{CloseDialog();secondaryAction?.Invoke();});
            if(tertiary!=null)UiFactory.Button(panel.transform,tertiary,32,top+174,532,72,()=>{CloseDialog();tertiaryAction?.Invoke();});
            dismissDialog=()=>{CloseDialog();if(secondaryAction!=null)secondaryAction();else ShowPause();};
            return panel.rectTransform;
        }
        private void CloseDialog()
        { if(dialog!=null){dialog.SetActive(false);Destroy(dialog);dialog=null;}dismissDialog=null; }
        private string RulesLabel()
        {
            var rules=session.Game.Rules;
            return (rules.Mode==SnakeMode.Classic?Difficulties[rules.Difficulty]:catalog.Name(profile.SelectedLevel))+" · "+(rules.Boundary==SnakeBoundary.Wrap?"四边相通":"围墙边界");
        }
        private static string FormatTime(double seconds)
        { long whole=(long)seconds;return (whole/60).ToString("00")+":"+(whole%60).ToString("00"); }
        private void ShowPause()
        {
            if(!Available)return;
            if(session.Game.State!=SnakeState.Playing){resultShown=null;CheckEndState();return;}
            string title=sessionStarted?"已暂停":"准备开始";
            ShowDialog(title,RulesLabel()+"\n\n本局得分 "+session.Game.Score+"\n用时 "+FormatTime(session.ElapsedSeconds),
                sessionStarted?"继续游戏":"开始游戏",ContinueGame,"重新开始",ConfirmRestart,"返回大厅",()=>returnLobby(),680);
            // Back on the pause screen returns to the lobby without restarting or resuming.
            dismissDialog=()=>{CloseDialog();returnLobby();};
        }
        private void ContinueGame()
        {
            if(!Available)return;
            input.Cancel();sessionStarted=true;session.Resume();lastCountdown=0;Save();
            countdownText.text="3";countdown.SetActive(session.CountdownSeconds>0);RefreshHud();
        }
        private void ConfirmRestart()
        {
            if(!Available)return;
            ShowDialog("重新开始？","当前对局将结束。\n纪录和其他难度的进度会保留。","重新开始",RestartCurrent,"取消",ShowCurrentState);
        }
        private void RestartCurrent()
        {
            var rules=session.Game.Rules;
            if(rules.Mode==SnakeMode.Classic)StartClassic(rules.Difficulty,rules.Boundary,true);
            else StartCampaign(profile.SelectedLevel,true);
        }
        private void ConfirmBoundary()
        {
            if(!Available||session.Game.Rules.Mode!=SnakeMode.Classic)return;
            var rules=session.Game.Rules;
            var next=rules.Boundary==SnakeBoundary.Wrap?SnakeBoundary.Solid:SnakeBoundary.Wrap;
            string warningText=profile.GetClassic(rules.Difficulty,next)==null?"切换边界后将重新开始。":"切换后会替换目标边界的已有对局。\n当前边界的进度和纪录会保留。";
            ShowDialog(next==SnakeBoundary.Wrap?"开启四边相通？":"关闭四边相通？",warningText,"切换并重开",()=>StartClassic(rules.Difficulty,next,true),"取消",ShowCurrentState);
        }
        private void ChooseMode()
        {
            if(Available)ShowModePicker(CaptureOverlayReturn());
        }
        private void ShowModePicker(Action after)
        {
            if(!Available)return;
            ShowDialog("选择玩法","经典无限挑战，或沿着障碍逐关前进。","经典无限",()=>StartClassic(profile.SelectedDifficulty,profile.SelectedBoundary,false),
                "障碍闯关",()=>EnterCampaignMode(()=>ShowModePicker(after)),"取消",after,640);
            dismissDialog=()=>{CloseDialog();after();};
        }
        private void EnterCampaignMode(Action back)
        {
            if(profile.CampaignSlot!=null&&profile.CampaignSlot.Board.State==SnakeState.Playing)
                StartCampaign(profile.CampaignLevel,false);
            else PreviewCampaign(Mathf.Clamp(profile.SelectedLevel,0,profile.UnlockedLevel),back);
        }
        private void ChooseLevel(int selected)
        {
            if(!Available)return;
            var rules=session.Game.Rules;
            int current=rules.Mode==SnakeMode.Classic?rules.Difficulty:profile.SelectedLevel;
            var after=returnFromDropdown;returnFromDropdown=null;
            levelDropdown.SetValueWithoutNotify(current);levelDropdown.RefreshShownValue();levelDropdown.Hide();
            if(selected==current){if(after!=null)after();else ShowCurrentState();return;}
            if(rules.Mode==SnakeMode.Campaign){PreviewCampaign(selected,()=>ReopenLevelDropdown(after));return;}
            if(selected<0||selected>=Difficulties.Length)return;
            // Each difficulty owns its own slot; ordinary selection never replaces another one.
            StartClassic(selected,rules.Boundary,false);
        }
        private void ReopenLevelDropdown(Action after)
        {
            if(!Available)return;
            returnFromDropdown=null;
            StartCoroutine(ReopenLevelDropdownAfterClose(after,interactionEpoch,session.SessionId));
        }
        private IEnumerator ReopenLevelDropdownAfterClose(Action after,int epoch,string sessionId)
        {
            // UGUI destroys its fading list asynchronously; Show() ignores calls until it is gone.
            while(levelDropdown.IsExpanded)yield return null;
            if(!Available||dialog!=null||epoch!=interactionEpoch||session.SessionId!=sessionId)yield break;
            returnFromDropdown=after??CaptureOverlayReturn();
            levelDropdown.Show();
        }
        private void PreviewCampaign(int level,Action cancel=null)
        {
            if(level<0||level>=catalog.Count)return;
            Action after=cancel??(Action)ShowCurrentState;
            var rules=catalog.Get(level);bool unlocked=level<=profile.UnlockedLevel;
            bool resumable=profile.CampaignSlot!=null&&profile.CampaignLevel==level&&profile.CampaignSlot.Board.State==SnakeState.Playing;
            string description="收集 "+rules.TargetFood+" 个果实 · "+rules.InitialTickMs+" ms / 格\n"+(rules.Boundary==SnakeBoundary.Wrap?"四边相通":"围墙边界")+(unlocked?"":" · 完成上一关后解锁");
            if(unlocked&&profile.CampaignSlot!=null&&profile.CampaignLevel!=level&&profile.CampaignSlot.Board.State==SnakeState.Playing)description+="\n开始将替换当前闯关对局，通关纪录保留。";
            var panel=ShowDialog((level+1).ToString("00")+" "+catalog.Name(level),description,
                unlocked?(resumable?"继续关卡":"开始关卡"):"尚未解锁",unlocked?(Action)(()=>StartCampaign(level,!resumable)):ShowCurrentState,
                "取消",after,null,null,1050,unlocked);
            var body=panel.Find("Dialog message").GetComponent<Text>();body.rectTransform.anchoredPosition=new Vector2(32,-660);body.rectTransform.sizeDelta=new Vector2(532,160);body.fontSize=24;
            var preview=CreateBoard(panel,"Campaign map preview",98,120,400,480);
            preview.raycastTarget=false;preview.Render(rules,new SnakeGame(rules,1u).GetSnapshot());
        }
        private void ShowSettings()
        {
            if(!Available)return;
            var after=CaptureOverlayReturn();
            Suspend();CloseDialog();openSettings(after);
        }
        private void ShowRules()
        {
            if(!Available)return;
            var after=CaptureOverlayReturn();
            ShowDialog("贪吃蛇玩法","用右侧方向键或在棋盘上滑动转向。\n收集果实，每个得 10 分，蛇身增长一格。\n\n四边相通时，会从对侧边缘回来。\n围墙模式越界结束；撞到蛇身或障碍也结束。\n\n经典挑战逐渐加速，填满可行走格即通关。\n障碍关卡达到果实目标即可进入下一关。\n\n暂停、返回和后台都会保留进度。\n继续前有 3 秒倒计时。","知道了",after,null,null,null,null,870);
            dismissDialog=()=>{CloseDialog();after();};
        }
        private void ShowResult()
        {
            var game=session.Game;var rules=game.Rules;
            bool full=game.State==SnakeState.WonFullBoard,won=full||game.State==SnakeState.WonTarget;
            string title=full?"满盘通关":won?"关卡通关":"本局结束";
            string message=(full?"100%\n"+game.Body.Length+" / "+rules.Capacity+" 可行走格\n\n":"")+
                "得分 "+game.Score+"    果实 "+game.FoodEaten+"\n用时 "+FormatTime(session.ElapsedSeconds)+"\n\n"+RulesLabel();
            if(rules.Mode==SnakeMode.Campaign&&won)
            {
                int next=profile.SelectedLevel+1;
                if(next>=catalog.Count)ShowDialog("本章已完成",message,"重玩关卡",RestartCurrent,"返回大厅",()=>returnLobby(),null,null,760);
                else ShowDialog(title,message,"下一关",()=>StartCampaign(next,true),"重玩关卡",RestartCurrent,"返回大厅",()=>returnLobby(),820);
            }
            else ShowDialog(title,message,"再来一局",RestartCurrent,"返回大厅",()=>returnLobby(),null,null,full?820:660);
            dismissDialog=()=>{CloseDialog();returnLobby();};
        }
    }
}
