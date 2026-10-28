using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed class SokobanBoardView
    {
        private sealed class MovingPiece { internal RectTransform Rect; internal int From,To; internal float Scale; }
        private readonly RectTransform content;
        private readonly List<MovingPiece> pieces=new List<MovingPiece>();
        private readonly List<RectTransform> tiles=new List<RectTransform>();
        private readonly Sprite wall,crate,goalCrate;
        private readonly bool miniature;
        private readonly Dictionary<Direction,Sprite> players=new Dictionary<Direction,Sprite>();
        private SokobanViewport viewport;
        private SokobanGame game;
        private float elapsed;
        internal bool Animating { get; private set; }
        internal RectTransform Content { get { return content; } }
        internal SokobanBoardView(RectTransform surface,bool miniature=false)
        {
            this.miniature=miniature;
            content=UiFactory.Rect(surface,"Sokoban map",0,0,1,1);
            var clip=content.gameObject.AddComponent<SokobanGraphic>(); clip.Kind=SokobanGraphicKind.Round; clip.Radius=miniature?5*surface.rect.width/82:5*surface.rect.width/349; clip.color=UiFactory.Hex("E9E0CA"); clip.raycastTarget=false;
            var mask=content.gameObject.AddComponent<Mask>(); mask.showMaskGraphic=true;
            wall=Load("wall"); crate=Load("crate"); goalCrate=Load("crate-goal");
            players.Add(Direction.Down,Load("player-down")); players.Add(Direction.Up,Load("player-up")); players.Add(Direction.Left,Load("player-left")); players.Add(Direction.Right,Load("player-right"));
        }
        private static Sprite Load(string name)
        { var sprite=Resources.Load<Sprite>("Sokoban/Sprites/"+name); if(sprite==null) throw new InvalidOperationException("Missing Sokoban sprite: "+name); return sprite; }
        internal void Render(SokobanGame model,SokobanViewport view,Direction direction,int previousPlayer=-1,int movedBoxFrom=-1,int movedBoxTo=-1)
        {
            game=model; viewport=view; Animating=previousPlayer>=0; elapsed=0; pieces.Clear(); tiles.Clear();
            for(int i=content.childCount-1;i>=0;i--) { var child=content.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            int count=model.Level.Width*model.Level.Height;
            for(int i=0;i<count;i++)
            {
                var tile=UiFactory.Rect(content,"Cell "+i,0,0,1,1); tiles.Add(tile); Position(tile,i,1);
                if(model.Level.IsWall(i))
                { var backing=tile.gameObject.AddComponent<Image>(); backing.color=UiFactory.Hex("E8DDC4"); backing.raycastTarget=false; AddImage(tile,"Wall",wall,1); }
                else
                {
                    var floor=tile.gameObject.AddComponent<SokobanGraphic>(); floor.Kind=SokobanGraphicKind.Floor; floor.raycastTarget=false;
                    if(model.Level.IsGoal(i)&&!model.HasBox(i))
                    { var goal=UiFactory.Rect(tile,"Goal",0,0,1,1); UiFactory.Stretch(goal); var shape=goal.gameObject.AddComponent<SokobanGraphic>(); shape.Kind=SokobanGraphicKind.Goal; shape.GoalRing=!miniature; shape.Radius=miniature?(float)(viewport.Width/82):4*(float)(viewport.Width/349); shape.raycastTarget=false; }
                }
            }
            for(int i=0;i<count;i++) if(model.HasBox(i))
            {
                var rect=UiFactory.Rect(content,"Box "+i,0,0,1,1);
                var image=AddImage(rect,"Crate",model.Level.IsGoal(i)?goalCrate:crate,1);
                if(model.Level.IsGoal(i))
                {
                    // Match the prototype's subdued completed crate without recoloring the source texture.
                    image.color=new Color(.94f,.97f,.94f);
                    if(!miniature)
                    {
                    var mark=UiFactory.Rect(rect,"Goal check",0,0,1,1); mark.anchorMin=mark.anchorMax=new Vector2(.95f,.03f); mark.pivot=new Vector2(1,0);
                    var check=mark.gameObject.AddComponent<SokobanGraphic>(); check.Kind=SokobanGraphicKind.Check; check.raycastTarget=false;
                    }
                }
                pieces.Add(new MovingPiece { Rect=rect,From=i==movedBoxTo?movedBoxFrom:i,To=i,Scale=1 });
            }
            var player=UiFactory.Rect(content,"Player",0,0,1,1); AddImage(player,"Player sprite",players[direction],.91f);
            pieces.Add(new MovingPiece { Rect=player,From=previousPlayer>=0?previousPlayer:model.Player,To=model.Player,Scale=1 });
            Layout();
        }
        private static Image AddImage(RectTransform parent,string name,Sprite sprite,float scale)
        {
            var rect=UiFactory.Rect(parent,name,0,0,1,1); rect.anchorMin=Vector2.one*((1-scale)/2); rect.anchorMax=Vector2.one*((1+scale)/2); rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=rect.gameObject.AddComponent<Image>(); image.sprite=sprite; image.preserveAspect=true; image.raycastTarget=false; return image;
        }
        internal void Layout()
        {
            if(game==null||viewport==null) return;
            content.anchoredPosition=new Vector2((float)viewport.OffsetX,-(float)viewport.OffsetY);
            content.sizeDelta=new Vector2((float)(game.Level.Width*viewport.CellSize),(float)(game.Level.Height*viewport.CellSize));
            for(int i=0;i<tiles.Count;i++) Position(tiles[i],i,1);
            float t=Animating?Mathf.Clamp01(elapsed/.10f):1; t=t*t*(3-2*t);
            foreach(var piece in pieces)
            {
                Vector2 from=Cell(piece.From),to=Cell(piece.To); piece.Rect.anchoredPosition=Vector2.Lerp(from,to,t); piece.Rect.sizeDelta=Vector2.one*(float)viewport.CellSize;
                var check=piece.Rect.Find("Goal check") as RectTransform;
                if(check!=null) { float size=(float)viewport.CellSize*.36f; check.sizeDelta=new Vector2(size,size); }
            }
        }
        private Vector2 Cell(int index) { return new Vector2((float)(index%game.Level.Width*viewport.CellSize),-(float)(index/game.Level.Width*viewport.CellSize)); }
        private void Position(RectTransform rect,int index,float scale) { rect.anchoredPosition=Cell(index); rect.sizeDelta=Vector2.one*(float)viewport.CellSize*scale; }
        internal bool Tick(float delta)
        {
            if(!Animating) return false; elapsed+=delta;
            if(elapsed>=.10f) Animating=false; Layout(); return !Animating;
        }
        internal void StopAnimation() { Animating=false; elapsed=.10f; Layout(); }
    }
}
