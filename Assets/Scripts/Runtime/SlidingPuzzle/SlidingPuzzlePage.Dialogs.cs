using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    internal sealed partial class SlidingPuzzlePage
    {
        private GameObject dialog;
        private enum DialogKind { None, Rules, Restart, Result }
        private DialogKind dialogKind;
        private Action dismissDialog;
        private RectTransform OpenDialog(string title,float height)
        {
            CancelInput();CloseDialog();
            var overlay=UiFactory.Panel(root,"SlidingPuzzle dialog overlay",0,0,root.rect.width,root.rect.height,new Color(.09f,.24f,.21f,.46f),false);
            UiFactory.Stretch(overlay.rectTransform);overlay.raycastTarget=true;dialog=overlay.gameObject;
            var panel=Panel(overlay.transform,"SlidingPuzzle dialog",0,0,342,height,UiFactory.Paper,14);UiFactory.Center(panel.rectTransform);panel.raycastTarget=true;
            Label(panel.transform,"Dialog title",title,21,25,300,40,26,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            dismissDialog=CloseDialog;SynchronizeGate();Save();return panel.rectTransform;
        }
        private void CloseDialog()
        {
            if(dialog!=null){dialog.SetActive(false);Destroy(dialog);dialog=null;}
            dialogKind=DialogKind.None;dismissDialog=null;SynchronizeGate();
        }
        private void DismissDialog(){var dismiss=dismissDialog;CloseDialog();dismiss?.Invoke();}
        internal void ShowRules()
        {
            if(!CanNavigate)return;
            int n=choosing||Session==null?profile.SelectedChapter+3:Session.Game.Size;
            var panel=OpenDialog("让数字依次归位",515);
            dialogKind=DialogKind.Rules;
            Label(panel,"Rule movement","点击紧邻空格的数字，或将它滑向空格。\n每次只移动一块，记 1 步。",21,80,300,48,13,Secondary);
            var goal=Panel(panel,"Goal board",81,148,180,180,UiFactory.Hex("BBCBB6"),7);
            float gap=5,cell=(166-(n-1)*gap)/n;
            for(int i=0;i<n*n-1;i++)
            {
                var tile=Panel(goal.transform,"Goal tile "+(i+1),7+i%n*(cell+gap),7+i/n*(cell+gap),cell,cell,UiFactory.Hex("F5F1D9"),4);
                Label(tile.transform,"Goal number",(i+1).ToString(),0,0,cell,cell,14,UiFactory.Ink,TextAnchor.MiddleCenter);
            }
            Label(panel,"Rule goal","从左到右、从上到下排列，空格留在右下角。\n已经归位的数字仍可移动，无需合并数字。",21,344,300,46,13,Secondary);
            Label(panel,"Rule assistance","首次移动开始计时，离开或打开弹窗暂停。\n撤回增加 1 步并标记练习，闯关仍可解锁。",21,398,300,46,13,Secondary);
            Button(panel,"Rules done","明白了",21,457,300,42,()=>{CloseDialog();CheckEndState();},true,14);
            dismissDialog=()=>{CloseDialog();CheckEndState();};
        }
        private void ConfirmRestart()
        {
            if(!CanNavigate||Session==null)return;
            bool free=profile.SelectedMode==0;
            var panel=OpenDialog(free?"重新开始":"重玩本关？",free?304:250);
            dialogKind=DialogKind.Restart;
            Label(panel,"Restart description",free?"想再试一次，还是换个局面？\n当前尺寸的对局将被替换。":"回到本关初始局面，重新计时、计步。\n已解锁进度和个人纪录会保留。",21,79,300,48,13,Secondary);
            Button(panel,"Replay same",free?"重玩这题":"重玩本关",21,147,300,42,Restart,true,14);
            if(free)Button(panel,"New puzzle","换一题",21,199,300,42,NewFree,false,14);
            Button(panel,"Cancel restart","继续当前对局",21,free?251:199,300,32,CloseDialog,false,13);
        }
        public void CheckEndState()
        {
            if(!visible||!foreground||choosing||settingsBlocked||dialog!=null||!acceptsInput()||Session==null||!Session.Game.Won||IsAnimating||resultDismissed)return;
            Save();resultDismissed=true;
            bool campaign=profile.SelectedMode==1;int index=profile.ActiveLevelIndex;
            bool next=campaign&&index+1<SlidingPuzzleCatalog.Count;
            var panel=OpenDialog("",390);
            dialogKind=DialogKind.Result;
            var icon=UiFactory.CirclePanel(panel,"Success circle",143*Scale,25*Scale,56*Scale,UiFactory.Hex("E7EDE4"));
            Label(icon.transform,"Success check","✓",0,0,56,56,27,UiFactory.Accent,TextAnchor.MiddleCenter);
            Label(panel,"Success title","每一格，都刚刚好。",21,96,300,40,24,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            string message=campaign?(next?"本关完成 · 下一关已解锁":"全部 18 关已完成"):"本局完成";
            if(Session.Game.Assisted)message+=" · 练习局";
            Label(panel,"Success message",message,21,145,300,22,13,Secondary,TextAnchor.MiddleCenter);
            var time=Panel(panel,"Result time",21,185,145,72,UiFactory.Hex("E7EDE4"),10);
            var moves=Panel(panel,"Result moves",176,185,145,72,UiFactory.Hex("E7EDE4"),10);
            Label(time.transform,"Result time label","用时",10,9,125,20,12,Secondary,TextAnchor.MiddleCenter);
            SetNumber(Label(time.transform,"Result time value","",10,30,125,35,26,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold),FormatTime(Session.ElapsedSeconds));
            Label(moves.transform,"Result moves label","步数",10,9,125,20,12,Secondary,TextAnchor.MiddleCenter);
            SetNumber(Label(moves.transform,"Result moves value","",10,30,125,35,26,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold),Session.Game.Moves.ToString());
            Button(panel,"Result continue",campaign?(next?"下一关":"返回选关"):"再来一局",21,282,300,42,()=>
            {CloseDialog();if(campaign){if(next)StartLevel(index+1);else ShowLevels();}else NewFree();},true,14);
            Button(panel,"View solved board","查看棋盘",21,334,300,36,()=>{CloseDialog();Refresh();},false,13);
            dismissDialog=()=>{CloseDialog();Refresh();};
        }
    }
}
