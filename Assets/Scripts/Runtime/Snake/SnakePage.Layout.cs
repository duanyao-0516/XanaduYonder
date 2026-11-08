using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    internal sealed partial class SnakePage
    {
        private void BuildLayout()
        {
            var surface=UiFactory.Rect(root,"Snake game",0,0,720,1320);UiFactory.Stretch(surface);
            gameInput=surface.gameObject.AddComponent<CanvasGroup>();
            var nav=UiFactory.Band(surface,"Snake navigation",0,92,0);
            UiFactory.IconButton(nav,UiIconKind.Home,56,34,58,58,()=>{if(Available){Suspend();returnLobby();}});
            UiFactory.Label(nav,"XANADU / YONDER",210,34,300,58,19,UiFactory.Muted,TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav,UiIconKind.Book,536,34,58,58,ShowRules);
            UiFactory.IconButton(nav,UiIconKind.Settings,606,34,58,58,ShowSettings);
            var heading=UiFactory.Band(surface,"Snake heading",108,64,.03f);
            UiFactory.Label(heading,"贪吃蛇",36,0,290,64,52,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            modeButton=UiFactory.Button(heading,"经典无限",474,5,210,54,ChooseMode);modeButton.name="Mode";
            modeText=modeButton.GetComponentInChildren<Text>();modeText.color=UiFactory.Ink;modeText.fontSize=23;
            modeText.rectTransform.sizeDelta=new Vector2(158,54);
            SnakeIconGraphic.Add(modeButton.transform,SnakeIconKind.Down,174,15,24,UiFactory.Ink);
            var progress=UiFactory.Band(surface,"Snake progress",178,58,.10f);
            levelDropdown=SnakeDropdown.Create(progress,36,0,164,54);
            levelDropdown.CanOpen=()=>Available&&dialog==null;
            levelDropdown.Opening=()=>{returnFromDropdown=CaptureOverlayReturn();input.Cancel();session.Pause();board.FinishAnimation();countdown.SetActive(false);Save();};
            levelDropdown.Closed=ReturnFromDropdown;
            levelDropdown.onValueChanged.AddListener(ChooseLevel);
            scoreText=UiFactory.Label(progress,"",306,0,194,54,25,UiFactory.Muted,TextAnchor.MiddleRight);
            progressText=UiFactory.Label(progress,"",510,0,174,54,25,UiFactory.Muted,TextAnchor.MiddleRight);
            UiFactory.Panel(progress,"Divider",36,56,648,1.5f,UiFactory.Hex("D7DED2"),false);
            boardBand=UiFactory.Band(surface,"Snake board band",268,778,.45f);
            var boardFrame=UiFactory.Rect(boardBand,"Snake board frame",36,0,648,777.6f);
            board=CreateBoard(boardFrame,"Snake board",0,0,648,777.6f);board.raycastTarget=true;
            var boardRect=board.rectTransform;
            input=boardRect.gameObject.AddComponent<SnakeInput>();input.CanInput=()=>Interactive;
            input.Turn=RequestDirection;input.Canceled=()=>session?.ClearInput();
            exits=new GameObject[4];
            var edgeAnchors=new[]{new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(.5f,1),new Vector2(.5f,0)};
            for(int i=0;i<4;i++)
            {
                // Anchor to the board itself; rotate around the icon center, never its top-left corner.
                var exit=UiFactory.Panel(boardFrame,"Wrap exit "+i,0,0,32,32,UiFactory.Paper);
                exit.rectTransform.anchorMin=exit.rectTransform.anchorMax=edgeAnchors[i];
                exit.rectTransform.pivot=new Vector2(.5f,.5f);exit.rectTransform.anchoredPosition=Vector2.zero;
                var icon=SnakeIconGraphic.Add(exit.transform,SnakeIconKind.Wrap,0,0,24,UiFactory.Accent);
                UiFactory.Center(icon.rectTransform);
                if(i>=2)icon.rectTransform.localRotation=Quaternion.Euler(0,0,90);
                exits[i]=exit.gameObject;
            }
            controls=UiFactory.Band(surface,"Snake controls",1032,256,.80f);
            var tools=UiFactory.Rect(controls,"Snake left tools",36,48,320,208);
            pauseButton=UiFactory.Button(tools,"暂停",0,0,152,96,()=>{if(Available)ShowPause();});pauseButton.name="Pause";
            AddToolIcon(pauseButton,SnakeIconKind.Pause);
            var restart=UiFactory.Button(tools,"重来",168,0,152,96,ConfirmRestart);restart.name="Restart";
            var restartIcon=UiFactory.Rect(restart.transform,"Restart icon",20,30,36,36).gameObject.AddComponent<UiIconGraphic>();restartIcon.Kind=UiIconKind.Restart;restartIcon.color=UiFactory.Ink;restartIcon.raycastTarget=false;
            PositionToolText(restart);
            boundaryButton=UiFactory.Button(tools,"四边相通",0,112,320,96,ConfirmBoundary);boundaryButton.name="Boundary";
            var boundaryText=boundaryButton.GetComponentInChildren<Text>();boundaryText.alignment=TextAnchor.MiddleLeft;
            boundaryText.rectTransform.anchoredPosition=new Vector2(56,0);boundaryText.rectTransform.sizeDelta=new Vector2(160,96);boundaryText.fontSize=24;
            SnakeIconGraphic.Add(boundaryButton.transform,SnakeIconKind.Wrap,18,32,30,UiFactory.Accent);
            boundaryLock=SnakeIconGraphic.Add(boundaryButton.transform,SnakeIconKind.Lock,220,34,26,UiFactory.Muted);
            boundaryTrack=UiFactory.Panel(boundaryButton.transform,"Boundary switch",252,32,52,32,UiFactory.Accent);
            boundaryThumb=UiFactory.CirclePanel(boundaryTrack.transform,"Switch thumb",26,4,24,UiFactory.Paper).rectTransform;
            var pad=UiFactory.Rect(controls,"Snake direction pad",380,0,304,256);
            AddDirection(pad,"Up",SnakeDirection.Up,104,48,SnakeIconKind.Up);
            AddDirection(pad,"Left",SnakeDirection.Left,0,104,SnakeIconKind.Left);
            AddDirection(pad,"Right",SnakeDirection.Right,208,104,SnakeIconKind.Right);
            AddDirection(pad,"Down",SnakeDirection.Down,104,160,SnakeIconKind.Down);
            var countSurface=UiFactory.Panel(root,"Snake countdown",0,0,720,1320,new Color(.09f,.24f,.2f,.24f),false);
            UiFactory.Stretch(countSurface.rectTransform);countSurface.raycastTarget=true;countdown=countSurface.gameObject;
            var circle=UiFactory.CirclePanel(countSurface.transform,"Countdown disc",0,0,200,UiFactory.Paper);UiFactory.Center(circle.rectTransform);
            countdownText=UiFactory.Label(circle.transform,"3",0,0,200,200,92,UiFactory.Accent,TextAnchor.MiddleCenter,FontStyle.Bold);countdown.SetActive(false);
            var banner=UiFactory.Panel(root,"Snake save warning",0,0,720,30,UiFactory.Hex("F1DED3"),false);
            banner.rectTransform.anchorMin=banner.rectTransform.anchorMax=new Vector2(.5f,1);banner.rectTransform.pivot=new Vector2(.5f,1);
            warningText=UiFactory.Label(banner.transform,"",14,0,692,30,18,UiFactory.Hex("8F4235"),TextAnchor.MiddleCenter);
            warningBanner=banner.gameObject;warningBanner.SetActive(false);
            Layout();
        }
        private static SnakeBoardView CreateBoard(Transform parent,string name,float x,float y,float width,float height)
        {
            var clip=UiFactory.Panel(parent,name+" clip",x,y,width,height,Color.white);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var rect=UiFactory.Rect(clip.transform,name,0,0,width,height);UiFactory.Stretch(rect);
            return rect.gameObject.AddComponent<SnakeBoardView>();
        }
        private void AddToolIcon(Button button,SnakeIconKind kind)
        { SnakeIconGraphic.Add(button.transform,kind,20,30,36,UiFactory.Ink);PositionToolText(button); }
        private static void PositionToolText(Button button)
        { var text=button.GetComponentInChildren<Text>();text.rectTransform.anchoredPosition=new Vector2(62,0);text.rectTransform.sizeDelta=new Vector2(78,96);text.fontSize=25; }
        private void AddDirection(Transform parent,string name,SnakeDirection direction,float x,float y,SnakeIconKind icon)
        {
            var button=UiFactory.Button(parent,"",x,y,96,96,()=>{});button.name=name;
            SnakeIconGraphic.Add(button.transform,icon,24,24,48,UiFactory.Accent);
            var touch=button.gameObject.AddComponent<SnakeDirectionButton>();touch.InputTarget=input;touch.Direction=direction;
        }
        private void Layout()
        {
            lastSize=root.rect.size;
            // SafeDesignFrame uniformly scales short screens; these bands share only the added height.
            boardBand.anchoredPosition=new Vector2(0,1320*.45f-268);
            controls.anchoredPosition=new Vector2(0,1320*.80f-1032);
        }
    }
}
