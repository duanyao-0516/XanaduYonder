using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed partial class SokobanPage
    {
        private RectTransform navigation,information,operations,statusBand,toastPanel;
        private Text title,levelText,movesText,pushesText,goalsText,zoomText,compactStatus,toastText;
        private Button levelButton,undoButton,restartButton,zoomOut,zoomIn,fitButton;
        private readonly RectTransform[] directionRects=new RectTransform[4];
        private readonly Direction[] directionValues={Direction.Up,Direction.Left,Direction.Right,Direction.Down};
        private readonly Button[] directionButtons=new Button[4];
        private int pointerMoveFrame=-1;
        private readonly List<Button> hitButtons=new List<Button>();
        private Rect lastSafeArea;
        private float Scale { get { return Mathf.Max(1,root.rect.width)/388; } }
        private void BuildGame()
        {
            float s=Scale,w=root.rect.width;
            navigation=UiFactory.Rect(root,"Sokoban navigation",0,0,w,63*s);
            NavButton("Home",UiIconKind.Home,(388*.07778f-6)*s,Home);
            NavButton("Book",UiIconKind.Book,(388*.95f-92)*s,ShowRules);
            NavButton("Settings",UiIconKind.Settings,(388*.95f-44)*s,Settings);
            var brand=Label(navigation,"XANADU / YONDER",(388-180)/2*s,12*s,180*s,32*s,10.3f,UiFactory.Muted,TextAnchor.MiddleCenter);
            brand.name="Sokoban brand";
            title=Label(root,"推箱子",19.5f*s,63*s,349*s,38.421875f*s,26.5f,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); title.fontSize=49; title.name="Sokoban title";
            var weight=title.gameObject.AddComponent<Outline>(); weight.effectColor=UiFactory.Ink; weight.effectDistance=new Vector2(.25f*s,.25f*s); weight.useGraphicAlpha=true;
            information=UiFactory.Rect(root,"Sokoban information",19.5f*s,110.421875f*s,349*s,45*s);
            var line=UiFactory.Panel(information,"Information divider",0,44*s,349*s,s,UiFactory.Hex("DCE2D6"),false);
            levelButton=ActionButton(information,"Levels",0,0,160*s,44*s,ShowLevels,0,true);
            levelText=Label(levelButton.transform,"",0,0,150*s,44*s,12,UiFactory.Ink); levelText.name="Sokoban level";
            var chevron=UiFactory.Rect(levelButton.transform,"Level chevron",154*s,17*s,10*s,10*s).gameObject.AddComponent<SokobanGraphic>();
            chevron.Kind=SokobanGraphicKind.Chevron; chevron.color=UiFactory.Ink; chevron.raycastTarget=false;
            movesText=Stat("Sokoban moves"); pushesText=Stat("Sokoban pushes"); goalsText=Stat("Sokoban goals");
            var background=UiFactory.Panel(root,"Sokoban board viewport",19.5f*s,163.421875f*s,349*s,452.578125f*s,Color.clear,false);
            surface=background.rectTransform; background.raycastTarget=true; surface.gameObject.AddComponent<RectMask2D>();
            board=new SokobanBoardView(surface); pointer=surface.gameObject.AddComponent<SokobanPointer>();
            toastPanel=Shape(surface,"Sokoban toast",0,0,320*s,42*s,12*s,UiFactory.Ink).rectTransform;
            toastText=Label(toastPanel,"",10*s,0,300*s,42*s,12,UiFactory.Paper,TextAnchor.MiddleCenter); toastPanel.gameObject.SetActive(false);
            operations=UiFactory.Rect(root,"Sokoban operations",19.5f*s,632*s,349*s,152*s);
            undoButton=ActionButton(operations,"Undo",0,24*s,79*s,48*s,Undo,12*s);
            ActionContents(undoButton,UiIconKind.Undo,"撤回",79*s);
            restartButton=ActionButton(operations,"Restart",87*s,24*s,79*s,48*s,ConfirmRestart,12*s);
            ActionContents(restartButton,UiIconKind.Restart,"重来",79*s);
            var zoom=Shape(operations,"Sokoban zoom controls",0,84*s,166*s,44*s,12*s,UiFactory.Hex("E7EDE4")).rectTransform;
            zoomOut=ActionButton(zoom,"Zoom out",0,0,44*s,44*s,()=>Zoom(-1),0,true);
            Label(zoomOut.transform,"−",0,0,44*s,44*s,23,UiFactory.Ink,TextAnchor.MiddleCenter);
            fitButton=ActionButton(zoom,"Fit",46*s,0,74*s,44*s,ResetZoom,0,true);
            zoomText=Label(fitButton.transform,"适配",0,0,74*s,44*s,12,UiFactory.Ink,TextAnchor.MiddleCenter);
            zoomIn=ActionButton(zoom,"Zoom in",122*s,0,44*s,44*s,()=>Zoom(1),0,true);
            Label(zoomIn.transform,"+",0,0,44*s,44*s,23,UiFactory.Ink,TextAnchor.MiddleCenter);
            float padX=197*s;
            Vector2[] positions={new Vector2(52,0),new Vector2(0,52),new Vector2(104,52),new Vector2(52,104)};
            for(int i=0;i<4;i++)
            {
                int index=i; var button=ActionButton(operations,"Move "+directionValues[i],padX+positions[i].x*s,positions[i].y*s,48*s,48*s,()=>{
                    if(Input.touchCount==0&&!pointer.HasOwner&&pointerMoveFrame!=Time.frameCount) Move(directionValues[index]);
                },13*s);
                directionButtons[i]=button; directionRects[i]=(RectTransform)button.transform.Find("Sokoban hit area");
                var arrow=UiFactory.Rect(button.transform,"Direction arrow",13*s,13*s,22*s,22*s).gameObject.AddComponent<SokobanGraphic>();
                arrow.Kind=SokobanGraphicKind.Arrow; arrow.Direction=directionValues[i]; arrow.color=UiFactory.Accent; arrow.raycastTarget=false;
                button.gameObject.AddComponent<SokobanDirectionTarget>().Pointer=pointer;
            }
            Shape(operations,"Direction center",padX+73.5f*s,73.5f*s,5*s,5*s,2.5f*s,UiFactory.Hex("9AAF8F"));
            statusBand=UiFactory.Rect(root,"Sokoban compact status",19.5f*s,0,349*s,26*s);
            compactStatus=Label(statusBand,"",0,0,349*s,26*s,9.5f,UiFactory.Hex("B34842"),TextAnchor.MiddleCenter);
            compactStatus.resizeTextForBestFit=true; compactStatus.resizeTextMinSize=14; compactStatus.resizeTextMaxSize=18;
            LayoutNow();
        }
        private Text Stat(string name)
        {
            var value=Label(information,"",0,0,80*Scale,44*Scale,10,UiFactory.Muted,TextAnchor.MiddleRight); value.name=name;
            value.supportRichText=true; value.horizontalOverflow=HorizontalWrapMode.Overflow; return value;
        }
        private void NavButton(string name,UiIconKind icon,float x,Action action)
        {
            float s=Scale;
            var button=ActionButton(navigation,name,x,6*s,44*s,44*s,action,0,true);
            Shape(button.transform,name+" background",6*s,6*s,32*s,32*s,10*s,UiFactory.Hex("E7EDE4"));
            var rect=UiFactory.Rect(button.transform,name+" icon",12*s,12*s,20*s,20*s);
            var symbol=rect.gameObject.AddComponent<UiIconGraphic>(); symbol.Kind=icon; symbol.color=UiFactory.Ink; symbol.raycastTarget=false;
        }
        private void ActionContents(Button button,UiIconKind icon,string text,float width)
        {
            float s=Scale,content=50*s,x=(width-content)/2;
            var symbol=UiFactory.Rect(button.transform,"Action icon",x,13*s,22*s,22*s).gameObject.AddComponent<UiIconGraphic>(); symbol.Kind=icon; symbol.color=UiFactory.Ink; symbol.raycastTarget=false;
            Label(button.transform,text,x+28*s,0,22*s,48*s,11,UiFactory.Ink,TextAnchor.MiddleCenter);
        }
        private static SokobanGraphic Shape(Transform parent,string name,float x,float y,float width,float height,float radius,Color color)
        {
            var shape=UiFactory.Rect(parent,name,x,y,width,height).gameObject.AddComponent<SokobanGraphic>();
            shape.Kind=SokobanGraphicKind.Round; shape.Radius=radius; shape.color=color; shape.raycastTarget=false; return shape;
        }
        private Button ActionButton(Transform parent,string name,float x,float y,float width,float height,Action action,float radius,bool clear=false)
        {
            var shape=Shape(parent,name,x,y,width,height,radius,clear?Color.clear:UiFactory.Hex("E7EDE4")); shape.raycastTarget=false;
            var button=shape.gameObject.AddComponent<Button>(); button.targetGraphic=shape;
            var colors=button.colors; colors.highlightedColor=Color.white; colors.selectedColor=Color.white; colors.pressedColor=new Color(.90f,.94f,.89f); colors.disabledColor=Color.white; button.colors=colors;
            button.gameObject.AddComponent<CanvasGroup>();
            var hit=SokobanLayout.HitBounds(name,width,height,Scale,MinimumTouchSize());
            var target=UiFactory.Panel(button.transform,"Sokoban hit area",(float)hit.X,(float)hit.Y,(float)hit.Width,(float)hit.Height,Color.clear,false); target.raycastTarget=true;
            var filter=target.gameObject.AddComponent<SokobanHitArea>(); filter.Original=(RectTransform)button.transform; filter.Round=name.StartsWith("Move ",StringComparison.Ordinal);
            hitButtons.Add(button);
            button.onClick.AddListener(()=>action()); return button;
        }
        private float? minimumTouchSize;
        private float MinimumTouchSize()
        {
            if(minimumTouchSize.HasValue) return minimumTouchSize.Value;
            float widthDp=360;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var resources=activity.Call<AndroidJavaObject>("getResources"))
                using(var metrics=resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                { float density=metrics.Get<float>("density"); if(density>0) widthDp=Screen.safeArea.width/density; }
            }
            catch(Exception error) { Debug.LogWarning("Sokoban density unavailable: "+error.Message); }
#endif
            minimumTouchSize=(float)SokobanLayout.MinimumTarget(root.rect.width,widthDp); return minimumTouchSize.Value;
        }
        private void RefreshHitAreas()
        {
            float minimum=MinimumTouchSize();
            foreach(var button in hitButtons)
            {
                var rect=(RectTransform)button.transform; var target=(RectTransform)rect.Find("Sokoban hit area");
                var hit=SokobanLayout.HitBounds(button.name,rect.rect.width,rect.rect.height,Scale,minimum);
                target.anchoredPosition=new Vector2((float)hit.X,-(float)hit.Y); target.sizeDelta=new Vector2((float)hit.Width,(float)hit.Height);
            }
        }
        private Text Label(Transform parent,string text,float x,float y,float width,float height,float cssSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft,FontStyle style=FontStyle.Normal)
        {
            var value=UiFactory.Label(parent,text,x,y,width,height,Mathf.RoundToInt(cssSize*Scale),color,alignment,style);
            value.horizontalOverflow=HorizontalWrapMode.Overflow; value.verticalOverflow=VerticalWrapMode.Overflow; return value;
        }
        private void Layout(bool force)
        {
            if(surface==null||operations==null) return;
            var size=root.rect.size; bool hasWarning=!string.IsNullOrEmpty(hostSaveWarning)||!string.IsNullOrEmpty(warning);
            Rect safeArea=Screen.safeArea;
            if(!force&&size==lastSize&&hasWarning==lastWarning&&safeArea==lastSafeArea) return;
            if(size!=lastSize||safeArea!=lastSafeArea) minimumTouchSize=null;
            lastSize=size; lastWarning=hasWarning; lastSafeArea=safeArea; CancelInput(); RefreshHitAreas();
            var layout=new SokobanLayout(Math.Max(1,size.x),Math.Max(1,size.y),hasWarning);
            surface.anchoredPosition=new Vector2((float)layout.Inset,-(float)layout.BoardTop);
            surface.sizeDelta=new Vector2((float)layout.BoardWidth,(float)layout.BoardHeight);
            operations.anchoredPosition=new Vector2((float)layout.Inset,-(float)layout.ControlsTop);
            statusBand.anchoredPosition=new Vector2((float)layout.Inset,-(float)layout.StatusTop); statusBand.gameObject.SetActive(hasWarning);
            if(viewport!=null) { viewport.Resize(layout.BoardWidth,layout.BoardHeight); RenderView(); }
        }
        private void Refresh()
        {
            if(session==null) return; RefreshCounts(); RenderView(); RefreshCompactStatus();
        }
        private void RefreshCounts()
        {
            if(session==null||movesText==null) return;
            float s=Scale;
            movesText.text=StatText("步数",session.Game.Moves.ToString()); pushesText.text=StatText("推动",session.Game.Pushes.ToString()); goalsText.text=StatText("归位",session.Game.GoalsFilled+"/"+session.Game.Level.BoxCount);
            float right=349*s;
            foreach(var stat in new[]{goalsText,pushesText,movesText})
            {
                float width=stat.preferredWidth; stat.rectTransform.sizeDelta=new Vector2(width,44*s); right-=width;
                stat.rectTransform.anchoredPosition=new Vector2(right,0); right-=12*s;
            }
            float levelWidth=Mathf.Max(36*s,right); ((RectTransform)levelButton.transform).sizeDelta=new Vector2(levelWidth,44*s);
            levelText.rectTransform.sizeDelta=new Vector2(Mathf.Max(10*s,levelWidth-16*s),44*s);
            string full=LevelNumber(session.Game.Level.LevelId).ToString("00")+" "+session.Game.Level.Title;
            levelText.text=full;
            while(levelText.preferredWidth>levelText.rectTransform.rect.width&&full.Length>1) { full=full.Substring(0,full.Length-1); levelText.text=full+"…"; }
            var chevron=(RectTransform)levelButton.transform.Find("Level chevron"); chevron.anchoredPosition=new Vector2(Mathf.Min(levelText.preferredWidth+4*s,levelWidth-10*s),-17*s);
            Enable(undoButton,session.Game.CanUndo&&!session.Game.Won);
            foreach(var button in directionButtons) Enable(button,!session.Game.Won);
            RefreshHitAreas();
        }
        private string StatText(string caption,string value)
        { return caption+" <color=#173D35><size="+Mathf.RoundToInt(14*Scale)+">"+value+"</size></color>"; }
        private int LevelNumber(string id) { for(int i=0;i<catalog.Count;i++) if(catalog.At(i).LevelId==id) return i+1; return 1; }
        private void RenderView()
        {
            if(viewport==null||zoomText==null) return; board.Layout();
            Enable(zoomOut,viewport.Zoom>1.00001); Enable(zoomIn,viewport.Zoom<1.99999);
            zoomText.text=viewport.Zoom<=1.00001?"适配":Math.Round(viewport.Zoom*100)+"%";
        }
        private void RefreshCompactStatus()
        {
            if(compactStatus==null) return;
            compactStatus.text=string.IsNullOrEmpty(hostSaveWarning)?warning??"":string.IsNullOrEmpty(warning)?hostSaveWarning:hostSaveWarning+"  "+warning;
            Layout(false);
        }
        private void Toast(string text)
        {
            if(toastPanel==null) return; float s=Scale; toastText.text=text;
            float width=Mathf.Min(surface.rect.width-20*s,toastText.preferredWidth+28*s);
            toastPanel.sizeDelta=new Vector2(width,42*s); toastPanel.anchoredPosition=new Vector2((surface.rect.width-width)/2,-(surface.rect.height-58*s));
            toastText.rectTransform.sizeDelta=new Vector2(width-20*s,42*s); toastPanel.SetAsLastSibling(); toastPanel.gameObject.SetActive(true); toastRemaining=2.2f;
        }
        private void ClearToast() { toastRemaining=0; if(toastPanel!=null) toastPanel.gameObject.SetActive(false); }
        private static void Enable(Button button,bool enabled)
        { button.interactable=enabled; var group=button.GetComponent<CanvasGroup>(); if(group!=null) group.alpha=enabled?1:.35f; }
    }
}
