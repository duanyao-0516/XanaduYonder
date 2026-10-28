using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    internal sealed partial class SlidingPuzzlePage
    {
        private const float ReferenceWidth = 390, ReferenceHeight = 820;
        private static readonly Color Secondary = UiFactory.Hex("65786D"), Locked = UiFactory.Hex("A2AAA3");
        private RectTransform page, content, boardRect;
        private Text timeText, movesText, footer;
        private Button undoButton;
        private float Scale { get { return Mathf.Min(root.rect.width / ReferenceWidth, root.rect.height / ReferenceHeight); } }
        private void Build()
        {
            // Re-layout is not a user dismissal. Recreate the current modal at the new scale.
            var previousDialog = dialogKind;
            lastSize = root.rect.size; CloseDialog();
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            float s = Scale;
            page = UiFactory.Rect(root, "SlidingPuzzle content", (root.rect.width - ReferenceWidth * s) / 2, 0, ReferenceWidth * s, root.rect.height);
            var home = UiFactory.IconButton(page, UiIconKind.Home, 28*s, 25*s, 58*s/(720f/390), 58*s/(720f/390), Home); home.name = "Home";
            var book = UiFactory.IconButton(page, UiIconKind.Book, 289.17f*s, 25*s, 58*s/(720f/390), 58*s/(720f/390), ShowRules); book.name = "Book";
            var settings = UiFactory.IconButton(page, UiIconKind.Settings, 330.58f*s, 25*s, 58*s/(720f/390), 58*s/(720f/390), Settings); settings.name = "Settings";
            Label(page,"SlidingPuzzle brand","XANADU / YONDER",105,25,180,31.42f,10,UiFactory.Muted,TextAnchor.MiddleCenter);
            Label(page,"SlidingPuzzle title","数字华容道",28,89,334,41.25f,33,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(page,"SlidingPuzzle subtitle","留一格空白，让数字慢慢归位。",28,140.25f,334,20.8f,13,Secondary);
            footer = Label(page,"SlidingPuzzle footer","",28,root.rect.height/s-37,334,20,11,Secondary,TextAnchor.MiddleCenter);
            footer.horizontalOverflow = HorizontalWrapMode.Overflow;
            BuildContent();
            if (previousDialog == DialogKind.Rules) ShowRules();
            else if (previousDialog == DialogKind.Restart) ConfirmRestart();
            else if (previousDialog == DialogKind.Result) { resultDismissed = false; CheckEndState(); }
        }
        private void BuildContent()
        {
            if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            content = UiFactory.Rect(page,"SlidingPuzzle state",0,0,ReferenceWidth*Scale,root.rect.height);
            board = null; pointer = null; timeText = movesText = null; undoButton = null;
            var modes = Panel(content,"Mode selector",28,183.05f,334,46,UiFactory.Hex("E7EDE4"),10);
            var free = Button(modes.transform,"Free mode","自由拼图",4,4,161,38,()=>SelectMode(0),profile.SelectedMode==0,14);
            var campaign = Button(modes.transform,"Campaign mode","解锁闯关",169,4,161,38,()=>SelectMode(1),profile.SelectedMode==1,14);
            SlidingPuzzleBoardView.Round(free.image,7*Scale);SlidingPuzzleBoardView.Round(campaign.image,7*Scale);
            if (profile.SelectedMode != 0) free.image.color = Color.clear;
            if (profile.SelectedMode != 1) campaign.image.color = Color.clear;
            if (choosing) BuildLevels(); else BuildGame();
            footer.transform.SetAsLastSibling();
        }
        private void BuildGame()
        {
            if (Session == null) return;
            bool free = profile.SelectedMode == 0;
            string modeTitle = free ? "经典 · 自由拼图" : "第 " + SlidingPuzzleCatalog.Get(profile.ActiveLevelIndex).Number.ToString("00") + " 关 · " + Session.Game.Size + "×" + Session.Game.Size;
            Label(content,"Game context",modeTitle,28,249.05f,185,32,14,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            if (free)
            {
                for (int n=3;n<=5;n++)
                {
                    int size=n; var choice=Button(content,"Size "+n,n+"×"+n,235+(n-3)*42,249.05f,40,32,()=>SelectSize(size),false,12);
                    SlidingPuzzleBoardView.Round(choice.image,6*Scale);
                    choice.image.color = profile.SelectedSize==n?UiFactory.Hex("E7EDE4"):Color.clear;
                    var text=choice.GetComponentInChildren<Text>(); text.color=profile.SelectedSize==n?UiFactory.Accent:Secondary; text.fontStyle=profile.SelectedSize==n?FontStyle.Bold:FontStyle.Normal;
                }
            }
            else { var levels=Button(content,"Choose levels","选择关卡 ›",260,249.05f,102,32,ShowLevels,false,12); levels.image.color=Color.clear; }
            var time=Panel(content,"Time card",28,293.05f,162,76.3f,UiFactory.Hex("E7EDE4"),10);
            var moves=Panel(content,"Moves card",200,293.05f,162,76.3f,UiFactory.Hex("E7EDE4"),10);
            Label(time.transform,"Time label","用时",14,11,134,19.2f,12,Secondary);
            Label(moves.transform,"Moves label","步数",14,11,134,19.2f,12,Secondary);
            timeText=Label(time.transform,"SlidingPuzzle time","00:00",14,30.2f,134,35.1f,26,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            movesText=Label(moves.transform,"SlidingPuzzle moves","0",14,30.2f,134,35.1f,26,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            var tray=Panel(content,"SlidingPuzzle board",28,384.35f,334,334,UiFactory.Hex("BBCBB6"),12); tray.raycastTarget=true;
            boardRect=tray.rectTransform; board=new SlidingPuzzleBoardView(boardRect); board.Render(Session.Game,Scale);
            pointer=tray.gameObject.AddComponent<SlidingPuzzlePointer>(); pointer.Initialize(boardRect,board,()=>Session.Game,()=>CanMove,Move,Scale);
            undoButton=Button(content,"Undo","撤回",28,735.35f,163,42,Undo,false,13);
            Button(content,"Restart",free?"重新开始":"重玩本关",199,735.35f,163,42,ConfirmRestart,false,13);
            RefreshCounts();
        }
        private static readonly string[] ChapterTitles={"从一格空白开始","进阶篇 · 4×4","挑战篇 · 5×5"};
        private static readonly string[] ChapterDescriptions={"先学会移动，再学会还原","更多空间，学会安排归位的顺序。","给想再多走一步的你。"};
        private void BuildLevels()
        {
            int chapter=profile.SelectedChapter;
            if(!profile.IsChapterUnlocked(chapter)) { profile.SelectChapter(0); chapter=0; }
            var card=Panel(content,"Chapter summary",28,249.05f,334,111.6f,UiFactory.Hex("E7EDE4"),11);
            Label(card.transform,"Chapter eyebrow",(chapter==0?"入门章节":chapter==1?"进阶章节":"挑战章节")+" / "+(chapter+3)+" × "+(chapter+3),17,22.5f,300,14,10,Secondary);
            Label(card.transform,"Chapter title",ChapterTitles[chapter],17,42.4f,300,28,20,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            int completed=0;for(int i=chapter*6;i<chapter*6+6;i++)if(profile.IsCompleted(i))completed++;
            Label(card.transform,"Chapter progress","已通关 "+completed+" / 6 · "+ChapterDescriptions[chapter],17,75.4f,300,19.2f,12,Secondary);
            for(int i=0;i<6;i++)
            {
                int index=chapter*6+i; bool done=profile.IsCompleted(index),available=profile.IsLevelUnlocked(index),current=available&&!done;
                var b=Button(content,"Level "+index,"",28+(i%3)*(104.6667f+10),380.64f+(i/3)*90,104.6667f,80,()=>StartLevel(index),current,14);
                b.interactable=available;
                foreach(var text in b.GetComponentsInChildren<Text>())text.text="";
                Color color=current?Color.white:UiFactory.Ink;
                Label(b.transform,"Level number",(i+1).ToString("00"),0,12,104.6667f,35,25,color,TextAnchor.MiddleCenter,FontStyle.Bold);
                bool continuing=Session!=null&&profile.ActiveLevelIndex==index&&!Session.Game.Won&&Session.Game.Moves>0;
                Label(b.transform,"Level state",done?"已通关 ✓":current?(continuing?"继续挑战":"开始挑战"):"未解锁",0,48,104.6667f,18,11,color,TextAnchor.MiddleCenter);
                if(!available) b.GetComponent<CanvasGroup>().alpha=.48f;
            }
            // The former explanatory paragraph is consolidated into the bottom footer.
            float y=570.64f;
            for(int c=0;c<3;c++)
            {
                if(c==chapter)continue;int target=c;bool unlocked=profile.IsChapterUnlocked(c);
                var row=Button(content,"Chapter "+c,"",28,y,334,83.4f,()=>SelectChapter(target),false,14);row.image.color=Color.clear;row.interactable=unlocked;
                Panel(row.transform,"Chapter divider",0,0,334,1,UiFactory.Hex("D9DFD3"),0);
                Color color=unlocked?UiFactory.Ink:Locked;
                Label(row.transform,"Chapter row title",c==0?"入门篇 · 3×3":ChapterTitles[c],0,19,292,24,15,color,TextAnchor.MiddleLeft,FontStyle.Bold);
                Label(row.transform,"Chapter row description",ChapterDescriptions[c],0,44.6f,300,19.2f,12,unlocked?Secondary:Locked);
                if(!unlocked)
                {
                    var rect=UiFactory.Rect(row.transform,"Chapter lock",310*Scale,26*Scale,20*Scale,24*Scale);
                    var symbol=rect.gameObject.AddComponent<UiIconGraphic>();symbol.Kind=UiIconKind.Lock;symbol.color=Locked;symbol.raycastTarget=false;
                }
                y+=83.4f;
            }
        }
        private void Refresh() { RefreshCounts(); RefreshFooter(); }
        private void RefreshCounts()
        {
            if(Session==null||timeText==null)return;
            SetNumber(timeText,FormatTime(Session.ElapsedSeconds));SetNumber(movesText,Session.Game.Moves.ToString());
            var label=movesText.transform.parent.Find("Moves label")?.GetComponent<Text>();
            if(label!=null)label.text=Session.Game.Assisted?"步数 · 练习":"步数";
            if(undoButton!=null)
            { undoButton.interactable=Session.Game.CanUndo;undoButton.GetComponent<CanvasGroup>().alpha=undoButton.interactable?1:.48f; }
        }
        private void RefreshFooter()
        {
            if(footer==null)return;
            string text=hostSaveWarning??warning;
            if(string.IsNullOrEmpty(text))
            {
                if(choosing)text="完成当前关卡，解锁下一关 · 进度自动保存";
                else if(Session==null)text="选择关卡，开始挑战";
                else if(Session.Game.AtActionLimit)text="操作记录已达安全上限，请重玩或换一题";
                else if(Session.Game.Won)text="本局已完成 · 进度自动保存";
                else if(Session.Game.Moves==0)text="首次移动开始计时 · 不限时";
                else text=Session.Game.Assisted?"练习局 · 不限时 · 进度自动保存":"不限时 · 进度自动保存";
            }
            footer.text=text;footer.color=saveFailed||!string.IsNullOrEmpty(hostSaveWarning)?UiFactory.Hex("B34842"):Secondary;
        }
        private static string FormatTime(double seconds)
        { long whole=(long)Math.Max(0,seconds);return (whole/60).ToString("00")+":"+(whole%60).ToString("00"); }
        private void SetNumber(Text label,string value)
        {
            if(label.text==value && label.horizontalOverflow==HorizontalWrapMode.Overflow)return;
            label.text=value;label.horizontalOverflow=HorizontalWrapMode.Overflow;
            label.fontSize=Mathf.RoundToInt(26*Scale);
            int minimum=Mathf.RoundToInt(16*Scale);
            while(label.fontSize>minimum && label.preferredWidth>label.rectTransform.rect.width)label.fontSize--;
        }
        private Text Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft,FontStyle style=FontStyle.Normal)
        {
            var label=UiFactory.Label(parent,text,x*Scale,y*Scale,w*Scale,h*Scale,Mathf.RoundToInt(size*Scale),color,anchor,style);
            label.name=name;label.supportRichText=false;return label;
        }
        private Image Panel(Transform parent,string name,float x,float y,float w,float h,Color color,float radius)
        {
            var panel=UiFactory.Panel(parent,name,x*Scale,y*Scale,w*Scale,h*Scale,color,radius>0);
            if(radius>0)SlidingPuzzleBoardView.Round(panel,radius*Scale);return panel;
        }
        private Button Button(Transform parent,string name,string text,float x,float y,float w,float h,Action action,bool primary=false,float size=13)
        {
            var b=UiFactory.Button(parent,text,x*Scale,y*Scale,w*Scale,h*Scale,action,primary);b.name=name;
            SlidingPuzzleBoardView.Round(b.image,9*Scale);
            var colors=b.colors;colors.disabledColor=Color.white;b.colors=colors;
            b.gameObject.AddComponent<CanvasGroup>();
            var label=b.GetComponentInChildren<Text>();label.fontSize=Mathf.RoundToInt(size*Scale);label.supportRichText=false;
            UiFactory.Stretch(label.rectTransform);
            return b;
        }
    }
}
