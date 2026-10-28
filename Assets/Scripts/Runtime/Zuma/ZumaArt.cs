using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Zuma
{
    internal static class ZumaArt
    {
        public static readonly Color Gold=new Color(1,1,0);
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        { return UiFactory.Rect(parent,name,x,y,w,h); }
        public static RawImage Image(Transform parent,string name,Texture texture,Rect source,float x,float y,float width,float height)
        {
            var image=Rect(parent,name,x,y,width,height).gameObject.AddComponent<RawImage>();
            image.raycastTarget=false; SetSource(image,texture,source); return image;
        }
        public static void SetSource(RawImage image,Texture texture,Rect source)
        {
            image.texture=texture;
            if(texture!=null) image.uvRect=new Rect(source.x/texture.width,1-(source.y+source.height)/texture.height,source.width/texture.width,source.height/texture.height);
        }
        public static void Center(RawImage image,float x,float y,float width,float height,float angle=0)
        {
            var rect=image.rectTransform;
            rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=new Vector2(x,-y);
            rect.sizeDelta=new Vector2(width,height); rect.localEulerAngles=new Vector3(0,0,-angle);
        }
        public static ZumaBitmapText Text(Transform parent,ZumaAssets assets,string font,string value,float x,float y,float width,float height,Color color,bool centered=false,float scale=1.5f)
        {
            var label=Rect(parent,"Text "+value,x,y,width,height).gameObject.AddComponent<ZumaBitmapText>();
            label.Initialize(assets.LoadFont(font),assets.Texture("Fonts/"+font),scale,centered);
            label.color=color; label.SetText(value); return label;
        }
        public static ZumaSkinButton Button(Transform parent,Texture texture,Rect source,float x,float y,float width,float height,Action action,bool threeSlice=false)
        {
            var rect=Rect(parent,"Zuma button",x,y,width,height);
            var hit=rect.gameObject.AddComponent<Image>(); hit.color=Color.clear;
            var button=rect.gameObject.AddComponent<ZumaSkinButton>();
            button.Initialize(texture,source,action,threeSlice);
            return button;
        }
        public static RectTransform Panel(Transform parent,Texture menu,int columns,int rows,bool godHead)
        {
            var w=columns*121; var h=rows*121;
            var panel=Rect(parent,"Original stone dialog",(1280-w)/2f,(720-h)/2f,w,h);
            for(var row=0;row<rows;row++) for(var col=0;col<columns;col++)
                Image(panel,"Stone tile",menu,new Rect(col==0?0:col==columns-1?242:121,row==0?0:row==rows-1?242:121,121,121),col*121,row*121,121,121);
            if(godHead) Image(panel,"God head",menu,new Rect(363,0,234,210),w/2f-117,-112,234,210);
            return panel;
        }
    }

    // Original three atlas states, including repeated center tiles for wide buttons.
    internal sealed class ZumaSkinButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Texture texture;
        private Rect source;
        private Action action;
        private readonly List<RawImage> pieces=new List<RawImage>();
        private readonly List<Rect> slices=new List<Rect>();
        private int owner;
        private bool pressed,hover,selected;
        public void Initialize(Texture tex,Rect rect,Action callback,bool threeSlice)
        {
            texture=tex;source=rect;action=callback;
            var own=(RectTransform)transform;
            if(!threeSlice) AddPiece(source,0,own.rect.width,own.rect.height);
            else
            {
                var cap=source.width/3;
                AddPiece(new Rect(source.x,source.y,cap,source.height),0,cap,own.rect.height);
                for(float x=cap;x<own.rect.width-cap;x+=cap)
                {
                    var w=Mathf.Min(cap,own.rect.width-cap-x);
                    AddPiece(new Rect(source.x+cap,source.y,w,source.height),x,w,own.rect.height);
                }
                AddPiece(new Rect(source.x+2*cap,source.y,cap,source.height),own.rect.width-cap,cap,own.rect.height);
            }
        }
        private void AddPiece(Rect slice,float x,float width,float height)
        { slices.Add(slice);pieces.Add(ZumaArt.Image(transform,"Button skin",texture,slice,x,0,width,height)); }
        public void Select(bool value) { selected=value; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hover=true;Refresh(); }
        public void OnPointerExit(PointerEventData e) { hover=false;Refresh(); }
        public void OnPointerDown(PointerEventData e)
        { if(e.button!=PointerEventData.InputButton.Left||pressed)return;pressed=true;owner=e.pointerId;Refresh(); }
        public void OnPointerUp(PointerEventData e)
        {
            if(!pressed||e.pointerId!=owner)return;
            pressed=false;Refresh();
            for(var i=0;i<Input.touchCount;i++){var t=Input.GetTouch(i);if(t.fingerId==owner&&t.phase==TouchPhase.Canceled)return;}
            if(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,e.position,e.pressEventCamera)) action?.Invoke();
        }
        private void OnDisable() { pressed=hover=false; Refresh(); }
        private void Refresh()
        {
            var state=(pressed||selected)?2:hover?1:0;
            for(var i=0;i<pieces.Count;i++){var rect=slices[i];rect.x+=state*source.width;ZumaArt.SetSource(pieces[i],texture,rect);}
        }
    }

    // One mesh per label, using the source bitmap glyphs, advances, offsets and kerning.
    internal sealed class ZumaBitmapText : MaskableGraphic
    {
        private ZumaBitmapFont font;
        private Texture atlas;
        private float scale;
        private bool centered;
        private string value="";
        private readonly Dictionary<char,ZumaGlyph> glyphs=new Dictionary<char,ZumaGlyph>();
        private readonly Dictionary<string,int> kerning=new Dictionary<string,int>();
        public override Texture mainTexture { get { return atlas!=null?atlas:Texture2D.whiteTexture; } }
        public void Initialize(ZumaBitmapFont data,Texture texture,float fontScale,bool center)
        {
            font=data;atlas=texture;scale=fontScale;centered=center;raycastTarget=false;
            glyphs.Clear();kerning.Clear();
            foreach(var g in font.glyphs) if(!string.IsNullOrEmpty(g.character)) glyphs[g.character[0]]=g;
            foreach(var k in font.kerning) kerning[k.pair]=k.amount;
            SetAllDirty();
        }
        public void SetText(string text)
        {
            if(value==text)return;
            // The original display font stores C/M/V only under lowercase keys
            // (the source itself uses mENU and GAmE OvER). Reuse those exact glyphs.
            var chars=(text??"").ToCharArray();
            for(var i=0;i<chars.Length;i++)
                if(!glyphs.ContainsKey(chars[i])&&glyphs.ContainsKey(char.ToLowerInvariant(chars[i])))chars[i]=char.ToLowerInvariant(chars[i]);
            var normalized=new string(chars);if(value==normalized)return;value=normalized;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();if(font==null||atlas==null)return;
            var lines=value.Split('\n');
            for(var lineIndex=0;lineIndex<lines.Length;lineIndex++)
            {
                var line=lines[lineIndex];var start=centered?(rectTransform.rect.width-Measure(line)*scale)/2:0;
                AddLine(helper,line,start+1,lineIndex*font.lineHeight*scale-1,new Color32(0,0,0,160));
                AddLine(helper,line,start,lineIndex*font.lineHeight*scale,color);
            }
        }
        private int Measure(string text)
        {
            var width=0;
            for(var i=0;i<text.Length;i++)
            {
                if(text[i]==' '){width+=font.spaceAdvance;continue;}
                ZumaGlyph glyph;if(!glyphs.TryGetValue(text[i],out glyph))continue;
                int amount;if(i+1<text.Length&&kerning.TryGetValue(text.Substring(i,2),out amount))width+=amount;
                width+=glyph.advance;
            }
            return width;
        }
        private void AddLine(VertexHelper helper,string text,float start,float top,Color32 tint)
        {
            var advance=0;
            for(var i=0;i<text.Length;i++)
            {
                if(text[i]==' '){advance+=font.spaceAdvance;continue;}
                ZumaGlyph glyph;if(!glyphs.TryGetValue(text[i],out glyph))continue;
                int amount;if(i+1<text.Length&&kerning.TryGetValue(text.Substring(i,2),out amount))advance+=amount;
                var x=start+(advance+glyph.offsetX)*scale;
                var y=-top-glyph.offsetY*scale;
                var w=glyph.width*scale;var h=glyph.height*scale;
                var u=(float)glyph.x/atlas.width;var v=1f-(float)glyph.y/atlas.height;
                var du=(float)glyph.width/atlas.width;var dv=(float)glyph.height/atlas.height;
                var n=helper.currentVertCount;
                helper.AddVert(new Vector3(x,y),tint,new Vector2(u,v));
                helper.AddVert(new Vector3(x+w,y),tint,new Vector2(u+du,v));
                helper.AddVert(new Vector3(x+w,y-h),tint,new Vector2(u+du,v-dv));
                helper.AddVert(new Vector3(x,y-h),tint,new Vector2(u,v-dv));
                helper.AddTriangle(n,n+1,n+2);helper.AddTriangle(n,n+2,n+3);
                advance+=glyph.advance;
            }
        }
    }
}
