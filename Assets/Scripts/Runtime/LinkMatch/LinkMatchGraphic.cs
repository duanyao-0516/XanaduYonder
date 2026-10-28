using UnityEngine;
using UnityEngine.UI;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal enum LinkMatchGraphicKind { Round, Route, Hint, Shuffle, Pause, Chevron }
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class LinkMatchGraphic : MaskableGraphic
    {
        internal LinkMatchGraphicKind Kind;
        internal float Radius,BorderWidth,Stroke=3;
        internal Color BorderColor;
        internal Vector2[] Points;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r=GetPixelAdjustedRect();
            if(Kind==LinkMatchGraphicKind.Round)
            {
                if(BorderWidth>0) { Rounded(mesh,r,Radius,BorderColor); r=new Rect(r.x+BorderWidth,r.y+BorderWidth,Mathf.Max(0,r.width-2*BorderWidth),Mathf.Max(0,r.height-2*BorderWidth)); }
                Rounded(mesh,r,Mathf.Max(0,Radius-BorderWidth),color); return;
            }
            if(Kind==LinkMatchGraphicKind.Route)
            {
                if(Points==null) return;
                for(int pass=0;pass<2;pass++) for(int i=1;i<Points.Length;i++)
                {
                    var a=new Vector2(r.xMin+Points[i-1].x,r.yMax-Points[i-1].y); var b=new Vector2(r.xMin+Points[i].x,r.yMax-Points[i].y);
                    Line(mesh,a,b,pass==0?Stroke*2:Stroke,pass==0?UiFactory.Paper:color);
                }
                return;
            }
            float size=Mathf.Min(r.width,r.height);
            if(Kind==LinkMatchGraphicKind.Pause) { IconLine(mesh,r,size,-.16f,.3f,-.16f,-.3f); IconLine(mesh,r,size,.16f,.3f,.16f,-.3f); }
            else if(Kind==LinkMatchGraphicKind.Chevron) { IconLine(mesh,r,size,-.3f,.15f,0,-.15f); IconLine(mesh,r,size,0,-.15f,.3f,.15f); }
            else if(Kind==LinkMatchGraphicKind.Shuffle)
            {
                IconLine(mesh,r,size,-.4f,.22f,-.2f,.22f); IconLine(mesh,r,size,-.2f,.22f,.23f,-.22f); IconLine(mesh,r,size,.23f,-.22f,.4f,-.22f);
                IconLine(mesh,r,size,-.4f,-.22f,-.2f,-.22f); IconLine(mesh,r,size,-.2f,-.22f,.23f,.22f); IconLine(mesh,r,size,.23f,.22f,.4f,.22f);
                IconLine(mesh,r,size,.25f,.36f,.4f,.22f); IconLine(mesh,r,size,.4f,.22f,.25f,.08f);
                IconLine(mesh,r,size,.25f,-.08f,.4f,-.22f); IconLine(mesh,r,size,.4f,-.22f,.25f,-.36f);
            }
            else
            {
                Vector2 previous=r.center+new Vector2(-.18f,-.13f)*size;
                for(int i=0;i<=24;i++) { float angle=(210-i*240f/24)*Mathf.Deg2Rad; var next=r.center+new Vector2(Mathf.Cos(angle)*.32f,.16f+Mathf.Sin(angle)*.32f)*size; Line(mesh,previous,next,size*.065f,color); previous=next; }
                Line(mesh,previous,r.center+new Vector2(.18f,-.13f)*size,size*.065f,color);
                IconLine(mesh,r,size,-.16f,-.25f,.16f,-.25f); IconLine(mesh,r,size,-.12f,-.36f,.12f,-.36f);
            }
        }
        private void IconLine(VertexHelper mesh,Rect r,float s,float ax,float ay,float bx,float by) { Line(mesh,r.center+new Vector2(ax,ay)*s,r.center+new Vector2(bx,by)*s,s*.07f,color); }
        private static void Rounded(VertexHelper mesh,Rect r,float radius,Color shade)
        {
            radius=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)/2); int first=mesh.currentVertCount; mesh.AddVert(r.center,shade,Vector2.zero);
            const int count=36;
            for(int i=0;i<count;i++)
            {
                int q=i/9; float a=(q*90+i%9*11.25f)*Mathf.Deg2Rad;
                Vector2 c=new Vector2(q==0||q==3?r.xMax-radius:r.xMin+radius,q<2?r.yMax-radius:r.yMin+radius);
                mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,shade,Vector2.zero);
            }
            for(int i=0;i<count;i++) mesh.AddTriangle(first,first+1+i,first+1+(i+1)%count);
        }
        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color shade)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width/2; int i=mesh.currentVertCount;
            mesh.AddVert(a-n,shade,Vector2.zero);mesh.AddVert(a+n,shade,Vector2.zero);mesh.AddVert(b+n,shade,Vector2.zero);mesh.AddVert(b-n,shade,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
