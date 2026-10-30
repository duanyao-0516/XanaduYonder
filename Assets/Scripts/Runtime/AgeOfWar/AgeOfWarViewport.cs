using UnityEngine;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarViewport : MonoBehaviour
    {
        private RectTransform stage;
        private int stageWidth,stageHeight;
        private Rect safe;
        private Vector2 screen;
        public AgeOfWarViewportRect Bounds {get;private set;}
        public event System.Action Changed;
        public void Initialize(RectTransform value,int width,int height)
        {stage=value;stageWidth=width;stageHeight=height;Refresh();}
        private void Update(){if(safe!=Screen.safeArea||screen!=new Vector2(Screen.width,Screen.height))Refresh();}
        public void Refresh()
        {
            if(stage==null)return;safe=Screen.safeArea;screen=new Vector2(Screen.width,Screen.height);
            Bounds=AgeOfWarViewportMath.FillWidth(safe.x,safe.y,safe.width,safe.height,stageWidth,stageHeight);
            stage.anchorMin=stage.anchorMax=Vector2.zero;stage.pivot=new Vector2(0,1);
            stage.sizeDelta=new Vector2((float)Bounds.StageWidth,stageHeight);
            stage.anchoredPosition=new Vector2((float)Bounds.Left,(float)(Bounds.Bottom+Bounds.Height));
            stage.localScale=Vector3.one*(float)(Bounds.Height/stageHeight);Changed?.Invoke();
        }
    }
}
