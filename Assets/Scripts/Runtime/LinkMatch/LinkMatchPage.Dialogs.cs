using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.LinkMatch;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed partial class LinkMatchPage
    {
        internal void ShowPause() { if(isEntry||session==null||session.IsTerminal) {CheckEndState();return;} TickClock();if(session.IsTerminal){CheckEndState();return;}ShowDialog("pause");Save(); }
        internal void ResumeGame()
        {
            if(!foreground||settingsBlocked||session==null||session.IsTerminal) {CheckEndState();return;}
            CloseDialog();CancelTransient();session.SetBlocked(false);lastClock=Time.realtimeSinceStartupAsDouble;RecoverNoMove();RefreshBoard();
        }
        internal void ShowRules() {TickClock();if(!isEntry&&session!=null&&session.IsTerminal){CheckEndState();return;}ShowDialog("help");Save();}
        internal void ConfirmRestart() {if(session==null||isEntry)return;TickClock();ShowDialog("restart");Save();}
        private void Settings()
        {
            if(settingsBlocked)return;TickClock();CancelTransient();CloseDialog();settingsBlocked=true;session?.SetBlocked(true);RefreshBoard();Save();
            openSettings(()=>{settingsBlocked=false;lastClock=Time.realtimeSinceStartupAsDouble;if(!isEntry&&session!=null){if(session.IsTerminal)CheckEndState();else ShowPause();}else RefreshBoard();});
        }
        private void CloseDialog()
        {
            if(dialog!=null){dialog.SetActive(false);Destroy(dialog);dialog=null;}dialogKind=null;
        }
        private void ShowDialog(string kind)
        {
            TickClock();CancelTransient();CloseDialog();dialogKind=kind;session?.SetBlocked(true);BuildDialog();RefreshBoard();
        }
        private void BuildDialog()
        {
            if(dialogKind==null)return;
            float s=S,cssWidth=(float)layout.CssWidth,width=Math.Min(316,cssWidth-48)*s;
            var shade=Shape(root,"LinkMatch dialog shade",0,0,root.rect.width,root.rect.height,0,new Color(23/255f,61/255f,53/255f,.36f));shade.raycastTarget=true;dialog=shade.gameObject;
            bool result=dialogKind=="won",help=dialogKind=="help",restart=dialogKind=="restart",timeout=dialogKind=="timeout";
            float height=(result?428:help?423:restart?294:timeout?294:346)*s;
            height=Math.Min(height,root.rect.height-48*s);
            var panel=Shape(shade.transform,"LinkMatch dialog",(root.rect.width-width)/2,(root.rect.height-height)/2,width,height,20*s,UiFactory.Paper);
            panel.BorderWidth=s;panel.BorderColor=UiFactory.Hex("D9DFD0");
            if(help||dialogKind=="pause")
            {
                // Dismiss preserves the paused attempt; only the primary action resumes play.
                var close=Button(panel.transform,"Dialog close",width-47*s,6*s,40*s,40*s,
                    ()=>{if(dialogKind=="pause")ShowEntry();else Back();},0,Color.clear);
                Label(close.transform,"×",0,0,40*s,40*s,24,UiFactory.Hex("778672"),TextAnchor.MiddleCenter);
            }
            float inner=width-48*s,y=31*s;bool center=result||dialogKind=="pause"||timeout;
            var align=center?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
            if(result) {Fruit(panel.transform,"Result fruit",session.Game.Level.Number==LinkMatchLevel.Count?10:0,(width-76*s)/2,y,76*s);y+=94*s;}
            string kicker=help?"一条路，两次转弯":session==null?"连连看":ModeName(session.Mode)+" · 第 "+session.Game.Level.Number.ToString("00")+" 关";
            Label(panel.transform,kicker,24*s,y,inner,15*s,10,UiFactory.Hex("859576"),align);y+=27*s;
            string title=help?"连连看玩法":restart?"重新开始这一关？":timeout?"时间到":result?(session.Game.Level.Number==LinkMatchLevel.Count?"第 "+session.Game.Level.Number.ToString("00")+" 关完成":"全部连起来了"):"歇一会儿";
            Label(panel.transform,title,24*s,y,inner,34*s,23,UiFactory.Ink,align,FontStyle.Bold).name="LinkMatch dialog title";y+=47*s;
            if(help)
            {
                Fruit(panel.transform,"Help strawberry left",0,40*s,y,44*s);Fruit(panel.transform,"Help strawberry right",0,width-84*s,y,44*s);
                var route=Symbol(panel.transform,"Help route",LinkMatchGraphicKind.Route,24*s,y,inner,63*s);route.Points=new[]{new Vector2(38*s,27*s),new Vector2(38*s,3*s),new Vector2(inner-38*s,3*s),new Vector2(inner-38*s,27*s)};route.Stroke=2.5f*s;route.color=UiFactory.Hex("729C51");y+=72*s;
                Label(panel.transform,"点击两张相同图案。连线不穿过其他牌，最多转弯两次，就能配成一对。棋盘外圈也可以走。",24*s,y,inner,74*s,12,UiFactory.Muted);y+=86*s;
                var helpCard=Shape(panel.transform,"Help summary",24*s,y,inner,76*s,10*s,UiFactory.Hex("E7EDE1"));
                Label(helpCard.transform,"配完所有图案就过关。轻松模式不限时；挑战模式需要在时间内完成。无路可连时会免费重新排列。",12*s,0,inner-24*s,76*s,12,UiFactory.Muted);y+=90*s;
                DialogButton(panel.transform,"Dialog primary",isEntry?"明白了":"继续游戏",24*s,y,inner,()=>{if(isEntry){CloseDialog();RefreshBoard();}else ResumeGame();},true);
            }
            else
            {
                string body=result?(session.Game.Level.Number==LinkMatchLevel.Count?"最后一关完成，再来一次也很开心。":"小小的配对，刚刚好的成就感。"):timeout?"本关未完成。\n再试一次，慢慢找到自己的节奏。":restart?"将替换这一关的当前进度。\n其他模式和关卡不受影响。":"图案在这里等你。\n回来时，从刚才那一步继续。";
                Label(panel.transform,body,24*s,y,inner,result?28*s:47*s,12,UiFactory.Muted,align);y+=(result?40:64)*s;
                if(result)
                {
                    Label(panel.transform,Clock(session.ElapsedSeconds),24*s,y,inner/2,30*s,21,UiFactory.Ink,TextAnchor.MiddleCenter);
                    Label(panel.transform,(session.Game.Level.Columns*session.Game.Level.Rows/2).ToString(),24*s+inner/2,y,inner/2,30*s,21,UiFactory.Ink,TextAnchor.MiddleCenter);
                    Label(panel.transform,"完成用时",24*s,y+32*s,inner/2,16*s,10,UiFactory.Muted,TextAnchor.MiddleCenter);Label(panel.transform,"成功配对",24*s+inner/2,y+32*s,inner/2,16*s,10,UiFactory.Muted,TextAnchor.MiddleCenter);y+=64*s;
                }
                string primary="继续游戏";Action primaryAction=ResumeGame;
                if(restart||timeout){primary=timeout?"重试本关":"重新开始";primaryAction=()=>StartLevel(session.Mode,session.Game.Level.Id,true);}
                if(result)
                {
                    if(session.Game.Level.Number==LinkMatchLevel.Count) {primary="再玩这一关";primaryAction=()=>StartLevel(session.Mode,session.Game.Level.Id,true);}
                    else {var next=LinkMatchLevel.Get(session.Game.Level.Number+1);var slot=profile.GetSlot(session.Mode,next.Id);primary=slot!=null&&slot.Outcome==LinkMatchOutcome.Playing?"继续下一关":"下一关";primaryAction=()=>StartLevel(session.Mode,next.Id);}
                }
                DialogButton(panel.transform,"Dialog primary",primary,24*s,y,inner,primaryAction,true);y+=55*s;
                DialogButton(panel.transform,"Dialog secondary",restart?"保留当前进度":"返回选关",24*s,y,inner,restart?(Action)ShowPause:ShowEntry,false);y+=55*s;
                if(dialogKind=="pause")
                {
                    var redo=Button(panel.transform,"Dialog restart",24*s,y,inner,24*s,ConfirmRestart,0,Color.clear);
                    Label(redo.transform,"重新开始本关",0,0,inner,24*s,10,UiFactory.Muted,TextAnchor.MiddleCenter);
                }
            }
        }
        private void DialogButton(Transform parent,string name,string label,float x,float y,float width,Action action,bool primary)
        {
            var button=Button(parent,name,x,y,width,46*S,action,11*S,primary?UiFactory.Accent:UiFactory.Hex("E6ECDF"));
            Label(button.transform,label,8*S,0,width-16*S,46*S,12,primary?UiFactory.Paper:UiFactory.Ink,TextAnchor.MiddleCenter);
        }
    }
}
