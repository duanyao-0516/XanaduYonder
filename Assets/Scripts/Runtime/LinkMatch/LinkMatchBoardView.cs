using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.LinkMatch;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed class LinkMatchBoardView
    {
        internal static readonly string[] FruitIds={"strawberry","red-apple","tangerine","lemon","banana","watermelon","grapes","cherries","peach","pear","pineapple","avocado"};
        private readonly RectTransform parent;
        private readonly RectTransform[] tiles=new RectTransform[48];
        private readonly LinkMatchGraphic[] faces=new LinkMatchGraphic[48];
        private readonly RawImage[] fruits=new RawImage[48];
        private readonly Texture2D[] textures=new Texture2D[12];
        private readonly LinkMatchGraphic route;
        internal RectTransform Content { get; private set; }
        internal LinkMatchBoardView(RectTransform parent)
        {
            this.parent=parent;
            for(int i=0;i<12;i++) { textures[i]=Resources.Load<Texture2D>("LinkMatch/Sprites/"+FruitIds[i]); if(textures[i]==null) throw new InvalidOperationException("Missing LinkMatch fruit: "+FruitIds[i]); }
            Content=UiFactory.Rect(parent,"LinkMatch tiles",0,0,parent.rect.width,parent.rect.height); UiFactory.Stretch(Content);
            for(int i=0;i<48;i++)
            {
                tiles[i]=UiFactory.Rect(Content,"Tile "+i,0,0,1,1);
                var shadow=UiFactory.Rect(tiles[i],"Shadow",0,0,1,1).gameObject.AddComponent<LinkMatchGraphic>(); shadow.color=UiFactory.Hex("D6DCCB");shadow.raycastTarget=false;
                faces[i]=UiFactory.Rect(tiles[i],"Face",0,0,1,1).gameObject.AddComponent<LinkMatchGraphic>();faces[i].raycastTarget=false;
                fruits[i]=UiFactory.Rect(tiles[i],"Fruit",0,0,1,1).gameObject.AddComponent<RawImage>();fruits[i].raycastTarget=false;
            }
            route=UiFactory.Rect(Content,"LinkMatch path",0,0,1,1).gameObject.AddComponent<LinkMatchGraphic>(); UiFactory.Stretch(route.rectTransform);
            route.Kind=LinkMatchGraphicKind.Route;route.color=UiFactory.Hex("719B50");route.raycastTarget=false;
        }
        internal void Render(LinkMatchGame game,LinkMatchLayout l,int selected,int hintA=-1,int hintB=-1)
        {
            float s=(float)l.Scale,c=(float)l.Cell;
            for(int i=0;i<48;i++)
            {
                bool exists=i<game.Level.Columns*game.Level.Rows&&game.TileAt(i)>=0; tiles[i].gameObject.SetActive(exists); if(!exists) continue;
                Put(tiles[i],(float)l.GridLeft+i%l.Columns*c,(float)l.GridTop+i/l.Columns*c,c,c);
                var shadow=tiles[i].GetChild(0).GetComponent<LinkMatchGraphic>(); Put(shadow.rectTransform,2*s,4*s,c-4*s,c-4*s);shadow.Radius=9*s;shadow.SetVerticesDirty();
                var face=faces[i];Put(face.rectTransform,2*s,2*s,c-4*s,c-4*s);face.Radius=9*s;
                bool chosen=i==selected||i==hintA||i==hintB;
                face.BorderWidth=(chosen?2:1)*s;face.BorderColor=UiFactory.Hex(chosen?"78A15D":"E2E3D6");face.color=UiFactory.Hex(chosen?"F4F7E7":"FFFDF5");face.SetVerticesDirty();
                float size=c*.79f*(chosen?1.03f:1);Put(fruits[i].rectTransform,(c-size)/2,(c-size)/2,size,size);fruits[i].texture=textures[game.TileAt(i)];
            }
        }
        internal void ShowPath(LinkMatchPath path,LinkMatchLayout l)
        {
            if(path==null) { route.Points=null;route.SetVerticesDirty();return; }
            var points=new Vector2[path.Points.Count];
            for(int i=0;i<points.Length;i++)
            {
                var p=path.Points[i];
                double x=p.X<0?l.GridLeft-8*l.Scale:p.X>=l.Columns?l.GridLeft+l.Cell*l.Columns+8*l.Scale:l.GridLeft+(p.X+.5)*l.Cell;
                double y=p.Y<0?l.GridTop-8*l.Scale:p.Y>=l.Rows?l.GridTop+l.Cell*l.Rows+8*l.Scale:l.GridTop+(p.Y+.5)*l.Cell;
                points[i]=new Vector2((float)x,(float)y);
            }
            route.Points=points;route.Stroke=3*(float)l.Scale;route.SetVerticesDirty();
        }
        internal void Conceal(bool value) { Content.gameObject.SetActive(!value); }
        internal static void Put(RectTransform r,float x,float y,float w,float h) { r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); }
    }
}
