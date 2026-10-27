using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed partial class MinesweeperPage
    {
        private RectTransform ShowDialog(string title,string message,string primary,Action primaryAction,string secondary=null,Action secondaryAction=null,string tertiary=null,Action tertiaryAction=null,float height=660)
        {
            CancelInput(); ExitOverview(); CloseDialog(); feedback.Stop();
            if(!choosing) session?.SetBlocked(true);
            Save(); Refresh();
            var overlay=UiFactory.Panel(root,"Minesweeper dialog overlay",0,0,720,1320,new Color(.06f,.14f,.1f,.48f),false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget=true; dialog=overlay.gameObject;
            var panel=UiFactory.Panel(overlay.transform,"Minesweeper dialog",0,0,596,height,UiFactory.Paper); UiFactory.Center(panel.rectTransform); panel.raycastTarget=true;
            UiFactory.Label(panel.transform,title,32,28,532,70,33,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            float actions=tertiary!=null?260:secondary!=null?172:90;
            var body=UiFactory.Label(panel.transform,message,32,116,532,height-actions-140,24,UiFactory.Muted,TextAnchor.MiddleCenter);
            body.name="Dialog message";
            body.supportRichText=false;
            float top=height-actions-12;
            UiFactory.Button(panel.transform,primary,32,top,532,74,()=>{CloseDialog();primaryAction();Refresh();},true);
            if(secondary!=null) UiFactory.Button(panel.transform,secondary,32,top+88,532,66,()=>{CloseDialog();secondaryAction?.Invoke();Refresh();});
            if(tertiary!=null) UiFactory.Button(panel.transform,tertiary,32,top+168,532,66,()=>{CloseDialog();tertiaryAction?.Invoke();Refresh();});
            dismissDialog=()=>{ CloseDialog(); secondaryAction?.Invoke(); Refresh(); };
            return panel.rectTransform;
        }
        private void CloseDialog()
        {
            if(dialog!=null) { dialog.SetActive(false); Destroy(dialog); dialog=null; }
            dismissDialog=null; session?.SetBlocked(false);
        }
        private void ShowTutorial()
        {
            Action finish=()=>{profile.TutorialSeen=true;Save();};
            var panel=ShowDialog("从数字开始", "数字表示周围八格中的雷数。\n\n点击翻开；长按后松手插旗。\n切换「插旗」模式可连续标记。\n\n单指拖动，双指缩放并平移。\n全屏扩大视野，总览只观察和定位。\n\n首击安全，不保证全盘无猜。", "开始探索", finish,"跳过",finish,null,null,960);
            var message=(RectTransform)panel.Find("Dialog message");
            message.anchoredPosition=new Vector2(32,-294); message.sizeDelta=new Vector2(532,450);
            var diagram=UiFactory.Rect(panel,"Tutorial diagram",226,118,144,144);
            for(int i=0;i<9;i++)
            {
                var tile=UiFactory.Panel(diagram,"Tutorial cell "+i,i%3*48,i/3*48,44,44,UiFactory.Hex("E7EDE4"));
                if(i!=4) UiFactory.Label(tile.transform,"1",0,0,44,44,28,UiFactory.Accent,TextAnchor.MiddleCenter);
                else
                {
                    var flag=UiFactory.Rect(tile.transform,"Example flag",7,7,30,30).gameObject.AddComponent<MinesweeperIconGraphic>();
                    flag.Kind=MinesweeperIconKind.Flag; flag.color=UiFactory.Hex("B34842"); flag.ContrastFlagPole=true; flag.raycastTarget=false;
                }
            }
        }
        private void ShowRules()
        {
            ShowDialog("扫雷玩法", "翻开所有安全格即获胜，触雷立即结束。\n数字 1～8 表示周围八格的地雷总数。\n空白区域会自动展开，旗帜阻挡展开。\n\n翻开模式：点击翻格，长按后松手插旗。\n插旗模式：点击插旗或拔旗。\n已开数字周围旗数匹配时，点击快速展开。\n旗插错也可能触雷；剩余标记允许为负。\n\n单指拖动，双指同时缩放和平移。\n缩放 50%～200%，点击倍率恢复 100%。\n总览只观察与定位，进行中仍继续计时。\n全屏只改变布局，保留对局和观察位置。\n\n暂停会遮盘停时，返回进行局需明确继续。\n三档独立存档；放弃不计败，不提供悔棋。\n首格及周围一圈安全，不保证全盘无猜。", "知道了",()=>{},null,null,null,null,1100);
        }
    }
}
