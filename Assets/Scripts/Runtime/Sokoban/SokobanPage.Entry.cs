using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed partial class SokobanPage
    {
        private RectTransform entryViewport,entryContent,entryChapters,entryGrid,miniSurface,progressFill;
        private ScrollRect entryScroll;
        private SokobanBoardView miniBoard;
        private Text continueEyebrow,continueTitle,continueStats,continueAction,completedText,chapterTitle,chapterCount,entryFootnote;
        private string selectedChapter="tutorial",pickerChapter;
        internal string SelectedChapter { get { return selectedChapter; } }
        private const float EntryTop=110.421875f,CardTop=43.59375f,EntryGridTop=387.09375f;

        internal void ShowEntry()
        {
            CancelInput(); CloseDialog(); feedback.Stop(); isEntry=true; session.Leave(); Save();
            SetViewVisibility(); Refresh(); LayoutNow();
        }
        internal void ContinueGame()
        {
            if(hasStarted&&session.Game.Won)
            {
                int next=CatalogIndex(session.Game.Level.LevelId)+1;
                StartLevel(next<catalog.Count?catalog.At(next).LevelId:session.Game.Level.LevelId,true);
            }
            else StartLevel(hasStarted?session.Game.Level.LevelId:catalog.At(0).LevelId);
        }
        internal void SelectChapter(string id)
        {
            if(ChapterLevels(id).Count==0) throw new ArgumentException("Unknown Sokoban chapter.",nameof(id));
            selectedChapter=id; RefreshEntryLevels();
        }
        internal int DisplayLevelNumber(string id)
        {
            var level=catalog.Get(id); int number=0;
            for(int i=0;i<catalog.Count;i++)
            {
                var item=catalog.At(i);
                if((item.Chapter=="tutorial")== (level.Chapter=="tutorial")) number++;
                if(item.LevelId==id) return number;
            }
            throw new ArgumentException("Unknown Sokoban level.",nameof(id));
        }
        internal string DisplayLevelLabel(string id)
        {
            var level=catalog.Get(id); int number=DisplayLevelNumber(id);
            return level.Chapter=="tutorial"?number.ToString("00")+" "+level.Title:"第 "+number+" 关";
        }
        private int CatalogIndex(string id)
        { for(int i=0;i<catalog.Count;i++) if(catalog.At(i).LevelId==id) return i; throw new ArgumentException("Unknown level.",nameof(id)); }
        private List<string> ChapterIds()
        {
            var values=new List<string>();
            for(int i=0;i<catalog.Count;i++) if(!values.Contains(catalog.At(i).Chapter)) values.Add(catalog.At(i).Chapter);
            return values;
        }
        private List<SokobanLevel> ChapterLevels(string chapter)
        {
            var levels=new List<SokobanLevel>();
            for(int i=0;i<catalog.Count;i++) if(catalog.At(i).Chapter==chapter) levels.Add(catalog.At(i));
            return levels;
        }
        private string ChapterLabel(string id)
        {
            if(id=="tutorial") return "入门教学";
            var levels=ChapterLevels(id);
            return "第 "+DisplayLevelNumber(levels[0].LevelId)+"–"+DisplayLevelNumber(levels[levels.Count-1].LevelId)+" 关";
        }
        private bool IsActive(string id) { return hasStarted&&!session.Game.Won&&session.Game.Level.LevelId==id; }
        private void SetViewVisibility()
        {
            if(entryViewport!=null) entryViewport.gameObject.SetActive(isEntry);
            information.gameObject.SetActive(!isEntry); surface.gameObject.SetActive(!isEntry); operations.gameObject.SetActive(!isEntry);
        }
        private void BuildEntry()
        {
            float s=Scale,w=349*s;
            entryViewport=ScrollViewport(root,"Sokoban entry viewport",19.5f*s,EntryTop*s,w,1);
            entryContent=UiFactory.Rect(entryViewport,"Sokoban entry content",0,0,w,1);
            entryScroll=Scroll(entryViewport,entryContent,false);
            Label(entryContent,"把每一只箱子，推回它的位置。",0,0,w,21.59375f*s,12,UiFactory.Muted);
            var card=ActionButton(entryContent,"Continue game",0,CardTop*s,w,183*s,ContinueGame,18*s);
            var shape=card.GetComponent<SokobanGraphic>(); shape.color=UiFactory.Hex("E8EDDF"); shape.Gradient=true; shape.EndColor=UiFactory.Hex("E2E9DC"); shape.BorderWidth=s; shape.BorderColor=UiFactory.Hex("D7DFCF");
            continueEyebrow=Label(card.transform,"",18*s,26.203125f*s,217*s,14*s,10,UiFactory.Hex("6D806B")); continueEyebrow.name="Sokoban continue eyebrow";
            continueTitle=Label(card.transform,"",18*s,50.203125f*s,217*s,26.59375f*s,19,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); continueTitle.name="Sokoban continue title";
            continueStats=Label(card.transform,"",18*s,85.796875f*s,217*s,15*s,11,UiFactory.Muted); continueStats.name="Sokoban continue stats";
            var cta=Shape(card.transform,"Continue action background",18*s,125*s,313*s,44*s,10*s,UiFactory.Accent);
            continueAction=Label(cta.transform,"",15*s,0,258*s,44*s,13,UiFactory.Hex("FFFDF4")); continueAction.name="Sokoban continue action";
            Label(cta.transform,"→",278*s,0,20*s,44*s,20,UiFactory.Hex("FFFDF4"),TextAnchor.MiddleCenter);
            miniSurface=UiFactory.Rect(card.transform,"Sokoban mini board",249*s,22.5f*s,82*s,82*s);
            miniBoard=new SokobanBoardView(miniSurface,true);
            completedText=Label(entryContent,"",0,247.59375f*s,230*s,22*s,11,UiFactory.Muted); completedText.name="Sokoban completed"; completedText.supportRichText=true;
            Label(entryContent,"全部关卡开放",249*s,247.59375f*s,100*s,22*s,10,UiFactory.Hex("82907C"),TextAnchor.MiddleRight);
            Shape(entryContent,"Sokoban progress track",0,275.59375f*s,w,3*s,5*s,UiFactory.Hex("E1E6DA"));
            progressFill=Shape(entryContent,"Sokoban progress fill",0,275.59375f*s,0,3*s,5*s,UiFactory.Hex("7B9D72")).rectTransform;
            entryChapters=ScrollViewport(entryContent,"Sokoban entry chapters",0,296.59375f*s,w,40*s);
            chapterTitle=Label(entryContent,"",s,356.59375f*s,280*s,18.5f*s,13,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); chapterTitle.name="Sokoban chapter title";
            chapterCount=Label(entryContent,"",290*s,356.59375f*s,58*s,18.5f*s,10,UiFactory.Hex("87917F"),TextAnchor.MiddleRight); chapterCount.name="Sokoban chapter count";
            entryGrid=UiFactory.Rect(entryContent,"Sokoban entry level grid",0,EntryGridTop*s,w,1);
            entryFootnote=Label(entryContent,"随时可以撤回，也可以换一关继续探索。",0,0,w,18*s,10,UiFactory.Hex("8B9485")); entryFootnote.name="Sokoban entry footnote";
            LayoutEntry(false);
        }
        private void LayoutEntry(bool hasWarning)
        {
            float s=Scale;
            entryViewport.sizeDelta=new Vector2(349*s,Mathf.Max(1,root.rect.height-EntryTop*s-(hasWarning?26*s:0)));
        }
        private void RefreshEntry()
        {
            if(entryViewport==null) return;
            bool active=hasStarted&&!session.Game.Won,won=hasStarted&&session.Game.Won;
            continueEyebrow.text=active?"继续上次的搬运":won?"这一关已全部归位":"从第一只箱子开始";
            continueTitle.text=hasStarted?DisplayLevelLabel(session.Game.Level.LevelId):"先来一次轻松的搬运";
            Ellipsize(continueTitle);
            continueStats.text=active?session.Game.Moves+" 步 · "+session.Game.Pushes+" 次推动 · "+session.Game.GoalsFilled+"/"+session.Game.Level.BoxCount+" 归位":won?session.Game.Moves+" 步完成 · 已记入个人纪录":"8 关教学 · 不计时，随时撤回";
            continueAction.text=active?"继续游戏":won?(CatalogIndex(session.Game.Level.LevelId)+1<catalog.Count?"挑战下一关":"再玩这一关"):"开始入门教学";
            float s=Scale,mw=82*s,mh=82*s;
            var level=session.Game.Level;
            if(level.Height>level.Width) { mh=91*s; mw=mh*level.Width/level.Height; }
            else mh=mw*level.Height/level.Width;
            miniSurface.sizeDelta=new Vector2(mw,mh); miniSurface.anchoredPosition=new Vector2(331*s-mw,-(18*s+(91*s-mh)/2));
            miniBoard.Render(session.Game,new SokobanViewport(level.Width,level.Height,mw,mh),facing);
            completedText.text="已完成  <color=#173D35><size="+Mathf.RoundToInt(16*s)+"><b>"+CompletedCount+"</b></size></color> / "+catalog.Count+" 关";
            progressFill.sizeDelta=new Vector2(349*s*(float)CompletedCount/catalog.Count,3*s);
            RefreshEntryLevels();
        }
        private void RefreshEntryLevels()
        {
            if(entryGrid==null) return;
            var levels=ChapterLevels(selectedChapter);
            if(levels.Count==0) { selectedChapter=catalog.At(0).Chapter; levels=ChapterLevels(selectedChapter); }
            BuildChapterTabs(entryChapters,selectedChapter,false);
            chapterTitle.text=ChapterLabel(selectedChapter); chapterCount.text=levels.Count+" 关";
            ClearChildren(entryGrid);
            bool tutorial=selectedChapter=="tutorial";
            int columns=tutorial?(CompactWidth?4:5):(CompactWidth?5:6);
            float s=Scale,gap=(tutorial?7:8)*s,h=(tutorial?64:44)*s,w=(349*s-gap*(columns-1))/columns;
            for(int i=0;i<levels.Count;i++)
            {
                var level=levels[i]; string id=level.LevelId;
                var button=LevelButton(entryGrid,"Entry level "+id,level,(i%columns)*(w+gap),(i/columns)*(h+gap),w,h,tutorial);
                if(tutorial)
                {
                    Label(button.transform,DisplayLevelNumber(id).ToString("00"),6*s,8*s,w-12*s,18.4f*s,16,UiFactory.Ink);
                    var name=Label(button.transform,level.Title,6*s,28.703125f*s,w-12*s,14*s,10,UiFactory.Hex("75816E")); Ellipsize(name);
                    var record=profile.GetRecord(id);
                    var state=Label(button.transform,IsActive(id)?"进行中":record!=null?record.BestMoves.Moves+" 步完成":"",6*s,45*s,w-12*s,12*s,9,UiFactory.Hex("52764B")); Ellipsize(state);
                    if(record!=null) Label(button.transform,"✓",w-18*s,7*s,12*s,14*s,10,UiFactory.Hex("638356"),TextAnchor.MiddleRight);
                }
            }
            int rows=(levels.Count+columns-1)/columns; float height=rows*h+Mathf.Max(0,rows-1)*gap;
            entryGrid.sizeDelta=new Vector2(349*s,height); entryFootnote.rectTransform.anchoredPosition=new Vector2(0,-(EntryGridTop*s+height+19*s));
            entryContent.sizeDelta=new Vector2(349*s,EntryGridTop*s+height+51*s);
        }
        private Button LevelButton(Transform parent,string name,SokobanLevel level,float x,float y,float width,float height,bool tutorial)
        {
            string id=level.LevelId; bool active=IsActive(id),complete=profile.IsCompleted(id);
            var button=ActionButton(parent,name,x,y,width,height,()=>RequestLevel(id),(tutorial?10:9)*Scale);
            var shape=button.GetComponent<SokobanGraphic>(); shape.BorderWidth=(active&&!tutorial?2:1)*Scale;
            shape.BorderColor=UiFactory.Hex(tutorial?(active?"90AA82":"E0E5D8"):(active?"78996C":complete?"C6D8BD":"DFE5D7"));
            shape.color=UiFactory.Hex(tutorial?(active?"E8EDDF":complete?"EEF2E8":"F9F8F2"):(active?"EDF2E7":complete?"E1EADB":"F9F8F2"));
            if(!tutorial) Label(button.transform,DisplayLevelNumber(id).ToString("00"),0,0,width,height,14,complete?UiFactory.Hex("436A3B"):UiFactory.Ink,TextAnchor.MiddleCenter,active?FontStyle.Bold:FontStyle.Normal);
            return button;
        }
        private void BuildChapterTabs(RectTransform viewportRect,string selected,bool picker)
        {
            var existing=viewportRect.GetComponent<ScrollRect>(); float oldX=existing!=null&&existing.content!=null?existing.content.anchoredPosition.x:0;
            ClearChildren(viewportRect);
            float s=Scale,h=(picker?34:38)*s,x=0,padding=(picker?10:13)*s;
            var content=UiFactory.Rect(viewportRect,"Chapter tabs",0,0,1,h);
            foreach(string id in ChapterIds())
            {
                string chapter=id; var text=Label(content,ChapterLabel(id),0,0,1,h,picker?10:11,id==selected?UiFactory.Accent:UiFactory.Muted,TextAnchor.MiddleCenter,id==selected?FontStyle.Bold:FontStyle.Normal);
                float width=text.preferredWidth+2*padding+2*s;
                var button=ActionButton(content,(picker?"Picker chapter ":"Entry chapter ")+id,x,0,width,h,()=>{ if(picker) { pickerChapter=chapter; BuildLevelPicker(); } else SelectChapter(chapter); },9*s);
                text.transform.SetParent(button.transform,false); text.rectTransform.sizeDelta=new Vector2(width,h);
                var shape=button.GetComponent<SokobanGraphic>(); shape.color=UiFactory.Hex(id==selected?"DCE7D6":"E9EDE3");
                if(id==selected) { shape.BorderWidth=s; shape.BorderColor=UiFactory.Hex("CADAC4"); }
                x+=width+7*s;
            }
            content.sizeDelta=new Vector2(Mathf.Max(viewportRect.rect.width,x-7*s),h);
            Scroll(viewportRect,content,true); content.anchoredPosition=new Vector2(Mathf.Clamp(oldX,Mathf.Min(0,viewportRect.rect.width-content.rect.width),0),0);
        }
        private bool CompactWidth
        {
            get
            {
                float usable=Screen.safeArea.width;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using(var resources=activity.Call<AndroidJavaObject>("getResources"))
                    using(var metrics=resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                    { float density=metrics.Get<float>("density"); if(density>0) return usable/density<=350; }
                }
                catch(Exception error) { Debug.LogWarning("Sokoban compact layout density unavailable: "+error.Message); }
#endif
                // The host uses a width-only 720-unit design frame. Desktop renders
                // retain the reference CSS width; narrow design roots reflow grids.
                return root.rect.width*388/720<=350;
            }
        }
        private static RectTransform ScrollViewport(Transform parent,string name,float x,float y,float width,float height)
        {
            var rect=UiFactory.Rect(parent,name,x,y,width,height);
            var graphic=rect.gameObject.AddComponent<Image>(); graphic.color=Color.clear; graphic.raycastTarget=true;
            rect.gameObject.AddComponent<RectMask2D>(); return rect;
        }
        private ScrollRect Scroll(RectTransform viewportRect,RectTransform content,bool horizontal)
        {
            var scroll=viewportRect.GetComponent<ScrollRect>()??viewportRect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewportRect; scroll.content=content; scroll.horizontal=horizontal; scroll.vertical=!horizontal;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35*Scale;
            return scroll;
        }
        private static void ClearChildren(Transform parent)
        {
            for(int i=parent.childCount-1;i>=0;i--)
            {
                var child=parent.GetChild(i); child.gameObject.SetActive(false); child.SetParent(null,false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
        private static void Ellipsize(Text text)
        {
            string value=text.text;
            while(text.preferredWidth>text.rectTransform.rect.width&&value.Length>1) { value=value.Substring(0,value.Length-1); text.text=value+"…"; }
        }
    }
}
