using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    // One UGUI mesh owns rendering only. Logical positions always come from SnakeGame.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SnakeBoardView : MaskableGraphic
    {
        private SnakeRules rules;
        private SnakeSnapshot current, previous;
        private bool[] walls;
        private float animationAge, animationDuration;
        private static readonly Color Tile = UiFactory.Hex("E9DFC5"), Alt = UiFactory.Hex("ECE3CD"), Wall = UiFactory.Hex("819C9E");
        private static readonly Color Body = UiFactory.Hex("57916B"), Fruit = UiFactory.Hex("C75D43"), Grid = UiFactory.Hex("DDD4BD");
        public void Render(SnakeRules nextRules, SnakeSnapshot snapshot, bool animate = false, float duration = .14f)
        {
            bool same = rules != null && rules.Width == nextRules.Width && rules.Height == nextRules.Height && rules.LevelId == nextRules.LevelId && rules.Boundary == nextRules.Boundary;
            previous = animate && same && current != null ? current : null;
            rules = nextRules.Copy(); current = snapshot.Copy();
            walls = new bool[rules.Width * rules.Height]; foreach (int cell in rules.Obstacles) walls[cell] = true;
            animationAge = 0; animationDuration = Mathf.Clamp(duration, .05f, .2f); SetVerticesDirty();
        }
        public void FinishAnimation() { previous = null; SetVerticesDirty(); }
        private void Update()
        {
            if (previous == null) return;
            animationAge += Time.unscaledDeltaTime;
            if (animationAge >= animationDuration) previous = null;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (rules == null || current == null) return;
            var bounds = GetPixelAdjustedRect();
            float cell = Mathf.Min(bounds.width/rules.Width, bounds.height/rules.Height);
            var grid = new Rect(bounds.center.x-rules.Width*cell/2, bounds.center.y-rules.Height*cell/2, rules.Width*cell, rules.Height*cell);
            for (int y=0;y<rules.Height;y++) for (int x=0;x<rules.Width;x++)
            {
                var tile = new Rect(grid.x+x*cell, grid.yMax-(y+1)*cell, cell, cell);
                Quad(mesh,tile,(x+y)%2==0?Tile:Alt);
                Quad(mesh,new Rect(tile.xMax-.6f,tile.y,.6f,cell),Grid);
                Quad(mesh,new Rect(tile.x,tile.y,cell,.6f),Grid);
                if (!walls[y*rules.Width+x]) continue;
                Rounded(mesh,Inset(tile,.4f),Mathf.Min(3,cell*.12f),Wall);
                var mortar=UiFactory.Hex("6E8D8D");
                for(int row=1;row<3;row++) Quad(mesh,new Rect(tile.x+.7f,tile.y+row*cell/3,cell-1.4f,.75f),mortar);
                Quad(mesh,new Rect(tile.center.x,tile.y+cell/3,.75f,cell/3),mortar);
                Quad(mesh,new Rect(tile.x+cell*.25f,tile.y,.75f,cell/3),mortar);
                Quad(mesh,new Rect(tile.x+cell*.75f,tile.y+cell*2/3,.75f,cell/3),mortar);
            }
            Border(mesh,grid);
            float t=previous==null?1:Mathf.Clamp01(animationAge/animationDuration);
            for(int i=current.Body.Length-1;i>=0;i--)
            {
                Vector2 center=Center(current.Body[i],grid,cell);
                if(previous!=null)
                {
                    int old=previous.Body[Mathf.Min(i,previous.Body.Length-1)];
                    var from=Center(old,grid,cell); var delta=center-from;
                    if(rules.Boundary==SnakeBoundary.Wrap)
                    {
                        if(Mathf.Abs(delta.x)>cell*1.5f) delta.x-=Mathf.Sign(delta.x)*grid.width;
                        if(Mathf.Abs(delta.y)>cell*1.5f) delta.y-=Mathf.Sign(delta.y)*grid.height;
                    }
                    center=from+delta*t;
                }
                DrawSegment(mesh,center,cell,i,grid);
                if(rules.Boundary==SnakeBoundary.Wrap)
                {
                    if(center.x-cell/2<grid.x) DrawSegment(mesh,center+Vector2.right*grid.width,cell,i,grid);
                    if(center.x+cell/2>grid.xMax) DrawSegment(mesh,center-Vector2.right*grid.width,cell,i,grid);
                    if(center.y-cell/2<grid.y) DrawSegment(mesh,center+Vector2.up*grid.height,cell,i,grid);
                    if(center.y+cell/2>grid.yMax) DrawSegment(mesh,center-Vector2.up*grid.height,cell,i,grid);
                }
            }
            if(current.Food>=0)
            {
                var center=Center(current.Food,grid,cell);
                Rounded(mesh,new Rect(center.x-cell*.36f,center.y-cell*.34f,cell*.72f,cell*.7f),cell*.22f,Fruit);
                Quad(mesh,new Rect(center.x-cell*.05f,center.y+cell*.31f,cell*.1f,cell*.22f),UiFactory.Ink);
                Rounded(mesh,new Rect(center.x,center.y+cell*.34f,cell*.24f,cell*.12f),cell*.05f,UiFactory.Accent);
            }
        }
        private void DrawSegment(VertexHelper mesh, Vector2 center, float cell, int index, Rect grid)
        {
            var rect=new Rect(center.x-cell*.47f,center.y-cell*.47f,cell*.94f,cell*.94f);
            rect=Clip(rect,grid); if(rect.width<=0||rect.height<=0)return;
            Rounded(mesh,rect,cell*.18f,index==0?UiFactory.Accent:Body);
            if(index!=0)return;
            Vector2 forward=current.Direction==SnakeDirection.Up?Vector2.up:current.Direction==SnakeDirection.Down?Vector2.down:current.Direction==SnakeDirection.Left?Vector2.left:Vector2.right;
            var side=new Vector2(-forward.y,forward.x);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var eye=center+forward*cell*.21f+side*sign*cell*.22f;
                var r=Clip(new Rect(eye.x-cell*.065f,eye.y-cell*.065f,cell*.13f,cell*.13f),grid);
                if(r.width>0&&r.height>0) Rounded(mesh,r,cell*.04f,current.State==SnakeState.Lost?Fruit:UiFactory.Paper);
            }
        }
        private void Border(VertexHelper mesh, Rect rect)
        {
            bool wrap=rules.Boundary==SnakeBoundary.Wrap; var tint=wrap?UiFactory.Hex("9EAF95"):Wall;
            float width=wrap?1.4f:2f;
            // The outer edge matches the parent's 16-unit rounded mask. Keep the whole stroke inside it.
            float outerRadius=Mathf.Min(16f,Mathf.Min(rect.width,rect.height)/2);
            if(outerRadius<=width)return;
            var path=Inset(rect,width/2);float radius=outerRadius-width/2;
            float perimeter=2*(path.width+path.height-4*radius)+2*Mathf.PI*radius;
            if(!wrap){BorderStroke(mesh,path,radius,width,0,perimeter,tint);return;}
            // One distance/phase for the entire loop lets dashes continue through all four corners.
            // Fit a whole number of periods so the closing gap is the same size as every other gap.
            int periods=Mathf.Max(1,Mathf.RoundToInt(perimeter/12f));
            float pitch=perimeter/periods,dash=pitch*(7f/12f),offset=(pitch-dash)/2;
            for(int i=0;i<periods;i++)BorderStroke(mesh,path,radius,width,i*pitch+offset,i*pitch+offset+dash,tint);
        }
        private static void BorderStroke(VertexHelper mesh,Rect path,float radius,float width,float start,float end,Color tint)
        {
            Vector2 normal;var point=BorderPoint(path,radius,start,out normal);float half=width/2;
            var outer=point+normal*half;var inner=point-normal*half;
            // At most 11.25 degrees per curved segment; straight and curved sections share vertices.
            float step=Mathf.Min(4f,radius*Mathf.PI/16);
            while(start<end)
            {
                float next=Mathf.Min(end,start+step);
                point=BorderPoint(path,radius,next,out normal);
                var nextOuter=point+normal*half;var nextInner=point-normal*half;int index=mesh.currentVertCount;
                mesh.AddVert(outer,tint,Vector2.zero);mesh.AddVert(nextOuter,tint,Vector2.zero);
                mesh.AddVert(nextInner,tint,Vector2.zero);mesh.AddVert(inner,tint,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
                outer=nextOuter;inner=nextInner;start=next;
            }
        }
        private static Vector2 BorderPoint(Rect path,float radius,float distance,out Vector2 normal)
        {
            float horizontal=path.width-2*radius,vertical=path.height-2*radius,arc=Mathf.PI*radius/2;
            if(distance<=horizontal){normal=Vector2.up;return new Vector2(path.x+radius+distance,path.yMax);}
            distance-=horizontal;
            if(distance<=arc)return BorderArc(new Vector2(path.xMax-radius,path.yMax-radius),radius,Mathf.PI/2-distance/radius,out normal);
            distance-=arc;
            if(distance<=vertical){normal=Vector2.right;return new Vector2(path.xMax,path.yMax-radius-distance);}
            distance-=vertical;
            if(distance<=arc)return BorderArc(new Vector2(path.xMax-radius,path.y+radius),radius,-distance/radius,out normal);
            distance-=arc;
            if(distance<=horizontal){normal=Vector2.down;return new Vector2(path.xMax-radius-distance,path.y);}
            distance-=horizontal;
            if(distance<=arc)return BorderArc(new Vector2(path.x+radius,path.y+radius),radius,-Mathf.PI/2-distance/radius,out normal);
            distance-=arc;
            if(distance<=vertical){normal=Vector2.left;return new Vector2(path.x,path.y+radius+distance);}
            distance-=vertical;
            return BorderArc(new Vector2(path.x+radius,path.yMax-radius),radius,Mathf.PI-Mathf.Clamp(distance,0,arc)/radius,out normal);
        }
        private static Vector2 BorderArc(Vector2 center,float radius,float angle,out Vector2 normal)
        {
            normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));return center+normal*radius;
        }
        private Vector2 Center(int index, Rect grid, float cell) { return new Vector2(grid.x+(index%rules.Width+.5f)*cell,grid.yMax-(index/rules.Width+.5f)*cell); }
        private static Rect Inset(Rect r,float d) { return new Rect(r.x+d,r.y+d,r.width-d*2,r.height-d*2); }
        private static Rect Clip(Rect a,Rect b) { float x=Mathf.Max(a.x,b.x),y=Mathf.Max(a.y,b.y);return new Rect(x,y,Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-x),Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-y)); }
        private static void Quad(VertexHelper mesh,Rect r,Color tint)
        {
            int i=mesh.currentVertCount;mesh.AddVert(new Vector2(r.x,r.y),tint,Vector2.zero);mesh.AddVert(new Vector2(r.x,r.yMax),tint,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax,r.yMax),tint,Vector2.zero);mesh.AddVert(new Vector2(r.xMax,r.y),tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        private static void Rounded(VertexHelper mesh,Rect rect,float radius,Color tint)
        {
            radius=Mathf.Min(radius,Mathf.Min(rect.width,rect.height)/2);
            int center=mesh.currentVertCount;mesh.AddVert(rect.center,tint,Vector2.zero);
            for(int corner=0;corner<4;corner++)
            {
                Vector2 c=corner==0?new Vector2(rect.xMax-radius,rect.yMax-radius):corner==1?new Vector2(rect.x+radius,rect.yMax-radius):corner==2?new Vector2(rect.x+radius,rect.y+radius):new Vector2(rect.xMax-radius,rect.y+radius);
                for(int j=0;j<=4;j++)
                { float a=(corner*90+j*22.5f)*Mathf.Deg2Rad;mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero); }
            }
            for(int i=0;i<20;i++)mesh.AddTriangle(center,center+1+i,center+1+(i+1)%20);
        }
    }
}
