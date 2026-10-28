using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed partial class SokobanPage
    {
        private RectTransform BeginDialog(string heading,float cssHeight,bool picker=false)
        {
            CancelInput(); CloseDialog(); feedback.Stop(); session.SetBlocked(true); Save();
            float s=Scale,width=Mathf.Min((picker?320:360)*s,root.rect.width-32*s);
            var overlay=UiFactory.Panel(root,"Sokoban dialog overlay",0,0,root.rect.width,root.rect.height,new Color(.094f,.208f,.173f,.40f),false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget=true; dialog=overlay.gameObject;
            float height=Mathf.Min(cssHeight*s,root.rect.height-64*s);
            var panel=Shape(overlay.transform,"Sokoban dialog",0,0,width,height,22*s,UiFactory.Paper); UiFactory.Center(panel.rectTransform); panel.raycastTarget=true;
            panel.BorderWidth=s; panel.BorderColor=UiFactory.Hex("D4D8C9");
            float padding=(picker?25:29)*s;
            var headingText=Label(panel.transform,heading,padding,(picker?33:37)*s,width-2*padding,31*s,22,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); headingText.name="Sokoban dialog title";
            var close=ActionButton(panel.transform,"Close dialog",width-54*s,9*s,44*s,44*s,()=>dismissDialog?.Invoke(),0,true);
            Label(close.transform,"×",0,0,44*s,44*s,25,UiFactory.Ink,TextAnchor.MiddleCenter);
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
            return panel.rectTransform;
        }
        private void CloseDialog()
        {
            if(dialog!=null) { dialog.SetActive(false); dialog.transform.SetParent(null,false); Destroy(dialog); dialog=null; }
            choosing=false; dismissDialog=null; if(!settingsBlocked) session?.SetBlocked(false);
        }
        private void ShowDialog(string title,string message,string primary,Action primaryAction,string secondary=null,Action secondaryAction=null,float height=325)
        {
            var panel=BeginDialog(title,height); float s=Scale,h=panel.rect.height,w=panel.rect.width-58*s;
            float primaryY=h-(secondary==null?73:125)*s;
            var body=Label(panel,message,29*s,88*s,w,Mathf.Max(1,primaryY-100*s),13,UiFactory.Hex("64756B"),TextAnchor.UpperLeft);
            body.horizontalOverflow=HorizontalWrapMode.Wrap; body.supportRichText=false;
            var main=ActionButton(panel,"Dialog primary",29*s,primaryY,w,44*s,()=>{ CloseDialog(); primaryAction?.Invoke(); Refresh(); },11*s);
            main.GetComponent<SokobanGraphic>().color=UiFactory.Accent;
            Label(main.transform,primary,0,0,w,44*s,13,UiFactory.Hex("FFFDF4"),TextAnchor.MiddleCenter);
            if(secondary!=null)
            {
                var second=ActionButton(panel,"Dialog secondary",29*s,primaryY+52*s,w,44*s,()=>{ CloseDialog(); secondaryAction?.Invoke(); Refresh(); },11*s);
                Label(second.transform,secondary,0,0,w,44*s,13,UiFactory.Ink,TextAnchor.MiddleCenter);
            }
            dismissDialog=()=>{ CloseDialog(); secondaryAction?.Invoke(); Refresh(); };
        }
        internal void ShowRules()
        {
            ShowDialog("推箱子玩法","用方向盘、方向键或 WASD 移动。把全部木箱推到绿色标记上即可过关。一次只能推一个箱子，不能拉。\n\n棋盘始终完整显示，自动适配可用空间。卡住时，可以连续撤回，或重新开始这一关。\n\n关卡入口可以按章节挑选，也可以直接继续上次的搬运。游戏中点击关卡名可快速切换。\n\n走动计一步；推动同时计一步和一次推动。只保留一场进行中的尝试，切关前会确认。", "知道了",()=>{},null,null,440);
        }
        internal void ConfirmRestart()
        {
            if(!hasStarted||session==null||dialog!=null) return;
            ShowDialog("重新开始这一关？","本次尝试的步数与撤回记录将清空。\n已完成关卡与个人最佳纪录会保留。","重新开始",Restart,"继续游戏",()=>{},275);
        }
        internal void ShowLevels()
        {
            pickerChapter=session.Game.Level.Chapter; BuildLevelPicker();
        }
        private void BuildLevelPicker()
        {
            var levels=ChapterLevels(pickerChapter); bool tutorial=pickerChapter=="tutorial"; float s=Scale;
            float desiredList=tutorial?360:((levels.Count+4)/5)*44+Mathf.Max(0,(levels.Count+4)/5-1)*8+10;
            var panel=BeginDialog("选择关卡",209+desiredList,true); choosing=true;
            float inner=panel.rect.width-50*s,listHeight=Mathf.Max(44*s,panel.rect.height-209*s);
            var tabs=ScrollViewport(panel,"Sokoban picker chapters",25*s,84*s,inner,36*s);
            BuildChapterTabs(tabs,pickerChapter,true);
            var listViewport=ScrollViewport(panel,"Sokoban level list viewport",25*s,130*s,inner,listHeight);
            var content=UiFactory.Rect(listViewport,"Sokoban level list",0,0,inner,1);
            var scroll=Scroll(listViewport,content,false);
            float y=tutorial?6*s:5*s;
            if(tutorial)
            {
                foreach(var level in levels)
                {
                    string id=level.LevelId; var record=profile.GetRecord(id); bool active=IsActive(id);
                    bool detail=record!=null||active; float rowHeight=(detail?51.5f:44)*s;
                    var row=ActionButton(content,"Level "+id,0,y,inner,rowHeight,()=>RequestLevel(id),9*s);
                    if(active) { var shape=row.GetComponent<SokobanGraphic>(); shape.BorderWidth=s; shape.BorderColor=UiFactory.Hex("9AB08F"); }
                    float textWidth=inner-(record==null?20:76)*s;
                    var caption=Label(row.transform,DisplayLevelLabel(id),10*s,detail?8*s:0,textWidth,detail?17*s:rowHeight,12,UiFactory.Ink); Ellipsize(caption);
                    if(detail)
                    {
                        string subtitle=record!=null?"纪录 "+record.BestMoves.Moves+" 步 · "+record.BestMoves.Pushes+" 次推动":"当前关 · "+session.Game.Moves+" 步 · "+session.Game.GoalsFilled+"/"+session.Game.Level.BoxCount+" 归位";
                        var line=Label(row.transform,subtitle,10*s,30*s,textWidth,14*s,10,UiFactory.Hex("75816F")); Ellipsize(line);
                    }
                    if(record!=null) Label(row.transform,"✓ 已完成",inner-66*s,0,56*s,rowHeight,10,UiFactory.Hex("52764B"),TextAnchor.MiddleRight);
                    y+=rowHeight+6*s;
                }
            }
            else
            {
                float gap=8*s,w=(inner-4*gap)/5;
                for(int i=0;i<levels.Count;i++) LevelButton(content,"Level "+levels[i].LevelId,levels[i],i%5*(w+gap),5*s+i/5*(44*s+gap),w,44*s,false);
                y=desiredList*s;
            }
            content.sizeDelta=new Vector2(inner,y); scroll.verticalNormalizedPosition=1;
            float footerTop=130*s+listHeight+15*s;
            Shape(panel,"Picker footer divider",25*s,footerTop,inner,s,0,UiFactory.Hex("DDE3D5"));
            var entry=ActionButton(panel,"Return to entry",25*s,footerTop+11*s,140*s,28*s,ShowEntry,0,true);
            Label(entry.transform,"查看关卡入口",0,0,140*s,28*s,11,UiFactory.Hex("52764B"));
            Label(panel,CompletedCount+" / "+catalog.Count+" 已完成",25*s+inner-125*s,footerTop+11*s,125*s,28*s,10,UiFactory.Hex("87917F"),TextAnchor.MiddleRight);
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
        }
        private void RequestLevel(string id)
        {
            if(hasStarted&&id==session.Game.Level.LevelId&&!session.Game.Won) { StartLevel(id); return; }
            if(hasStarted&&session.Game.Moves>0&&!session.Game.Won&&id!=session.Game.Level.LevelId)
            {
                bool fromEntry=isEntry; string chapter=pickerChapter;
                ShowDialog("切换关卡？","只保留一场进行中的尝试。\n切换后，当前关的步数和撤回记录将丢弃；已完成关卡和个人最佳仍会保留。","切换关卡",()=>StartLevel(id,true),"取消",()=>{ if(!fromEntry) { pickerChapter=chapter??session.Game.Level.Chapter; BuildLevelPicker(); } },315);
            }
            else StartLevel(id,true);
        }
        internal void CheckEndState()
        {
            if(!visible||isEntry||!hasStarted||!foreground||settingsBlocked||session==null||!session.Game.Won||resultDismissed||dialog!=null||board.Animating||!acceptsInput()) return;
            resultDismissed=true; queued=null; Save();
            string id=session.Game.Level.LevelId; int index=CatalogIndex(id);
            string message=DisplayLevelLabel(id)+"\n"+session.Game.Moves+" 步 · "+session.Game.Pushes+" 次推动\n已完成 "+CompletedCount+" / "+catalog.Count+" 关"+(session.IsNewBest?"\n刷新了个人最佳纪录":"");
            if(index+1<catalog.Count)
                ShowDialog("全部归位",message,"下一关",()=>StartLevel(catalog.At(index+1).LevelId,true),"重玩本关",()=>StartLevel(id,true),325);
            else ShowDialog("全部归位",message,"返回关卡入口",ShowEntry,"重玩本关",()=>StartLevel(id,true),325);
            dismissDialog=()=>{ CloseDialog(); Refresh(); };
        }
    }
}
