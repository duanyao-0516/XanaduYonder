using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.LinkMatch;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed partial class LinkMatchPage
    {
        private Text remainingText,timeText,hintText,shuffleText,warningText,toastText;
        private RectTransform warningPanel,toastPanel;
        private Button hintButton,shuffleButton;
        private bool warningShown;
        private float S { get { return (float)layout.Scale; } }
        private string WarningText { get { return !string.IsNullOrEmpty(warning)&&!string.IsNullOrEmpty(hostSaveWarning)?warning+"；"+hostSaveWarning:warning??hostSaveWarning; } }
        internal void LayoutNow()
        {
            if(root==null||profile==null) return;
            pointer?.Cancel();lastSize=root.rect.size;
            var level=session==null?LinkMatchLevel.Get(1):session.Game.Level;
            warningShown=!string.IsNullOrEmpty(WarningText);
            layout=new LinkMatchLayout(Math.Max(1,root.rect.width),Math.Max(1,root.rect.height),warningShown,level.Columns,level.Rows);
            if(content!=null) {content.gameObject.SetActive(false);Destroy(content.gameObject);}
            if(dialog!=null) {dialog.SetActive(false);Destroy(dialog);dialog=null;}
            content=UiFactory.Rect(root,"LinkMatch content",0,0,root.rect.width,root.rect.height);UiFactory.Stretch(content);
            float s=S,w=root.rect.width,margin=(float)layout.Margin;
            Shape(content,"LinkMatch paper",0,0,w,root.rect.height,0,UiFactory.Paper);
            float navInset=layout.CssWidth<=370?8:13.5f,ny=layout.Compact?2:8;
            Nav("Home",UiIconKind.Home,navInset,ny,Home);
            Nav("Book",UiIconKind.Book,(float)layout.CssWidth-navInset-88-(layout.CssWidth<=370?0:4),ny,ShowRules);
            Nav("Settings",UiIconKind.Settings,(float)layout.CssWidth-navInset-44,ny,Settings);
            Label(content,"XANADU / YONDER",(w-150*s)/2,ny*s,150*s,44*s,layout.CssWidth<=370?8:9,UiFactory.Muted,TextAnchor.MiddleCenter).name="LinkMatch brand";
            var title=Label(content,"连连看",margin,(float)layout.NavHeight,w-2*margin,(float)layout.HeadingHeight-(layout.Compact?3:7)*s,layout.Compact?25:27,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);title.name="LinkMatch title";
            if(isEntry)
            {
                Label(content,"闲下来，连一连",w-margin-95*s,(float)layout.NavHeight,95*s,(float)layout.HeadingHeight-7*s,9,UiFactory.Muted,TextAnchor.MiddleRight);
                BuildEntry();board=null;surface=null;pointer=null;
            }
            else BuildGame();
            if(dialogKind!=null) BuildDialog();
            RefreshBoard();
        }
        private void BuildGame()
        {
            float s=S,margin=(float)layout.Margin,w=(float)layout.BoardWidth,top=(float)(layout.NavHeight+layout.HeadingHeight),info=(float)layout.InfoHeight;
            var information=UiFactory.Rect(content,"LinkMatch information",margin,top,w,info);
            var levelButton=Button(information,"Level selection",0,0,90*s,info,ShowEntry,0,Color.clear);
            Label(levelButton.transform,"第 "+session.Game.Level.Number.ToString("00")+" 关",0,0,64*s,info,layout.Compact?11:12,UiFactory.Ink).name="LinkMatch level";
            Symbol(levelButton.transform,"Chevron",LinkMatchGraphicKind.Chevron,65*s,(info-9*s)/2,9*s,9*s);
            float timeW=(layout.CssWidth<=370?65:72)*s,countW=62*s,modeW=30*s,infoGap=8*s;
            Label(information,ModeName(session.Mode),w-timeW-countW-modeW-2*infoGap,0,modeW,info,10,UiFactory.Hex("6E8665"));
            remainingText=Label(information,"",w-timeW-countW-infoGap,0,countW,info,10,UiFactory.Muted,TextAnchor.MiddleRight);remainingText.name="LinkMatch remaining";
            timeText=Label(information,"",w-timeW,0,timeW,info,10,UiFactory.Muted,TextAnchor.MiddleRight);timeText.name="LinkMatch time";
            remainingText.supportRichText=timeText.supportRichText=true;
            Shape(information,"Information divider",0,info-s,w,s,0,UiFactory.Hex("DCE2D6"));
            if(warningShown) BuildWarning(content,margin,(float)layout.BoardTop-40*s,w);
            var bg=Shape(content,"LinkMatch board",margin,(float)layout.BoardTop,w,(float)layout.BoardHeight,15*s,UiFactory.Hex("EBEEE2"));bg.BorderWidth=s;bg.BorderColor=UiFactory.Hex("DCE2D6");bg.raycastTarget=true;
            surface=bg.rectTransform;board=new LinkMatchBoardView(surface);pointer=surface.gameObject.AddComponent<LinkMatchPointer>();
            pointer.Surface=surface;pointer.Layout=()=>layout;pointer.Allowed=()=>CanPlay&&!removalFeedback;pointer.Submit=SelectTile;
            toastPanel=Shape(surface,"LinkMatch toast",12*s,(float)layout.BoardHeight-42*s,w-24*s,30*s,9*s,UiFactory.Ink).rectTransform;
            toastText=Label(toastPanel,"",8*s,0,w-40*s,30*s,10,UiFactory.Paper,TextAnchor.MiddleCenter);toastPanel.gameObject.SetActive(false);
            var operations=UiFactory.Rect(content,"LinkMatch operations",margin,(float)(layout.BoardTop+layout.BoardHeight+layout.ToolGap),w,(float)layout.ToolHeight);
            float gap=(layout.Compact?8:9)*s,bw=(w-gap*2)/3,bh=(float)layout.ToolHeight;
            hintButton=Tool(operations,"Hint",0,bw,bh,LinkMatchGraphicKind.Hint,"提示",Hint,out hintText);
            shuffleButton=Tool(operations,"Shuffle",bw+gap,bw,bh,LinkMatchGraphicKind.Shuffle,"洗牌",Shuffle,out shuffleText);
            Tool(operations,"Pause",2*(bw+gap),bw,bh,LinkMatchGraphicKind.Pause,"暂停",ShowPause,out var unused);
            RefreshCounts();
        }
        private Button Tool(Transform parent,string name,float x,float width,float height,LinkMatchGraphicKind kind,string text,Action action,out Text count)
        {
            var button=Button(parent,name,x,0,width,height,action,12*S,UiFactory.Hex("E7EDE4"));float block=(name=="Pause"?50:63)*S,start=(width-block)/2;
            Symbol(button.transform,name+" icon",kind,start,(height-19*S)/2,19*S,19*S);
            Label(button.transform,text,start+26*S,0,27*S,height,layout.Compact?11:12,UiFactory.Ink,TextAnchor.MiddleCenter);
            count=Label(button.transform,"",start+54*S,0,14*S,height,10,UiFactory.Muted,TextAnchor.MiddleCenter);return button;
        }
        private void BuildWarning(Transform parent,float x,float y,float width)
        {
            warningPanel=Shape(parent,"LinkMatch warning",x,y,width,34*S,8*S,UiFactory.Hex("F0E5CD")).rectTransform;
            warningText=Label(warningPanel,WarningText,9*S,0,width-54*S,34*S,9,UiFactory.Hex("806136"));warningText.resizeTextForBestFit=true;warningText.resizeTextMinSize=Mathf.RoundToInt(7*S);warningText.resizeTextMaxSize=Mathf.RoundToInt(9*S);
            var retry=Button(warningPanel,"Retry save",width-44*S,0,44*S,34*S,RetrySave,0,Color.clear);Label(retry.transform,"重试",0,0,44*S,34*S,10,UiFactory.Hex("805F26"),TextAnchor.MiddleCenter);
        }
        private void RetrySave()
        {
            Save();
            // Retry the owning host transaction without changing unrelated settings.
            if(!string.IsNullOrEmpty(hostSaveWarning)) RetryHostSave?.Invoke();
        }
        private void RefreshWarning()
        {
            if(content==null||layout==null)return;
            bool show=!string.IsNullOrEmpty(WarningText);
            if(show!=warningShown) {CancelTransient();LayoutNow();return;}
            if(warningText!=null)warningText.text=WarningText??"";
        }
        private void RefreshCounts()
        {
            if(isEntry||session==null||timeText==null)return;
            int font=Mathf.RoundToInt((layout.CssWidth<=370?12:13)*S);
            remainingText.text="剩余 <color=#173D35><size="+font+">"+session.Game.Remaining+"</size></color> 张";
            bool timed=session.Mode==LinkMatchMode.Challenge;
            timeText.text=(timed?"倒计时 ":"用时 ")+"<color="+(timed?"#A06D32":"#173D35")+"><size="+font+">"+Clock(timed?session.RemainingSeconds:Math.Floor(session.ElapsedSeconds))+"</size></color>";
            hintText.text=timed?session.HintsRemaining.ToString():"∞";shuffleText.text=timed?session.ShufflesRemaining.ToString():"∞";
            hintButton.interactable=!session.IsTerminal&&(!timed||session.HintsRemaining>0);shuffleButton.interactable=!session.IsTerminal&&(!timed||session.ShufflesRemaining>0);
        }
        private void RefreshBoard()
        {
            if(isEntry||session==null||board==null)return;
            board.Render(session.Game,layout,selected,hintA,hintB);board.ShowPath(shownPath,layout);
            board.Conceal(dialog!=null||settingsBlocked||!foreground);
        }
        private void Toast(string text)
        {if(toastPanel==null)return;toastText.text=text;toastRemaining=2.2f;toastPanel.SetAsLastSibling();toastPanel.gameObject.SetActive(true);}
        private void Nav(string name,UiIconKind kind,float x,float y,Action action)
        {
            var button=Button(content,name,x*S,y*S,44*S,44*S,action,0,Color.clear);
            Shape(button.transform,name+" background",6*S,6*S,32*S,32*S,10*S,UiFactory.Hex("E7EDE4"));
            var icon=UiFactory.Rect(button.transform,name+" icon",12*S,12*S,20*S,20*S).gameObject.AddComponent<UiIconGraphic>();icon.Kind=kind;icon.color=UiFactory.Ink;icon.raycastTarget=false;
        }
        private Text Label(Transform parent,string text,float x,float y,float width,float height,float size,Color color,TextAnchor align=TextAnchor.MiddleLeft,FontStyle style=FontStyle.Normal)
        {return UiFactory.Label(parent,text,x,y,width,height,Mathf.RoundToInt(size*S),color,align,style);}
        private LinkMatchGraphic Shape(Transform parent,string name,float x,float y,float width,float height,float radius,Color color)
        {var graphic=UiFactory.Rect(parent,name,x,y,width,height).gameObject.AddComponent<LinkMatchGraphic>();graphic.Radius=radius;graphic.color=color;graphic.raycastTarget=false;return graphic;}
        private LinkMatchGraphic Symbol(Transform parent,string name,LinkMatchGraphicKind kind,float x,float y,float width,float height)
        {var graphic=Shape(parent,name,x,y,width,height,0,UiFactory.Ink);graphic.Kind=kind;return graphic;}
        private Button Button(Transform parent,string name,float x,float y,float width,float height,Action action,float radius,Color color)
        {
            var graphic=Shape(parent,name,x,y,width,height,radius,color);graphic.raycastTarget=true;
            var button=graphic.gameObject.AddComponent<Button>();button.targetGraphic=graphic;
            var colors=button.colors;colors.highlightedColor=Color.white;colors.selectedColor=Color.white;colors.pressedColor=new Color(.9f,.94f,.89f);colors.disabledColor=new Color(1,1,1,.4f);button.colors=colors;
            var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
            button.onClick.AddListener(()=>{pointer?.Cancel();action();});return button;
        }
        private RawImage Fruit(Transform parent,string name,int index,float x,float y,float size,float angle=0)
        {
            var image=UiFactory.Rect(parent,name,x,y,size,size).gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("LinkMatch/Sprites/"+LinkMatchBoardView.FruitIds[index]);image.raycastTarget=false;
            if(angle!=0){image.rectTransform.pivot=new Vector2(.5f,.5f);image.rectTransform.anchoredPosition+=new Vector2(size/2,-size/2);image.rectTransform.localEulerAngles=new Vector3(0,0,angle);}
            return image;
        }
    }
}
