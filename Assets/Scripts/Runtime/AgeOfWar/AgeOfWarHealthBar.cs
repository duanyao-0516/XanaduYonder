using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar;
namespace XanaduYonder.Runtime.AgeOfWar
{
    // Unit.as creates these rectangles procedurally, independently of the scaled animation child.
    internal sealed class AgeOfWarHealthBar
    {
        private readonly RectTransform root,green;
        private readonly CanvasGroup alpha;
        internal AgeOfWarHealthBar(Transform parent)
        {
            root=AgeOfWarDrawing.Rect(parent,"Original unit health");alpha=root.gameObject.AddComponent<CanvasGroup>();
            Box(root,-15,-2,30,4,new Color32(187,0,0,255));green=Box(root,-15,-2,30,4,Color.green);
            Box(root,-15.5f,-2.5f,31,1,Color.black);Box(root,-15.5f,1.5f,31,1,Color.black);
            Box(root,-15.5f,-1.5f,1,3,Color.black);Box(root,14.5f,-1.5f,1,3,Color.black);
        }
        private static RectTransform Box(Transform parent,float x,float y,float w,float h,Color color)
        {var rect=AgeOfWarDrawing.Rect(parent,"Health rectangle",x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return rect;}
        internal void Render(AgeOfWarVisual visual)
        {root.anchoredPosition=new Vector2((float)visual.X,-(float)(visual.Y+visual.HealthY));alpha.alpha=Mathf.Clamp01((float)visual.HealthAlpha);green.localScale=new Vector3((float)visual.HealthFraction,1,1);root.SetAsLastSibling();}
        internal void Dispose(){Object.Destroy(root.gameObject);}
    }
}
