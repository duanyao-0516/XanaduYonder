using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // Split the original decorative HUD sheet at its empty inter-panel boundary.
    // Each half retains its authored scale and the original stage crop.
    internal sealed class AgeOfWarWideHud
    {
        private readonly RectTransform right;
        private readonly float split;
        internal AgeOfWarWideHud(AgeOfWarSceneView scene,AgeOfWarAssets assets)
        {
            string key=assets.Edition==1?"ui/one/51":"ui/hud/23";
            split=assets.Edition==1?410:230;
            for(int index=0;index<scene.Images.Count;index++)
            {
                if(scene.Definition.layers[index].key!=key)continue;
                int sibling=scene.Images[index].Root.GetSiblingIndex();scene.SuppressLayer(index);
                var left=Region(scene.Root,assets,key,0,split,assets.Stage.height);
                right=Region(scene.Root,assets,key,split,assets.Stage.width-split,assets.Stage.height);
                left.SetSiblingIndex(sibling);right.SetSiblingIndex(sibling+1);break;
            }
        }
        private static RectTransform Region(RectTransform parent,AgeOfWarAssets assets,string key,float start,float width,float height)
        {
            var root=AgeOfWarDrawing.Rect(parent,"Original HUD panel",start,0,width,height);
            root.gameObject.AddComponent<RectMask2D>();
            new AgeOfWarImage(root,assets,"Original panel pixels").Set(key,"idle",0,-start,0);
            return root;
        }
        internal void SetWidth(float extra){if(right!=null)right.anchoredPosition=new Vector2(split+extra,0);}
    }
}
