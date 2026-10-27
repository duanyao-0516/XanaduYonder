using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed class MinesweeperBoardView
    {
        private readonly RectTransform surface;
        private RectTransform content;
        private Image[] cells;
        private Text[] numbers;
        private MinesweeperIconGraphic[] symbols;
        private readonly RectTransform horizontal, vertical;
        private readonly RectTransform[] outline = new RectTransform[4];
        private MinesweeperSnapshot snapshot;
        private int preview=-1;
        private int[] flashed=new int[0], fading=new int[0];
        private float fadeTime, flashTime;
        private static readonly Color Covered=UiFactory.Hex("DCE5D8"), Open=UiFactory.Hex("EFEFE5"), Flag=UiFactory.Hex("B34842");
        private static readonly Color[] DigitColors={UiFactory.Ink,UiFactory.Hex("356B83"),UiFactory.Accent,UiFactory.Hex("AC594C"),UiFactory.Hex("70628D"),UiFactory.Hex("926838"),UiFactory.Hex("39837D"),UiFactory.Ink,UiFactory.Muted};
        public RectTransform Content { get { return content; } }
        public MinesweeperBoardView(RectTransform viewport)
        {
            surface=viewport;
            horizontal=UiFactory.Panel(viewport,"Horizontal range",0,0,10,4,UiFactory.Muted).rectTransform;
            vertical=UiFactory.Panel(viewport,"Vertical range",0,0,4,10,UiFactory.Muted).rectTransform;
            for(int i=0;i<4;i++) outline[i]=UiFactory.Panel(viewport,"Overview bounds "+i,0,0,1,1,UiFactory.Accent,false).rectTransform;
        }
        private void Build(int width,int height)
        {
            if(content!=null) { content.gameObject.SetActive(false); UnityEngine.Object.Destroy(content.gameObject); }
            content=UiFactory.Rect(surface,"Minesweeper board",0,0,width*72,height*72);
            content.SetAsFirstSibling();
            int count=width*height;
            cells=new Image[count]; numbers=new Text[count]; symbols=new MinesweeperIconGraphic[count];
            for(int i=0;i<count;i++)
            {
                cells[i]=UiFactory.Panel(content,"Cell "+i,i%width*72+2,i/width*72+2,68,68,Covered);
                numbers[i]=UiFactory.Label(cells[i].transform,"",0,0,68,68,44,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
                var icon=UiFactory.Rect(cells[i].transform,"Cell symbol",15,15,38,38).gameObject.AddComponent<MinesweeperIconGraphic>();
                icon.raycastTarget=false; icon.ContrastFlagPole=true; symbols[i]=icon;
            }
        }
        public void Render(MinesweeperSnapshot value)
        {
            if(snapshot==null||snapshot.Width!=value.Width||snapshot.Height!=value.Height) Build(value.Width,value.Height);
            snapshot=value;
            for(int i=0;i<cells.Length;i++) Paint(i);
        }
        private void Paint(int i)
        {
            bool lost=snapshot.State==MinesweeperState.Lost, won=snapshot.State==MinesweeperState.Won;
            bool mine=snapshot.IsMine(i), flag=snapshot.IsFlagged(i), revealed=snapshot.IsRevealed(i);
            bool marked=Array.IndexOf(flashed,i)>=0;
            cells[i].color=i==preview||marked?UiFactory.Hex("B5D2B6"):snapshot.DetonatedIndex==i?UiFactory.Hex("E6B5A4"):revealed?Open:Covered;
            symbols[i].gameObject.SetActive(flag||(lost&&mine)||(won&&mine));
            if(flag||won&&mine)
            {
                symbols[i].Kind=lost&&!mine?MinesweeperIconKind.WrongFlag:MinesweeperIconKind.Flag;
                symbols[i].color=Flag;
            }
            else { symbols[i].Kind=MinesweeperIconKind.Mine; symbols[i].color=UiFactory.Ink; }
            int n=revealed&&!mine?snapshot.AdjacentMines(i):0;
            numbers[i].text=n>0?n.ToString():""; numbers[i].color=DigitColors[n];
        }
        public void Preview(int cell)
        {
            if(preview==cell||snapshot==null) return;
            int previous=preview; preview=cell;
            if(previous>=0&&previous<cells.Length) Paint(previous);
            if(cell>=0&&cell<cells.Length) Paint(cell);
        }
        public void FlashNeighbors(int cell)
        {
            if(snapshot==null) return;
            ClearEffects(); flashed=MinesweeperRules.Neighbors(cell,snapshot.Width,snapshot.Height); flashTime=.35f;
            foreach(int i in flashed) Paint(i);
        }
        public void Animate(int[] changed) { ClearEffects(); fading=changed; fadeTime=.1f; Tick(0); }
        public void Tick(float delta)
        {
            if(fadeTime>0)
            {
                fadeTime=Mathf.Max(0,fadeTime-delta);
                foreach(int i in fading)
                {
                    if(i<0||i>=numbers.Length) continue;
                    Paint(i);
                    var n=numbers[i].color; n.a=1-fadeTime*7; numbers[i].color=n;
                    var s=symbols[i].color; s.a=n.a; symbols[i].color=s;
                }
                if(fadeTime==0) fading=new int[0];
            }
            if(flashTime>0) { flashTime-=delta; if(flashTime<=0) ClearEffects(); }
        }
        public void ClearEffects()
        {
            var old=flashed; flashed=new int[0]; flashTime=0; fadeTime=0;
            if(snapshot!=null)
            {
                foreach(int i in old) Paint(i);
                foreach(int i in fading) if(i>=0&&i<cells.Length) Paint(i);
            }
            fading=new int[0]; Preview(-1);
        }
        public void RenderView(MinesweeperViewport view)
        {
            if(content==null) return;
            content.localScale=Vector3.one*(float)(view.CellSize/72);
            content.anchoredPosition=new Vector2((float)view.OffsetX,-(float)view.OffsetY);
            bool h=!view.IsOverview&&view.Columns*view.CellSize>view.Width;
            bool v=!view.IsOverview&&view.Rows*view.CellSize>view.Height;
            horizontal.gameObject.SetActive(h); vertical.gameObject.SetActive(v);
            if(h)
            {
                double track=view.Width-16, length=track*view.Width/(view.Columns*view.CellSize);
                Place(horizontal,8-view.OffsetX/(view.Columns*view.CellSize-view.Width)*(track-length),view.Height-7,length,3);
            }
            if(v)
            {
                double track=view.Height-16, length=track*view.Height/(view.Rows*view.CellSize);
                Place(vertical,view.Width-7,8-view.OffsetY/(view.Rows*view.CellSize-view.Height)*(track-length),3,length);
            }
            foreach(var line in outline) line.gameObject.SetActive(view.IsOverview);
            if(view.IsOverview)
            {
                var bounds=view.NormalVisibleBounds;
                var p=view.BoardToViewport(new MinesweeperInputPosition(bounds.X,bounds.Y));
                double w=bounds.Width*view.CellSize,hgt=bounds.Height*view.CellSize;
                Place(outline[0],p.X,p.Y,w,3); Place(outline[1],p.X,p.Y+hgt-3,w,3);
                Place(outline[2],p.X,p.Y,3,hgt); Place(outline[3],p.X+w-3,p.Y,3,hgt);
            }
        }
        private static void Place(RectTransform r,double x,double y,double w,double h)
        { r.anchoredPosition=new Vector2((float)x,-(float)y); r.sizeDelta=new Vector2((float)w,(float)h); }
    }
}
