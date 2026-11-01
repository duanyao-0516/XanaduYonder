using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // Split the original decorative HUD sheet at its empty inter-panel boundary.
    // Each region retains its authored scale and the original stage crop.
    internal sealed class AgeOfWarWideHud
    {
        private readonly RectTransform right,rightBottom;
        private readonly float split;
        internal AgeOfWarWideHud(AgeOfWarSceneView scene,AgeOfWarAssets assets)
        {
            string key=assets.Edition==1?"ui/one/51":"ui/hud/23";
            split=assets.Edition==1?410:230;
            for(int index=0;index<scene.Images.Count;index++)
            {
                if(scene.Definition.layers[index].key!=key)continue;
                int sibling=scene.Images[index].Root.GetSiblingIndex();scene.SuppressLayer(index);
                var left=Region(scene.Root,assets,key,0,0,split,assets.Stage.height);
                float topHeight=assets.Edition==2?75:assets.Stage.height;
                right=Region(scene.Root,assets,key,split,0,assets.Stage.width-split,topHeight);
                left.SetSiblingIndex(sibling);right.SetSiblingIndex(sibling+1);
                if(assets.Edition==2)
                {
                    // The same source sheet also contains bottom HP framing, which stays in place.
                    rightBottom=Region(scene.Root,assets,key,split,topHeight,assets.Stage.width-split,assets.Stage.height-topHeight);
                    rightBottom.SetSiblingIndex(sibling+2);
                }
                break;
            }
        }
        private static RectTransform Region(RectTransform parent,AgeOfWarAssets assets,string key,float start,float top,float width,float height)
        {
            var root=AgeOfWarDrawing.Rect(parent,"Original HUD panel",start,top,width,height);
            root.gameObject.AddComponent<RectMask2D>();
            new AgeOfWarImage(root,assets,"Original panel pixels").Set(key,"idle",0,-start,-top);
            return root;
        }
        internal void SetWidth(float extra,float rightInset=0)
        {
            if(right!=null)right.anchoredPosition=new Vector2(split+extra-rightInset,0);
            if(rightBottom!=null)rightBottom.anchoredPosition=new Vector2(split+extra,rightBottom.anchoredPosition.y);
        }
    }
}
