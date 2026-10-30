using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal static class AgeOfWarDrawing
    {
        internal static RectTransform Rect(Transform parent,string name,float x=0,float y=0,float width=0,float height=0)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        internal static void Clear(Transform root)
        {for(int i=root.childCount-1;i>=0;i--){var child=root.GetChild(i);child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}}
        internal static Text Text(Transform root,AgeOfWarAssets assets,AgeOfWarTextField field)
        {
            var rect=Rect(root,field.name.Length>0?field.name:field.variable,field.x+2,field.y+2,Mathf.Max(1,field.w-4),Mathf.Max(1,field.h-4));
            var text=rect.gameObject.AddComponent<Text>();text.font=assets.LoadFont(field.font);
            if(text.font==null)throw new InvalidOperationException("Missing original text font: "+field.font);
            text.fontStyle=field.bold?(field.italic?FontStyle.BoldAndItalic:FontStyle.Bold):(field.italic?FontStyle.Italic:FontStyle.Normal);
            text.fontSize=Mathf.RoundToInt(field.size);text.supportRichText=false;text.text=AgeOfWarTextEffects.PlainText(field.initial);
            Color color;text.color=ColorUtility.TryParseHtmlString(field.color,out color)?color:Color.white;
            text.alignment=field.align==1?TextAnchor.UpperRight:field.align==2?TextAnchor.UpperCenter:TextAnchor.UpperLeft;
            text.horizontalOverflow=field.wordWrap?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
            // Flash useOutlines selects embedded glyphs; it does not request an outline effect.
            AgeOfWarTextLayout.Initialize(text,assets,field);
            AgeOfWarTextEffects.Initialize(text,field);
            return text;
        }
    }
    internal sealed class AgeOfWarImage
    {
        internal readonly RectTransform Root;
        internal readonly Image Image;
        private readonly AgeOfWarAssets assets;
        private int frameTick;
        internal string Key,Action;
        internal AgeOfWarImage(Transform parent,AgeOfWarAssets source,string name)
        {
            assets=source;Root=AgeOfWarDrawing.Rect(parent,name);
            Image=AgeOfWarDrawing.Rect(Root,"Original pixels").gameObject.AddComponent<Image>();Image.raycastTarget=false;
            Root.gameObject.AddComponent<AgeOfWarImageBinding>().Owner=this;
        }
        internal void Set(string key,string action,double frame,double x,double y,double scaleX=1,double scaleY=1,double rotation=0,double alpha=1)
        {
            Key=key;Action=action;frameTick=(int)frame;Rebind();Image.color=new Color(1,1,1,(float)alpha);
            Root.anchoredPosition=new Vector2((float)x,-(float)y);Root.localScale=new Vector3((float)scaleX,(float)scaleY,1);
            Root.localRotation=Quaternion.Euler(0,0,-(float)rotation);
        }
        internal void Rebind()
        {
            if(string.IsNullOrEmpty(Key))return;
            AgeOfWarSpriteFrame sprite;
            if(!assets.TryGetFrame(Key,Action,frameTick,out sprite))throw new InvalidOperationException("Missing original animation: "+Key+" / "+Action);
            Image.sprite=sprite.Sprite;Image.enabled=!sprite.Empty;
            Image.rectTransform.anchoredPosition=new Vector2(-sprite.Origin.x,sprite.Origin.y);
            Image.rectTransform.sizeDelta=sprite.Size;
        }
    }
    internal sealed class AgeOfWarSceneView
    {
        internal readonly RectTransform Root;
        internal readonly AgeOfWarScene Definition;
        internal readonly List<AgeOfWarImage> Images=new List<AgeOfWarImage>();
        internal readonly List<Text> Texts=new List<Text>();
        private readonly AgeOfWarAssets assets;
        private readonly HashSet<int> suppressedLayers=new HashSet<int>(),suppressedIds=new HashSet<int>();
        private readonly HashSet<string> suppressedActions=new HashSet<string>();
        private readonly Dictionary<int,Vector2> layerOffsets=new Dictionary<int,Vector2>(),textOffsets=new Dictionary<int,Vector2>();
        private sealed class MobileButton {internal AgeOfWarHotspot Hit;internal Text Text;internal Color Color;}
        private readonly List<MobileButton> mobileButtons=new List<MobileButton>();
        internal AgeOfWarSceneView(Transform parent,AgeOfWarAssets source,string key,float x=0,float y=0)
        {
            assets=source;Definition=source.GetScene(key);
            if(Definition==null)throw new InvalidOperationException("Missing original scene: "+key);
            Root=AgeOfWarDrawing.Rect(parent,key,x,y,Definition.width,Definition.height);
            foreach(var layer in Definition.layers)
            {
                var image=new AgeOfWarImage(Root,source,layer.name.Length>0?layer.name:layer.key);
                image.Set(layer.key,layer.action,layer.frame,layer.x,layer.y,layer.scaleX,layer.scaleY,layer.rotation,layer.alpha);Images.Add(image);
            }
            foreach(var field in Definition.texts??new AgeOfWarTextField[0])
            {
                for(int i=0;i<Images.Count;i++)if(Definition.layers[i].key.EndsWith("/"+field.characterId))Images[i].Root.gameObject.SetActive(false);
                Texts.Add(AgeOfWarDrawing.Text(Root,source,field));
            }
            if(assets.Edition==1)AgeOfWarOneMobileUi.Apply(this,assets);else AgeOfWarTwoMobileUi.Apply(this,assets);
        }
        internal void SuppressLayer(int index)
        {if(index<0||index>=Images.Count)return;suppressedLayers.Add(index);Images[index].Root.gameObject.SetActive(false);}
        internal void SuppressHotspot(int id){suppressedIds.Add(id);}
        internal void SuppressHotspot(string action){suppressedActions.Add(action);}
        internal void AddTextButton(string action,string label,string fontKey,float fontSize,Rect rect,Color color,bool bold=false)
        {
            var field=new AgeOfWarTextField{name=string.IsNullOrEmpty(action)?label:action,variable="",initial=label,font=fontKey,size=fontSize,
                x=rect.x,y=rect.y,w=rect.width,h=rect.height,color="#"+ColorUtility.ToHtmlStringRGBA(color),align=2,bold=bold,wordWrap=true,multiline=true};
            var text=AgeOfWarDrawing.Text(Root,assets,field);
            var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.75f);shadow.effectDistance=new Vector2(1,-1);
            mobileButtons.Add(new MobileButton{Hit=new AgeOfWarHotspot{action=action??"",name=action??"",id=-100-mobileButtons.Count,x=rect.x,y=rect.y,w=rect.width,h=rect.height},Text=text,Color=color});
        }
        internal void SetLayerOffset(int index,Vector2 offset)
        {
            if(index<0||index>=Images.Count)return;Vector2 previous;layerOffsets.TryGetValue(index,out previous);
            Images[index].Root.anchoredPosition+=offset-previous;layerOffsets[index]=offset;
        }
        internal void SetTextOffset(int index,Vector2 offset)
        {
            if(index<0||index>=Texts.Count)return;Vector2 previous;textOffsets.TryGetValue(index,out previous);
            Texts[index].rectTransform.anchoredPosition+=offset-previous;textOffsets[index]=offset;
        }
        private Vector2 LayerOffset(int index){Vector2 offset;layerOffsets.TryGetValue(index,out offset);return offset;}
        internal void Layer(int index,bool visible,int tick=-1,float alpha=-1)
        {
            var image=Images[index];visible=visible&&!suppressedLayers.Contains(index);image.Root.gameObject.SetActive(visible);if(!visible)return;
            var l=Definition.layers[index];var offset=LayerOffset(index);image.Set(l.key,l.action,tick<0?l.frame:tick,l.x+offset.x,l.y-offset.y,l.scaleX,l.scaleY,l.rotation,alpha<0?l.alpha:alpha);
        }
        internal void Named(string name,bool visible,int tick=-1)
        {for(int i=0;i<Images.Count;i++)if(Definition.layers[i].name==name)Layer(i,visible,tick);}
        internal void Value(string name,string value)
        {for(int i=0;i<Texts.Count;i++){var f=Definition.texts[i];if(f.name==name||f.variable==name)Texts[i].text=value;}}
        internal AgeOfWarHotspot Hit(Vector2 point)
        {
            var offset=Root.anchoredPosition;point-=new Vector2(offset.x,-offset.y);
            for(int i=mobileButtons.Count-1;i>=0;i--)
            {var h=mobileButtons[i].Hit;if(h.action.Length>0&&new Rect(h.x,h.y,h.w,h.h).Contains(point))return h;}
            var hotspots=Definition.hotspots;
            for(int i=hotspots.Length-1;i>=0;i--)
            {
                var h=hotspots[i];if(suppressedIds.Contains(h.id)||suppressedActions.Contains(h.action))continue;bool visible=true;Vector2 layerOffset=Vector2.zero;
                for(int j=0;j<Images.Count;j++)if((h.name.Length>0&&Definition.layers[j].name==h.name)||Definition.layers[j].key.EndsWith("/"+h.id))
                {visible=Images[j].Root.gameObject.activeSelf;layerOffset=LayerOffset(j);break;}
                if(visible&&new Rect(h.x+layerOffset.x,h.y-layerOffset.y,h.w,h.h).Contains(point))return h;
            }
            return null;
        }
        internal void Hover(AgeOfWarHotspot hit,bool down)
        {
            foreach(var button in mobileButtons)
            {var c=button.Color;if(hit==button.Hit)c=down?c*.75f:Color.Lerp(c,Color.yellow,.3f);button.Text.color=c;}
            for(int i=0;i<Images.Count;i++)
            {
                var l=Definition.layers[i];if(!Images[i].Root.gameObject.activeSelf)continue;
                bool isButton=false;foreach(var h in Definition.hotspots)if((h.name.Length>0&&h.name==l.name)||l.key.EndsWith("/"+h.id)){isButton=true;break;}
                if(!isButton)continue;
                bool selected=hit!=null&&(l.key.EndsWith("/"+hit.id));
                string action=selected?(down?"down":"over"):"out";var offset=LayerOffset(i);
                if(assets.GetClip(l.key,action)!=null)Images[i].Set(l.key,action,0,l.x+offset.x,l.y-offset.y,l.scaleX,l.scaleY,l.rotation,l.alpha);
                else if(assets.GetClip(l.key,"idle")!=null&&assets.GetClip(l.key,"over")!=null)Images[i].Set(l.key,"idle",0,l.x+offset.x,l.y-offset.y,l.scaleX,l.scaleY,l.rotation,l.alpha);
            }
        }
        internal void Animate(int tick)
        {
            for(int i=0;i<Images.Count;i++)
            {
                var layer=Definition.layers[i];var clip=assets.GetClip(layer.key,layer.action);
                if(clip!=null&&clip.loop&&clip.DurationTicks>1&&Images[i].Root.gameObject.activeSelf)Layer(i,true,tick);
            }
        }
        internal void Dispose(){Root.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);}
    }
}
