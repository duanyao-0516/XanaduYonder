using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed partial class SokobanPage
    {
        private RectTransform BeginDialog(string heading,float height)
        {
            CancelInput(); CloseDialog(); feedback.Stop(); session.SetBlocked(true); Save();
            var overlay=UiFactory.Panel(root,"Sokoban dialog overlay",0,0,720,1320,new Color(.094f,.208f,.173f,.40f),false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget=true; dialog=overlay.gameObject;
            height=Mathf.Min(height,root.rect.height-80);
            var panel=Shape(overlay.transform,"Sokoban dialog",0,0,636,height,40,UiFactory.Paper); UiFactory.Center(panel.rectTransform); panel.raycastTarget=true;
            var title=UiFactory.Label(panel.transform,heading,44,36,548,74,37,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); title.name="Sokoban dialog title";
            var close=UiFactory.IconButton(panel.transform,UiIconKind.Close,548,22,66,66,()=>dismissDialog?.Invoke()); close.name="Close dialog";
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
            return panel.rectTransform;
        }
        private void CloseDialog()
        {
            if(dialog!=null) { dialog.SetActive(false); Destroy(dialog); dialog=null; }
            choosing=false; dismissDialog=null; if(!settingsBlocked) session?.SetBlocked(false);
        }
        private void ShowDialog(string title,string message,string primary,Action primaryAction,string secondary=null,Action secondaryAction=null,float height=610)
        {
            var panel=BeginDialog(title,height); float h=panel.rect.height;
            var body=UiFactory.Label(panel,message,44,132,548,h-(secondary==null?262:350),24,UiFactory.Muted,TextAnchor.UpperLeft); body.supportRichText=false;
            float y=h-(secondary==null?116:204);
            var main=UiFactory.Button(panel,primary,44,y,548,76,()=>{ CloseDialog(); primaryAction?.Invoke(); Refresh(); },true); main.name="Dialog primary";
            if(secondary!=null)
            { var second=UiFactory.Button(panel,secondary,44,y+90,548,70,()=>{ CloseDialog(); secondaryAction?.Invoke(); Refresh(); }); second.name="Dialog secondary"; }
            dismissDialog=()=>{ CloseDialog(); secondaryAction?.Invoke(); Refresh(); };
        }
        internal void ShowRules()
        {
            ShowDialog("推箱子玩法","把所有箱子推到绿色目标上即可过关。\n只能推一只箱子，不能拉回或穿过墙。\n\n方向盘或方向键 / WASD：移动一格。\n适配 100%：棋盘内滑动一次，走一步。\n放大后：单指拖动查看，双指连续缩放。\n点击「适配」恢复完整地图。\n\n走动计一步；推动同时计一步和一次推动。\n卡住时可以连续撤回，或重来本关。\n全部关卡开放，选关可查看完成与个人纪录。\n只保留一场进行中的尝试，切关前会确认。", "知道了",()=>{},null,null,920);
        }
        internal void ConfirmRestart()
        {
            if(session==null||dialog!=null) return;
            ShowDialog("重新开始这一关？","当前尝试的步数与撤回记录将清空。\n已完成关卡与个人最佳纪录会保留。","重新开始",Restart,"继续游戏",()=>{},510);
        }
        internal void ShowLevels()
        {
            var panel=BeginDialog("选择关卡",Mathf.Min(1150,root.rect.height-80)); choosing=true;
            UiFactory.Label(panel,"已完成 "+CompletedCount+" / "+catalog.Count+"  ·  全部关卡开放",44,114,548,42,22,UiFactory.Muted);
            var viewportRect=UiFactory.Rect(panel,"Sokoban level list viewport",32,174,572,panel.rect.height-210);
            var mask=viewportRect.gameObject.AddComponent<Image>(); mask.color=Color.clear; mask.raycastTarget=true; viewportRect.gameObject.AddComponent<RectMask2D>();
            var content=UiFactory.Rect(viewportRect,"Sokoban level list",0,0,572,1);
            var scroll=viewportRect.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewportRect; scroll.content=content; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            float y=0; string chapter=null;
            for(int i=0;i<catalog.Count;i++)
            {
                var level=catalog.At(i); string id=level.LevelId;
                if(chapter!=level.Chapter)
                {
                    chapter=level.Chapter; string caption=chapter=="tutorial"?"入门教学 · 8 关":"仓库 "+((i-8)/20+1)+" · 20 关";
                    UiFactory.Label(content,caption,12,y,548,54,24,UiFactory.Accent,TextAnchor.MiddleLeft,FontStyle.Bold); y+=62;
                }
                var row=UiFactory.Button(content,"",4,y,564,100,()=>RequestLevel(id)); row.name="Level "+id;
                UiFactory.Label(row.transform,(i+1).ToString("00")+"  "+level.Title,18,9,440,44,24,UiFactory.Ink);
                var record=profile.GetRecord(id);
                string detail=record==null?"尚未完成":("最佳步数 "+record.BestMoves.Moves+" · "+record.BestMoves.Pushes+" 推动");
                UiFactory.Label(row.transform,detail,18,55,440,30,18,UiFactory.Muted);
                UiFactory.Label(row.transform,profile.IsCompleted(id)?"已完成":"进入",458,16,88,60,20,profile.IsCompleted(id)?UiFactory.Accent:UiFactory.Muted,TextAnchor.MiddleCenter);
                y+=112;
            }
            content.sizeDelta=new Vector2(572,y); scroll.verticalNormalizedPosition=1;
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
        }
        private void RequestLevel(string id)
        {
            if(id==session.Game.Level.LevelId)
            {
                if(session.Game.Won) ShowRecords(id);
                else { CloseDialog(); Refresh(); }
                return;
            }
            if(session.Game.Moves>0&&!session.Game.Won)
            {
                ShowDialog("切换关卡？","只保留一场进行中的尝试。\n切换后，当前关的步数和撤回记录将丢弃；\n已完成关卡和个人最佳仍会保留。","切换关卡",()=>StartLevel(id,true),"取消",ShowLevels,580);
            }
            else if(profile.IsCompleted(id)) ShowRecords(id);
            else StartLevel(id,true);
        }
        private void ShowRecords(string id)
        {
            var record=profile.GetRecord(id); var level=catalog.Get(id);
            string message=level.Title+"\n\n"+(record==null?"尚未完成":
                "最少步数："+record.BestMoves.Moves+" 步 · "+record.BestMoves.Pushes+" 推动\n\n最少推动："+record.BestPushes.Pushes+" 推动 · "+record.BestPushes.Moves+" 步");
            ShowDialog("个人纪录",message,"重玩这一关",()=>StartLevel(id,true),"返回选关",ShowLevels,620);
        }
        internal void CheckEndState()
        {
            if(!visible||!foreground||settingsBlocked||session==null||!session.Game.Won||resultDismissed||dialog!=null||board.Animating||!acceptsInput()) return;
            resultDismissed=true; queued=null; Save();
            string id=session.Game.Level.LevelId; int index=LevelNumber(id)-1;
            string message=session.Game.Level.Title+"\n\n"+session.Game.Moves+" 步  ·  "+session.Game.Pushes+" 次推动\n已完成 "+CompletedCount+" / "+catalog.Count+" 关"+(session.IsNewBest?"\n刷新了个人最佳纪录":"");
            if(index+1<catalog.Count)
                ShowDialog("全部归位",message,"下一关",()=>StartLevel(catalog.At(index+1).LevelId,true),"重玩本关",()=>StartLevel(id,true),610);
            else ShowDialog("全部归位",message,"选择关卡",ShowLevels,"重玩本关",()=>StartLevel(id,true),610);
            // Android Back closes the result before a subsequent Back opens the selector.
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
        }
    }
}
