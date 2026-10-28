using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.FlappyBird
{
    internal enum FlappyGraphicKind { Rounded, Ellipse, Pause, Tap }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class FlappyGraphic : MaskableGraphic
    {
        internal FlappyGraphicKind Kind;
        internal float Radius;
        internal bool Sky;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0 || r.height <= 0) return;
            if (Kind == FlappyGraphicKind.Pause)
            {
                Stroke(mesh, r, new Vector2(.32f,.15f), new Vector2(.32f,.85f));
                Stroke(mesh, r, new Vector2(.68f,.15f), new Vector2(.68f,.85f));
                return;
            }
            if (Kind == FlappyGraphicKind.Tap)
            {
                Vector2[] hand = { new Vector2(.38f,.45f),new Vector2(.38f,.72f),new Vector2(.43f,.80f),new Vector2(.51f,.80f),new Vector2(.55f,.72f),new Vector2(.55f,.46f),new Vector2(.60f,.52f),new Vector2(.69f,.49f),new Vector2(.78f,.43f),new Vector2(.82f,.34f),new Vector2(.73f,.08f),new Vector2(.43f,.08f),new Vector2(.22f,.34f),new Vector2(.22f,.42f),new Vector2(.29f,.45f),new Vector2(.38f,.35f) };
                for (int i=1;i<hand.Length;i++) Stroke(mesh,r,hand[i-1],hand[i]);
                Stroke(mesh,r,new Vector2(.22f,.75f),new Vector2(.12f,.81f));
                Stroke(mesh,r,new Vector2(.30f,.88f),new Vector2(.25f,.98f));
                Stroke(mesh,r,new Vector2(.68f,.85f),new Vector2(.78f,.95f));
                return;
            }
            int first = mesh.currentVertCount;
            mesh.AddVert(r.center, Shade(.5f), Vector2.zero);
            const int segments = 48;
            float radius = Mathf.Clamp(Radius,0,Mathf.Min(r.width,r.height)*.5f);
            for (int i=0;i<segments;i++)
            {
                Vector2 p;
                if (Kind == FlappyGraphicKind.Ellipse)
                {
                    float a=i*Mathf.PI*2/segments;
                    p=r.center+new Vector2(Mathf.Cos(a)*r.width*.5f,Mathf.Sin(a)*r.height*.5f);
                }
                else
                {
                    int q=i/12; float a=(q*90+(i%12)*90f/11)*Mathf.Deg2Rad;
                    var center=new Vector2(q==0||q==3?r.xMax-radius:r.xMin+radius,q<2?r.yMax-radius:r.yMin+radius);
                    p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                }
                mesh.AddVert(p,Shade((r.yMax-p.y)/r.height),Vector2.zero);
            }
            for(int i=0;i<segments;i++) mesh.AddTriangle(first,first+1+i,first+1+(i+1)%segments);
        }
        private Color Shade(float depth)
        {
            if (!Sky) return color;
            return depth<.76f?Color.Lerp(UiFactory.Hex("D4E9E5"),UiFactory.Hex("E4F0E7"),depth/.76f):Color.Lerp(UiFactory.Hex("E4F0E7"),UiFactory.Hex("E4EDD5"),(depth-.76f)/.24f);
        }
        private void Stroke(VertexHelper mesh,Rect r,Vector2 a,Vector2 b)
        {
            a=new Vector2(r.xMin+a.x*r.width,r.yMin+a.y*r.height); b=new Vector2(r.xMin+b.x*r.width,r.yMin+b.y*r.height);
            var n=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Min(r.width,r.height)*.035f;
            int i=mesh.currentVertCount; mesh.AddVert(a-n,color,Vector2.zero); mesh.AddVert(a+n,color,Vector2.zero); mesh.AddVert(b+n,color,Vector2.zero); mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
    }
}
