using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime.Sokoban
{
    internal enum SokobanGraphicKind { Round, Floor, Goal, Check, Arrow, Chevron }
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SokobanGraphic : MaskableGraphic
    {
        internal SokobanGraphicKind Kind;
        internal float Radius=16;
        internal float BorderWidth;
        internal Color BorderColor;
        internal bool Gradient;
        internal bool GoalRing=true;
        internal Color EndColor;
        internal Direction Direction;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r=GetPixelAdjustedRect();
            if(Kind==SokobanGraphicKind.Round)
            {
                if(BorderWidth>0)
                {
                    Rounded(mesh,r,Radius,BorderColor);
                    r=new Rect(r.x+BorderWidth,r.y+BorderWidth,Mathf.Max(0,r.width-2*BorderWidth),Mathf.Max(0,r.height-2*BorderWidth));
                }
                Rounded(mesh,r,Mathf.Max(0,Radius-BorderWidth),color,Gradient?EndColor:color); return;
            }
            if(Kind==SokobanGraphicKind.Floor)
            {
                var a=UiFactory.Hex("EEE5D0"); var b=UiFactory.Hex("E4DAC2");
                int n=mesh.currentVertCount; mesh.AddVert(new Vector2(r.xMin,r.yMax),a,Vector2.zero); mesh.AddVert(new Vector2(r.xMax,r.yMax),Color.Lerp(a,b,.45f),Vector2.zero);
                mesh.AddVert(new Vector2(r.xMax,r.yMin),b,Vector2.zero); mesh.AddVert(new Vector2(r.xMin,r.yMin),Color.Lerp(a,b,.55f),Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
                Color border=UiFactory.Hex("BCAE8A"); border.a=.15f; float t=r.width/43.625f*.5f;
                Line(mesh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),t,border); Line(mesh,new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),t,border);
                Line(mesh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),t,border); Line(mesh,new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),t,border); return;
            }
            if(Kind==SokobanGraphicKind.Goal)
            {
                float size=Mathf.Min(r.width,r.height)*.24f;
                // The approved target is a rounded diamond, never a substitute asset tile.
                float radius=Radius,ring=Radius*.75f;
                if(GoalRing) Diamond(mesh,r.center,size+2*ring,radius+ring,UiFactory.Hex("8FA57C")*new Color(1,1,1,.15f));
                Diamond(mesh,r.center,size,radius,UiFactory.Hex("789667")); return;
            }
            if(Kind==SokobanGraphicKind.Check)
            {
                Rounded(mesh,r,Mathf.Min(r.width,r.height)/2,UiFactory.Accent);
                Line(mesh,r.center+new Vector2(-.27f,0)*r.width,r.center+new Vector2(-.07f,-.2f)*r.width,r.width*.09f,Color.white);
                Line(mesh,r.center+new Vector2(-.07f,-.2f)*r.width,r.center+new Vector2(.30f,.24f)*r.width,r.width*.09f,Color.white); return;
            }
            float extent=Mathf.Min(r.width,r.height),stroke=Kind==SokobanGraphicKind.Chevron?extent*.13f:extent*.09f;
            Vector2 p=new Vector2(-.35f,-.17f),q=new Vector2(0,.18f),s=new Vector2(.35f,-.17f);
            if(Kind==SokobanGraphicKind.Chevron) Direction=Direction.Down;
            float angle=Direction==Direction.Up?0:Direction==Direction.Right?-90:Direction==Direction.Down?180:90;
            var rotate=Quaternion.Euler(0,0,angle);
            Line(mesh,r.center+(Vector2)(rotate*p)*extent,r.center+(Vector2)(rotate*q)*extent,stroke,color);
            Line(mesh,r.center+(Vector2)(rotate*q)*extent,r.center+(Vector2)(rotate*s)*extent,stroke,color);
        }
        private static void Diamond(VertexHelper mesh,Vector2 center,float size,float radius,Color shade)
        {
            // Rounded corners are tessellated before rotation.
            int first=mesh.currentVertCount; mesh.AddVert(center,shade,Vector2.zero); const int segments=24;
            float half=size/2; radius=Mathf.Min(half,radius);
            for(int i=0;i<segments;i++)
            {
                int quadrant=i/6; float angle=(quadrant*90+(i%6)*18)*Mathf.Deg2Rad;
                var corner=new Vector2(quadrant==0||quadrant==3?half-radius:-half+radius,quadrant<2?half-radius:-half+radius);
                var point=corner+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                mesh.AddVert(center+(Vector2)(Quaternion.Euler(0,0,45)*point),shade,Vector2.zero);
            }
            for(int i=0;i<segments;i++) mesh.AddTriangle(first,first+1+i,first+1+(i+1)%segments);
        }
        private static void Rounded(VertexHelper mesh,Rect r,float radius,Color shade,Color? end=null)
        {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2); int first=mesh.currentVertCount; mesh.AddVert(r.center,Color.Lerp(shade,end??shade,.5f),Vector2.zero);
            const int segments=36;
            for(int i=0;i<segments;i++)
            {
                int quadrant=i/9; float angle=(quadrant*90+(i%9)*11.25f)*Mathf.Deg2Rad;
                Vector2 center=new Vector2(quadrant==0||quadrant==3?r.xMax-radius:r.xMin+radius,quadrant<2?r.yMax-radius:r.yMin+radius);
                var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                float mix=r.width>0&&r.height>0?((point.x-r.xMin)/r.width*.7f+(r.yMax-point.y)/r.height*.3f):0;
                mesh.AddVert(point,Color.Lerp(shade,end??shade,mix),Vector2.zero);
            }
            for(int i=0;i<segments;i++) mesh.AddTriangle(first,first+1+i,first+1+(i+1)%segments);
        }
        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color shade)
        {
            Vector2 d=b-a,n=new Vector2(-d.y,d.x).normalized*width/2; int i=mesh.currentVertCount;
            mesh.AddVert(a-n,shade,Vector2.zero); mesh.AddVert(a+n,shade,Vector2.zero); mesh.AddVert(b+n,shade,Vector2.zero); mesh.AddVert(b-n,shade,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
