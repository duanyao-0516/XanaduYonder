using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed partial class MinesweeperPage
    {
        private GameObject normalChrome, focusChrome, pauseCover;
        private RectTransform toolbar, hintBand, statusBand;
        private Text remaining,focusRemaining,difficulty,timer,focusTimer,hint,compactStatus,pauseText,overviewNote,zoomLabel,overviewLabel;
        private Button revealButton,flagButton,zoomOut,zoomValue,zoomIn,overviewButton,enterFullscreen,exitFullscreen,pauseButton,focusPause,resumeButton;
        private readonly Text[] selectionRecords=new Text[3], selectionStates=new Text[3];
        private readonly Button[] starts=new Button[3], newGames=new Button[3], views=new Button[3], difficulties=new Button[3];
        private void BuildGame()
        {
            var page=UiFactory.Rect(root,"Minesweeper game",0,0,720,1320); UiFactory.Stretch(page); gameRoot=page.gameObject;
            var normal=UiFactory.Rect(page,"Normal chrome",0,0,720,350); normalChrome=normal.gameObject;
            normal.anchorMin=normal.anchorMax=new Vector2(.5f,1); normal.pivot=new Vector2(.5f,1); normal.anchoredPosition=Vector2.zero;
            var nav=UiFactory.Band(normal,"Navigation",0,92,0);
            UiFactory.IconButton(nav,UiIconKind.Back,56,34,58,58,ReturnToSelection);
            UiFactory.IconButton(nav,UiIconKind.Restart,126,34,58,58,ConfirmRestart);
            UiFactory.Label(nav,"XANADU / YONDER",210,34,300,58,19,UiFactory.Muted,TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav,UiIconKind.Book,536,34,58,58,ShowRules);
            UiFactory.IconButton(nav,UiIconKind.Settings,606,34,58,58,Settings);
            // These exact heading tokens and geometry are shared with the existing Sudoku page.
            var heading=UiFactory.Band(normal,"Heading",108,70,0);
            var title=UiFactory.Label(heading,"扫雷",36,0,200,70,49,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); title.name="Minesweeper title";
            UiFactory.Label(heading,"数字之间，步步为营。",240,10,444,58,25,UiFactory.Muted,TextAnchor.MiddleRight);
            var stats=UiFactory.Band(normal,"Progress",194,74,0);
            UiFactory.Label(stats,"剩余标记",36,0,210,30,20,UiFactory.Muted);
            remaining=UiFactory.Label(stats,"",36,30,210,44,30,UiFactory.Ink); remaining.name="Minesweeper remaining";
            difficulty=UiFactory.Label(stats,"",235,0,230,74,22,UiFactory.Accent,TextAnchor.MiddleCenter);
            timer=UiFactory.Label(stats,"00:00",470,10,125,58,26,UiFactory.Ink,TextAnchor.MiddleRight); timer.name="Minesweeper timer";
            Fit(timer,18,26);
            pauseButton=LocalButton(stats,"Pause",MinesweeperIconKind.Pause,null,600,0,84,80,Pause);
            enterFullscreen=LocalButton(normal,"Enter fullscreen",MinesweeperIconKind.Expand,"全屏",550,274,134,66,ToggleFullscreen);
            var focus=UiFactory.Rect(page,"Focus chrome",0,0,720,118); focusChrome=focus.gameObject;
            focus.anchorMin=focus.anchorMax=new Vector2(.5f,1); focus.pivot=new Vector2(.5f,1); focus.anchoredPosition=Vector2.zero;
            exitFullscreen=LocalButton(focus,"Exit fullscreen",MinesweeperIconKind.Collapse,null,36,26,84,84,ToggleFullscreen);
            focusRemaining=UiFactory.Label(focus,"",142,32,210,66,29,UiFactory.Ink);
            focusTimer=UiFactory.Label(focus,"00:00",384,32,194,66,29,UiFactory.Ink,TextAnchor.MiddleRight); Fit(focusTimer,20,29);
            focusPause=LocalButton(focus,"Focus pause",MinesweeperIconKind.Pause,null,600,26,84,84,Pause);
            var background=UiFactory.Panel(page,"Minesweeper viewport",36,350,648,762,UiFactory.Hex("F0F3EC"));
            background.raycastTarget=true; surface=background.rectTransform; surface.gameObject.AddComponent<RectMask2D>();
            board=new MinesweeperBoardView(surface); pointer=surface.gameObject.AddComponent<MinesweeperPointer>();
            overviewNote=UiFactory.Label(surface,"总览 · 点选区域返回局部",10,12,628,38,21,UiFactory.Accent,TextAnchor.MiddleCenter);
            overviewNote.gameObject.SetActive(false);
            var cover=UiFactory.Panel(surface,"Pause cover",0,0,648,762,UiFactory.Paper,false); UiFactory.Stretch(cover.rectTransform);
            cover.raycastTarget=true; pauseCover=cover.gameObject;
            var pauseContent=UiFactory.Rect(cover.transform,"Pause content",0,0,480,220); UiFactory.Center(pauseContent);
            pauseText=UiFactory.Label(pauseContent,"已暂停",0,0,480,70,38,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            UiFactory.Label(pauseContent,"盘面已遮挡，放心休息一下。",0,76,480,46,23,UiFactory.Muted,TextAnchor.MiddleCenter);
            resumeButton=UiFactory.Button(pauseContent,"继续游戏",90,142,300,76,Resume,true); resumeButton.name="Resume";
            toolbar=UiFactory.Rect(page,"Minesweeper toolbar",36,1134,648,82);
            revealButton=LocalButton(toolbar,"Reveal mode",MinesweeperIconKind.Reveal,"翻开",0,0,128,82,()=>SetMode(MinesweeperInputMode.Reveal));
            flagButton=LocalButton(toolbar,"Flag mode",MinesweeperIconKind.Flag,"插旗",128,0,128,82,()=>SetMode(MinesweeperInputMode.Flag));
            zoomOut=LocalButton(toolbar,"Zoom out",MinesweeperIconKind.Minus,null,272,0,80,82,()=>Zoom(-1));
            zoomValue=UiFactory.Button(toolbar,"100%",352,0,80,82,ResetZoom); zoomValue.name="Zoom value";
            zoomLabel=zoomValue.GetComponentInChildren<Text>(); zoomLabel.fontSize=21;
            zoomIn=LocalButton(toolbar,"Zoom in",MinesweeperIconKind.Plus,null,432,0,80,82,()=>Zoom(1));
            overviewButton=LocalButton(toolbar,"Overview",MinesweeperIconKind.Overview,"总览",528,0,120,82,ToggleOverview);
            overviewLabel=overviewButton.GetComponentInChildren<Text>();
            hintBand=UiFactory.Rect(page,"Minesweeper hint",36,1228,648,40);
            hint=UiFactory.Label(hintBand,"",0,0,648,40,20,UiFactory.Muted,TextAnchor.MiddleCenter); Fit(hint,16,20);
            statusBand=UiFactory.Rect(page,"Minesweeper compact status",36,1278,648,34);
            compactStatus=UiFactory.Label(statusBand,"",0,0,648,34,18,UiFactory.Hex("B34842"),TextAnchor.MiddleCenter); Fit(compactStatus,14,18);
            focusChrome.SetActive(false); pauseCover.SetActive(false);
        }
        private void BuildSelection()
        {
            var page=UiFactory.Rect(root,"Minesweeper selection",0,0,720,1320); UiFactory.Stretch(page); selectionRoot=page.gameObject;
            var nav=UiFactory.Band(page,"Selection navigation",0,92,0);
            UiFactory.IconButton(nav,UiIconKind.Home,56,34,58,58,returnLobby);
            UiFactory.Label(nav,"XANADU / YONDER",210,34,300,58,19,UiFactory.Muted,TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav,UiIconKind.Book,536,34,58,58,ShowRules);
            UiFactory.IconButton(nav,UiIconKind.Settings,606,34,58,58,Settings);
            var heading=UiFactory.Band(page,"Selection heading",112,130,.05f);
            UiFactory.Label(heading,"扫雷",36,0,300,80,52,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            UiFactory.Label(heading,"从数字出发，找到安全的每一步。",36,82,648,46,24,UiFactory.Muted);
            for(int d=0;d<3;d++)
            {
                int index=d; var spec=MinesweeperRules.Get(d);
                var band=UiFactory.Band(page,"Difficulty card band "+d,274+d*286,266,.2f+d*.23f);
                var card=UiFactory.Panel(band,"Difficulty card "+d,36,0,648,266,UiFactory.Hex("E7EDE4"));
                difficulties[d]=UiFactory.Button(card.transform,spec.Name,24,14,160,58,()=>{profile.SelectedDifficulty=index;Save();RefreshSelection();});
                difficulties[d].name="Difficulty "+d;
                UiFactory.Label(card.transform,spec.Width+" × "+spec.Height+"  ·  "+spec.MineCount+" 雷",222,16,400,54,24,UiFactory.Ink,TextAnchor.MiddleRight);
                selectionRecords[d]=UiFactory.Label(card.transform,"",24,80,600,42,22,UiFactory.Accent); Fit(selectionRecords[d],17,22);
                selectionStates[d]=UiFactory.Label(card.transform,"",24,123,600,34,21,UiFactory.Muted);
                starts[d]=UiFactory.Button(card.transform,"开始新局",24,178,360,70,()=>{
                    var slot=profile.GetSlot(index); bool ended=slot!=null&&(slot.Board.State==MinesweeperState.Won||slot.Board.State==MinesweeperState.Lost);
                    StartGame(index,slot==null||ended);
                },true); starts[d].name="Start "+d;
                newGames[d]=UiFactory.Button(card.transform,"新开一局",400,178,224,70,()=>RequestNew(index)); newGames[d].name="New "+d;
                views[d]=UiFactory.Button(card.transform,"查看上局",400,178,224,70,()=>StartGame(index,false)); views[d].name="View "+d;
            }
            var footer=UiFactory.Band(page,"Selection footer",1194,58,1);
            UiFactory.Label(footer,"首击安全，不保证全盘无猜 · 三档各自保存",36,0,648,58,21,UiFactory.Muted,TextAnchor.MiddleCenter);
        }
        private void RefreshSelection()
        {
            if(profile==null) return;
            for(int d=0;d<3;d++)
            {
                var record=profile.GetRecord(d); var slot=profile.GetSlot(d);
                bool ended=slot!=null&&(slot.Board.State==MinesweeperState.Won||slot.Board.State==MinesweeperState.Lost);
                selectionRecords[d].text="胜 "+record.Wins+" · 负 "+record.Losses+" · 最佳 "+(record.BestMilliseconds.HasValue?FormatTime(record.BestMilliseconds.Value,true):"—");
                selectionStates[d].text=slot==null?"等待新的挑战":ended?"上局已结束，可查看棋盘":slot.Board.State==MinesweeperState.Ready?"准备局 · 尚未翻开首格":"进行中 · "+FormatTime(slot.ElapsedMilliseconds,false);
                starts[d].GetComponentInChildren<Text>().text=slot==null||ended?"开始新局":"继续";
                float actionWidth=slot==null?600:360;
                starts[d].GetComponent<RectTransform>().sizeDelta=new Vector2(actionWidth,70);
                starts[d].GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(actionWidth-24,70);
                newGames[d].gameObject.SetActive(slot!=null&&!ended); views[d].gameObject.SetActive(ended);
                ModeColor(difficulties[d],profile.SelectedDifficulty==d);
            }
        }
        private void Layout(bool force)
        {
            if(surface==null) return;
            var size=root.rect.size;
            if(!force&&size==lastLayoutSize) return;
            if(!force) CancelInput(); lastLayoutSize=size;
            float height=Mathf.Max(1320,size.y), left=(size.x-648)/2;
            float minimum=(float)MinesweeperControlLayout.TargetSize(DensityDpi(),PixelsPerDesignUnit());
            float controlsHeight=ApplyControlLayout(minimum);
            normalChrome.SetActive(!fullscreen); focusChrome.SetActive(fullscreen);
            float top=fullscreen?Mathf.Max(128,minimum+38):Mathf.Max(350,194+minimum*2+22);
            float toolbarTop=height-controlsHeight-104;
            surface.anchoredPosition=new Vector2(left,-top); surface.sizeDelta=new Vector2(648,Mathf.Max(100,toolbarTop-22-top));
            toolbar.anchoredPosition=new Vector2(36,-toolbarTop);
            hintBand.anchoredPosition=new Vector2(left,-(height-92));
            statusBand.anchoredPosition=new Vector2(left,-(height-42)); statusBand.gameObject.SetActive(fullscreen);
            if(viewport!=null) { viewport.SetLayout(surface.rect.width,surface.rect.height,BaseCellSize()); board.RenderView(viewport); }
        }
        private float ApplyControlLayout(float minimumTarget)
        {
            var controls=MinesweeperControlLayout.Create(Math.Max(648,root.rect.width-72),minimumTarget);
            toolbar.sizeDelta=new Vector2((float)controls.Width,(float)controls.Height);
            SetControlBounds(revealButton,controls.Reveal); SetControlBounds(flagButton,controls.Flag);
            SetControlBounds(zoomOut,controls.ZoomOut); SetControlBounds(zoomValue,controls.ZoomValue);
            SetControlBounds(zoomIn,controls.ZoomIn); SetControlBounds(overviewButton,controls.Overview);
            SetControlBounds(pauseButton,new MinesweeperInputRect(684-minimumTarget,0,minimumTarget,minimumTarget));
            SetControlBounds(focusPause,new MinesweeperInputRect(684-minimumTarget,26,minimumTarget,minimumTarget));
            SetControlBounds(exitFullscreen,new MinesweeperInputRect(36,26,minimumTarget,minimumTarget));
            SetControlBounds(enterFullscreen,new MinesweeperInputRect(550,194+minimumTarget+12,134,minimumTarget),true);
            resumeButton.GetComponent<RectTransform>().sizeDelta=new Vector2(300,minimumTarget);
            resumeButton.GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(276,minimumTarget);
            timer.rectTransform.sizeDelta=new Vector2(Mathf.Max(60,684-minimumTarget-16-470),58);
            focusTimer.rectTransform.sizeDelta=new Vector2(Mathf.Max(100,684-minimumTarget-16-384),66);
            focusRemaining.rectTransform.anchoredPosition=new Vector2(36+minimumTarget+16,-32);
            focusRemaining.rectTransform.sizeDelta=new Vector2(332-minimumTarget,66);
            return (float)controls.Height;
        }
        private static void SetControlBounds(Button button,MinesweeperInputRect bounds,bool horizontal=false)
        {
            var rect=button.GetComponent<RectTransform>();
            float w=(float)bounds.Width,h=(float)bounds.Height;
            rect.anchoredPosition=new Vector2((float)bounds.X,-(float)bounds.Y); rect.sizeDelta=new Vector2(w,h);
            var icon=button.GetComponentInChildren<MinesweeperIconGraphic>(true);
            var label=button.GetComponentInChildren<Text>(true);
            if(icon!=null) icon.rectTransform.anchoredPosition=new Vector2(horizontal?14:(w-34)/2,-(horizontal||!label.gameObject.activeSelf?(h-34)/2:8));
            if(label.gameObject.activeSelf)
            {
                label.rectTransform.anchoredPosition=icon==null?new Vector2(12,0):horizontal?new Vector2(54,0):new Vector2(0,-h*.55f);
                label.rectTransform.sizeDelta=icon==null?new Vector2(w-24,h):horizontal?new Vector2(w-62,h):new Vector2(w,h*.40f);
            }
        }
        private float PixelsPerDesignUnit()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var canvas=root.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var a=RectTransformUtility.WorldToScreenPoint(camera,root.TransformPoint(Vector3.zero));
            var b=RectTransformUtility.WorldToScreenPoint(camera,root.TransformPoint(Vector3.right));
            return Mathf.Max(.01f,(b-a).magnitude);
#else
            return 1;
#endif
        }
        private float cachedDensityDpi;
        private float DensityDpi()
        {
            if(cachedDensityDpi>0) return cachedDensityDpi;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var resources=activity.Call<AndroidJavaObject>("getResources"))
                using(var metrics=resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                    cachedDensityDpi=metrics.Get<float>("density")*160;
            }
            catch(Exception) { cachedDensityDpi=Screen.dpi>0?Screen.dpi:160; }
#else
            cachedDensityDpi=160;
#endif
            return cachedDensityDpi>0?cachedDensityDpi:160;
        }
        private float BaseCellSize() { return Mathf.Max(72,32*(DensityDpi()/160)/PixelsPerDesignUnit()); }
        private static Button LocalButton(Transform parent,string name,MinesweeperIconKind kind,string text,float x,float y,float w,float h,Action action)
        {
            var button=UiFactory.Button(parent,text??"",x,y,w,h,action); button.name=name;
            var label=button.GetComponentInChildren<Text>();
            if(text==null) label.gameObject.SetActive(false);
            else if(h<80)
            { label.fontSize=21; label.rectTransform.anchoredPosition=new Vector2(54,0); label.rectTransform.sizeDelta=new Vector2(w-62,h); }
            else { label.fontSize=21; label.rectTransform.anchoredPosition=new Vector2(0,-h*.55f); label.rectTransform.sizeDelta=new Vector2(w,h*.40f); }
            var rect=UiFactory.Rect(button.transform,name+" icon",text!=null&&h<80?14:(w-34)/2,text==null||h<80?(h-34)/2:8,34,34);
            var icon=rect.gameObject.AddComponent<MinesweeperIconGraphic>(); icon.Kind=kind; icon.color=UiFactory.Ink; icon.raycastTarget=false;
            return button;
        }
        private static void ModeColor(Button button,bool selected)
        {
            button.GetComponent<Image>().color=selected?UiFactory.Accent:UiFactory.Hex("E7EDE4");
            foreach(var label in button.GetComponentsInChildren<Text>(true)) label.color=selected?Color.white:UiFactory.Ink;
            foreach(var icon in button.GetComponentsInChildren<MinesweeperIconGraphic>(true)) icon.color=selected?Color.white:UiFactory.Ink;
        }
        private static void ViewButtonState(Button button,bool available,Color activeColor)
        {
            button.interactable=available;
            var color=available?activeColor:UiFactory.Muted;
            foreach(var label in button.GetComponentsInChildren<Text>(true)) label.color=color;
            foreach(var icon in button.GetComponentsInChildren<MinesweeperIconGraphic>(true)) icon.color=color;
        }
        private static void Fit(Text text,int minimum,int maximum)
        { text.resizeTextForBestFit=true;text.resizeTextMinSize=minimum;text.resizeTextMaxSize=maximum; }
    }
}
