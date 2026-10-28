using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.FlappyBird
{
    internal sealed partial class FlappyBirdPage
    {
        private RectTransform layout,surface,information,warningBand;
        private Text scoreText,bestText,bestCaption,warningText,countdownText;
        private Text brandText,titleText,subtitleText;
        private RectTransform navigation;
        private readonly Button[] navigationButtons=new Button[3];
        private Button pauseButton,retryButton;
        private GameObject readyHint,countdownHint;
        private float scale=1;

        private void Build()
        {
            layout=UiFactory.Rect(root,"Flappy layout",0,0,388,784);
            UiFactory.Stretch(FlappyScene.Shape(layout,"Flappy paper",0,0,388,784,0,UiFactory.Paper).rectTransform);
            navigation=UiFactory.Rect(layout,"Flappy navigation",0,0,388,68);
            navigationButtons[0]=NavButton(navigation,"Home",UiIconKind.Home,19.5f,RequestHome);
            navigationButtons[1]=NavButton(navigation,"Book",UiIconKind.Book,280.5f,ShowRules);
            navigationButtons[2]=NavButton(navigation,"Settings",UiIconKind.Settings,328.5f,Settings);
            brandText=Label(navigation,"XANADU / YONDER",67.5f,14,205,40,10,UiFactory.Muted,TextAnchor.MiddleCenter);
            brandText.name="Flappy brand";
            titleText=Label(layout,"Flappy Bird",19.5f,68,260,43,28,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); titleText.name="Flappy title";
            subtitleText=Label(layout,"轻点飞行",285,71,83.5f,39,10,UiFactory.Hex("8A9A8C"),TextAnchor.MiddleRight);
            information=UiFactory.Rect(layout,"Flappy single line score",19.5f,118,349,44);
            UiFactory.Panel(information,"Score divider",0,43,349,1,UiFactory.Hex("DDE3D7"),false);
            Label(information,"本局",0,0,23,36,11,UiFactory.Muted);
            scoreText=Label(information,"0",31,0,26,36,20,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); scoreText.name="Flappy current score";
            bestCaption=Label(information,"最高",65,0,23,36,11,UiFactory.Muted);
            bestText=Label(information,"0",96,0,55,36,20,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold); bestText.name="Flappy best score";
            ConfigureNumber(scoreText); ConfigureNumber(bestText);
            pauseButton=ActionButton(information,"Pause",289,0,60,36,TogglePause,0,true);
            var icon=FlappyScene.Shape(pauseButton.transform,"Pause symbol",19,11,10,14,0,UiFactory.Ink); icon.Kind=FlappyGraphicKind.Pause;
            Label(pauseButton.transform,"暂停",35,0,25,36,11,UiFactory.Ink,TextAnchor.MiddleRight);
            retryButton=ActionButton(information,"Retry save",205,2,78,32,RetrySave,5);
            retryButton.GetComponent<FlappyGraphic>().color=UiFactory.Hex("EFE4CA");
            Label(retryButton.transform,"未保存 · 重试",3,0,72,32,10,UiFactory.Hex("906B27"),TextAnchor.MiddleCenter);
            var sky=FlappyScene.Shape(layout,"Flappy world 360x600",14,174,360,600,18,Color.white);
            sky.Sky=true; sky.raycastTarget=true; surface=sky.rectTransform;
            surface.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            warningBand=UiFactory.Rect(layout,"Flappy fixed warning reserve",19.5f,764,349,18);
            warningText=Label(warningBand,"",0,0,349,18,9,UiFactory.Hex("906B27"),TextAnchor.MiddleCenter);
            warningText.resizeTextForBestFit=true; warningText.resizeTextMinSize=7; warningText.resizeTextMaxSize=9;
        }
        private void BuildSceneHints()
        {
            var hints=UiFactory.Rect(surface,"Flappy ready hints",0,0,360,600); readyHint=hints.gameObject;
            Label(hints,"轻点，飞起来",21.6f,79.2f,316.8f,42,28,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            Label(hints,"穿过每一道空隙",21.6f,128,316.8f,23,12,UiFactory.Hex("688679"),TextAnchor.MiddleCenter);
            var circle=FlappyScene.Shape(hints,"Tap circle",157.2f,372,45.6f,45.6f,23,UiFactory.Hex("B8D2BA"));
            FlappyScene.Shape(circle.transform,"Tap circle inner",1,1,43.6f,43.6f,22,UiFactory.Hex("EAF2E7"));
            var tap=FlappyScene.Shape(circle.transform,"Tap symbol",10.2f,9,25.2f,25.2f,0,UiFactory.Hex("5D8170")); tap.Kind=FlappyGraphicKind.Tap;
            Label(hints,"轻点画面开始",0,425,360,25,12,UiFactory.Hex("5D8170"),TextAnchor.MiddleCenter);
            Label(hints,"一次轻点，一次拍翼",0,511,360,23,11,UiFactory.Hex("6B8C77"),TextAnchor.MiddleCenter);
            var countdown=FlappyScene.Shape(surface,"Flappy countdown",0,0,360,600,0,new Color(.965f,.957f,.925f,.13f));
            countdownHint=countdown.gameObject;
            countdownText=Label(countdown.transform,"3",0,235,360,100,76,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            Label(countdown.transform,"准备继续",0,336,360,28,13,UiFactory.Ink,TextAnchor.MiddleCenter);
        }
        private Button NavButton(Transform navigation,string name,UiIconKind kind,float x,Action action)
        {
            var button=ActionButton(navigation,name,x,14,40,40,action,11);
            var symbol=UiFactory.Rect(button.transform,name+" symbol",9,9,22,22).gameObject.AddComponent<UiIconGraphic>();
            symbol.Kind=kind; symbol.color=UiFactory.Ink; symbol.raycastTarget=false;
            UiFactory.Center(symbol.rectTransform); return button;
        }
        private static Text Label(Transform parent,string value,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft,FontStyle weight=FontStyle.Normal)
        {
            var label=UiFactory.Label(parent,value,x,y,w,h,size,color,align,weight);
            label.supportRichText=false; label.horizontalOverflow=HorizontalWrapMode.Wrap; return label;
        }
        private static void ConfigureNumber(Text text)
        {
            text.resizeTextForBestFit=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow;
        }
        private Button ActionButton(Transform parent,string name,float x,float y,float w,float h,Action action,float radius=10,bool clear=false,bool primary=false)
        {
            var graphic=FlappyScene.Shape(parent,name,x,y,w,h,radius,clear?Color.clear:primary?UiFactory.Accent:UiFactory.Hex("E7EDE4"));
            graphic.raycastTarget=true;
            var button=graphic.gameObject.AddComponent<Button>(); button.targetGraphic=graphic;
            var colors=button.colors; colors.highlightedColor=Color.white; colors.selectedColor=Color.white; colors.pressedColor=new Color(.85f,.91f,.84f); colors.disabledColor=new Color(.75f,.78f,.75f,.55f); button.colors=colors;
            button.onClick.AddListener(()=>{ if(visible&&foreground&&!settingsBlocked&&HostAllows) action?.Invoke(); });
            // A pointer down anywhere in page chrome owns that interaction. It
            // clears a queued wing beat without sending it through the world.
            var consume=button.gameObject.AddComponent<FlappyControlTarget>(); consume.Input=()=>pointer;
            return button;
        }
        private void RefreshScores()
        {
            if(scoreText==null||session==null||data==null) return;
            scoreText.text=session.Game.Score.ToString(CultureInfo.InvariantCulture);
            bestText.text=data.BestScore.ToString(CultureInfo.InvariantCulture);
            scoreText.fontSize=bestText.fontSize=20;
            bool failed=!string.IsNullOrEmpty(SaveError);
            retryButton.gameObject.SetActive(failed);
            float width=information.rect.width;
            float scoreWidth=Mathf.Clamp(scoreText.preferredWidth,12,failed?Mathf.Min(70,Mathf.Max(28,width-279)):96);
            scoreText.rectTransform.sizeDelta=new Vector2(scoreWidth,36);
            float bestX=Mathf.Max(65,31+scoreWidth+20);
            bestCaption.rectTransform.anchoredPosition=new Vector2(bestX,0);
            bestText.rectTransform.anchoredPosition=new Vector2(bestX+31,0);
            float right=failed?width-150:width-66;
            bestText.rectTransform.sizeDelta=new Vector2(Mathf.Max(16,right-bestX-31),36);
            FitNumber(scoreText); FitNumber(bestText);
        }
        private static void FitNumber(Text text)
        {
            // Fixed slots preserve the one-line navigation contract even for the
            // largest legal Int64 record; wrapping would hide the pause action.
            float natural=text.preferredWidth,available=text.rectTransform.rect.width;
            if(natural>available&&natural>0) text.fontSize=Mathf.Max(1,Mathf.FloorToInt(20*available/natural));
        }
        private void RefreshWarning()
        {
            if(warningBand==null) return;
            string message=hostSaveWarning??SaveError??loadWarning;
            warningText.text=message??""; warningBand.gameObject.SetActive(!string.IsNullOrEmpty(message));
            if(resultRetry!=null) resultRetry.SetActive(!string.IsNullOrEmpty(SaveError));
        }
        internal void LayoutNow() { Layout(true); }
        private void Layout(bool force)
        {
            if(layout==null||surface==null) return;
            var size=root.rect.size; var safe=Screen.safeArea;
            if(!force&&size==lastSize&&safe==lastSafeArea) return;
            bool changed=lastSize!=Vector2.zero&&(size!=lastSize||safe!=lastSafeArea);
            lastSize=size; lastSafeArea=safe;
            if(changed) { pointer?.Cancel(); session?.Suspend(); soundEffects?.Stop(); }
            float cssWidth=CssWidth();
            bool narrow=cssWidth<=350;
            scale=Mathf.Max(.01f,size.x/cssWidth);
            float height=Mathf.Max(1,size.y/scale);
            layout.sizeDelta=new Vector2(cssWidth,height); layout.localScale=Vector3.one*scale;
            float inset=narrow?16:19.5f,buttonSize=narrow?36:40,gap=narrow?6:8,inner=cssWidth-inset*2;
            navigation.sizeDelta=new Vector2(cssWidth,68);
            float[] navX={inset,cssWidth-inset-buttonSize*2-gap,cssWidth-inset-buttonSize};
            for(int i=0;i<3;i++) { var rect=(RectTransform)navigationButtons[i].transform; rect.anchoredPosition=new Vector2(navX[i],-(68-buttonSize)/2); rect.sizeDelta=new Vector2(buttonSize,buttonSize); }
            float brandLeft=inset+buttonSize+gap;
            brandText.rectTransform.anchoredPosition=new Vector2(brandLeft,-14); brandText.rectTransform.sizeDelta=new Vector2(navX[1]-gap-brandLeft,40); brandText.fontSize=narrow?9:10;
            titleText.fontSize=narrow?26:28; titleText.rectTransform.anchoredPosition=new Vector2(inset,-68);
            subtitleText.rectTransform.anchoredPosition=new Vector2(cssWidth-inset-83.5f,-71); subtitleText.fontSize=narrow?9:10;
            information.anchoredPosition=new Vector2(inset,narrow?-115.4f:-118); information.sizeDelta=new Vector2(inner,44);
            ((RectTransform)information.Find("Score divider")).sizeDelta=new Vector2(inner,1);
            ((RectTransform)pauseButton.transform).anchoredPosition=new Vector2(inner-60,0);
            ((RectTransform)retryButton.transform).anchoredPosition=new Vector2(inner-144,-2);
            // Short windows reduce the whole world, never its logical viewport.
            float header=narrow?171.4f:174;
            float available=Mathf.Max(1,height-header-20);
            float worldScale=Mathf.Min(inner/360,available/600);
            surface.localScale=Vector3.one*worldScale;
            surface.anchoredPosition=new Vector2((cssWidth-360*worldScale)/2,-header);
            warningBand.anchoredPosition=new Vector2(inset,-(height-19)); warningBand.sizeDelta=new Vector2(inner,18); warningText.rectTransform.sizeDelta=new Vector2(inner,18);
            if(dialogPanel!=null) FitDialog();
            if(changed) Refresh();
        }
        private float CssWidth()
        {
            var canvas=root.GetComponentInParent<Canvas>();
            float width=canvas!=null&&canvas.renderMode==RenderMode.WorldSpace?root.rect.width:Screen.safeArea.width;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var resources=activity.Call<AndroidJavaObject>("getResources"))
                using(var metrics=resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                { float density=metrics.Get<float>("density"); if(density>0) width/=density; }
            }
            catch(Exception) { width=388; }
#endif
            return Mathf.Clamp(width,280,388);
        }
    }

    internal sealed class FlappyControlTarget : MonoBehaviour,UnityEngine.EventSystems.IPointerDownHandler,UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.ICancelHandler
    {
        private readonly System.Collections.Generic.HashSet<int> contacts=new System.Collections.Generic.HashSet<int>();
        internal Func<FlappyPointer> Input;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData data)
        {
            if(data.button!=UnityEngine.EventSystems.PointerEventData.InputButton.Left) return;
            contacts.Add(data.pointerId); Input?.Invoke()?.Block(data.pointerId);
        }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData data)
        { contacts.Remove(data.pointerId); Input?.Invoke()?.Release(data.pointerId); }
        public void OnCancel(UnityEngine.EventSystems.BaseEventData data) { ReleaseAll(); Input?.Invoke()?.Cancel(); }
        private void ReleaseAll() { foreach(int id in contacts) Input?.Invoke()?.Release(id); contacts.Clear(); }
        private void OnDisable() { ReleaseAll(); }
    }
}
