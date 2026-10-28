using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.LinkMatch;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed partial class LinkMatchPage
    {
        private void BuildEntry()
        {
            float s=S,w=(float)layout.BoardWidth,top=(float)(layout.NavHeight+layout.HeadingHeight),margin=(float)layout.Margin;
            var viewport=UiFactory.Rect(content,"LinkMatch entry viewport",margin,top,w,root.rect.height-top-(float)layout.Bottom);
            var clip=viewport.gameObject.AddComponent<RectMask2D>();
            var background=viewport.gameObject.AddComponent<Image>();background.color=Color.clear;background.raycastTarget=true;
            var list=UiFactory.Rect(viewport,"LinkMatch entry content",0,0,w,600*s);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=list;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=true;
            float y=1*s;
            if(warningShown) {BuildWarning(list,0,y,w);y+=40*s;}
            Label(list,"相同的图案，刚好的一条路。",0,y,w,22*s,layout.Compact?11:12,UiFactory.Muted);y+=(layout.Compact?32:42)*s;
            var active=profile.GetLatestActive();bool resume=active!=null;var mode=resume?active.Mode:profile.SelectedMode;
            float padding=(layout.Compact?13:16)*s,cardHeight=(layout.Compact?150:176)*s,mainHeight=(layout.Compact?70:84)*s,ctaHeight=(layout.Compact?42:44)*s;
            var card=Button(list,"Continue game",0,y,w,cardHeight,ContinueGame,17*s,UiFactory.Hex("E6ECDF"));
            var border=card.GetComponent<LinkMatchGraphic>();border.BorderWidth=s;border.BorderColor=UiFactory.Hex("D5DFCC");
            Label(card.transform,resume?"接着上次的"+ModeName(mode):"从第一对水果开始",padding,padding,w-2*padding-95*s,17*s,10,UiFactory.Hex("6E8066"));
            Label(card.transform,resume?"第 "+LinkMatchLevel.Get(active.LevelId).Number.ToString("00")+" 关":"来一局"+ModeName(mode)+"连连看",padding,padding+25*s,w-2*padding-95*s,30*s,layout.Compact?18:20,UiFactory.Ink);
            string detail=resume?CountRemaining(active.Tiles)+" 张待配 · 已用时 "+Clock(System.Math.Floor(active.ElapsedSeconds)):"第 01 关 · 24 张 · "+(mode==LinkMatchMode.Relaxed?"不限时":"90 秒");
            Label(card.transform,detail,padding,padding+58*s,w-2*padding,17*s,10,UiFactory.Muted);
            float artWidth=(layout.Compact?91:104)*s,fruitSize=(layout.Compact?45:52)*s,artX=w-padding-artWidth;
            Fruit(card.transform,"Entry strawberry",0,artX+8*s,padding,fruitSize,13);
            Fruit(card.transform,"Entry banana",4,artX+artWidth-fruitSize+3*s,padding+16*s,fruitSize,-15);
            Fruit(card.transform,"Entry grapes",6,artX+17*s,padding+mainHeight-fruitSize+6*s,fruitSize,-10);
            float ctaY=padding+mainHeight+(layout.Compact?10:14)*s;
            var cta=Shape(card.transform,"Continue action",padding,ctaY,w-2*padding,ctaHeight,10*s,UiFactory.Accent);
            Label(cta.transform,resume?"继续游戏":"开始"+ModeName(mode)+"第一关",13*s,0,w-2*padding-50*s,ctaHeight,12,UiFactory.Paper);
            Label(cta.transform,"→",w-2*padding-32*s,0,20*s,ctaHeight,20,UiFactory.Paper,TextAnchor.MiddleCenter);
            y+=cardHeight+(layout.Compact?15:22)*s;
            float tabHeight=(layout.Compact?44:48)*s;
            var tabs=Shape(list,"LinkMatch modes",0,y,w,tabHeight,11*s,UiFactory.Hex("E8ECE2"));float tw=(w-12*s)/2;
            foreach(var entryMode in new[]{LinkMatchMode.Relaxed,LinkMatchMode.Challenge})
            {
                var chosen=entryMode;bool current=profile.SelectedMode==chosen;float x=chosen==LinkMatchMode.Relaxed?4*s:8*s+tw;
                var button=Button(tabs.transform,chosen==LinkMatchMode.Relaxed?"Mode relaxed":"Mode challenge",x,4*s,tw,tabHeight-8*s,()=>SelectMode(chosen),8*s,current?UiFactory.Hex("F9F8F2"):Color.clear);
                Label(button.transform,ModeName(chosen),0,0,tw,tabHeight-8*s,13,current?UiFactory.Accent:UiFactory.Hex("7C8979"),TextAnchor.MiddleCenter,current?FontStyle.Bold:FontStyle.Normal);
            }
            y+=tabHeight+8*s;
            Label(list,profile.SelectedMode==LinkMatchMode.Relaxed?"不限时 · 提示和洗牌随心用":"限时完成 · 每关 3 次提示、2 次洗牌",2*s,y,w-4*s,18*s,10,UiFactory.Muted);
            y+=(layout.Compact?31:36)*s;
            Label(list,"挑一关开始",2*s,y,w/2,19*s,13,UiFactory.Ink);
            Label(list,"全部开放",w-100*s,y,98*s,19*s,10,UiFactory.Hex("8B9586"),TextAnchor.MiddleRight);y+=29*s;
            foreach(var level in LinkMatchLevel.All)
            {
                var chosen=level;var slot=profile.GetSlot(profile.SelectedMode,level.Id);bool ongoing=slot!=null&&slot.Outcome==LinkMatchOutcome.Playing;
                float height=(layout.Compact?58:64)*s;
                var button=Button(list,"Entry level "+level.Id,0,y,w,height,()=>StartLevel(profile.SelectedMode,chosen.Id),12*s,UiFactory.Hex(ongoing?"EDF1E7":"FAF9F3"));
                var shape=button.GetComponent<LinkMatchGraphic>();shape.BorderWidth=s;shape.BorderColor=UiFactory.Hex(ongoing?"9DB58D":"E0E5D7");
                Label(button.transform,level.Number.ToString("00"),13*s,0,27*s,height,21,UiFactory.Hex("617E55"));
                Label(button.transform,level.Columns*level.Rows+" 张 · "+level.Kinds+" 种图案",53*s,(layout.Compact?8:11)*s,w-104*s,18*s,12,UiFactory.Ink);
                Label(button.transform,level.Columns+" × "+level.Rows+" · "+(profile.SelectedMode==LinkMatchMode.Relaxed?"不限时":level.ChallengeSeconds+" 秒"),53*s,(layout.Compact?31:34)*s,w-104*s,15*s,10,UiFactory.Hex("82907C"));
                Label(button.transform,ongoing?"继续":profile.GetRecord(profile.SelectedMode,level.Id).Wins>0?"已完成":"›",w-50*s,0,37*s,height,ongoing?10:12,UiFactory.Hex("64815A"),TextAnchor.MiddleRight);
                y+=height+8*s;
            }
            list.sizeDelta=new Vector2(w,y);scroll.verticalNormalizedPosition=1;
        }
        private static int CountRemaining(int[] tiles) {int count=0;foreach(int tile in tiles)if(tile>=0)count++;return count;}
    }
}
