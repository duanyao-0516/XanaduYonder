using System;
using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>User-requested mobile navigation: no sponsorship links or exclusive difficulty.</summary>
    internal static class AgeOfWarTwoMobileUi
    {
        private static readonly HashSet<string> RemovedActions=new HashSet<string>(StringComparer.Ordinal)
        {
            "btnPlayMoreGames","btnMaxGames","btnLouissi","btnLouissi2","btnWaterflame",
            "btnAdd","btnFirstGame","btnGuide","btnHowToPlay","btnLink",
            "btnInsane","btnInsane2"
        };

        internal static bool IsRemovedAction(string action)
        {
            return action!=null&&RemovedActions.Contains(action);
        }

        internal static void Apply(AgeOfWarSceneView scene,AgeOfWarAssets assets)
        {
            if(scene==null||assets==null||assets.Edition!=2)return;
            var definition=scene.Definition;
            foreach(var action in RemovedActions)scene.SuppressHotspot(action);
            for(int index=0;index<definition.layers.Length;index++)
            {
                var layer=definition.layers[index];
                bool removed=IsRemovedAction(layer.name);
                if(!removed)
                {
                    foreach(var hotspot in definition.hotspots)
                    {
                        if(!IsRemovedAction(hotspot.action))continue;
                        if((!string.IsNullOrEmpty(hotspot.name)&&hotspot.name==layer.name)||
                            layer.key.EndsWith("/"+hotspot.id,StringComparison.Ordinal))
                        {removed=true;break;}
                    }
                }
                if(removed)scene.SuppressLayer(index);
            }
            if(definition.key=="menu")
            {
                // Reuse the removed Play More Games row and a complete original embedded font.
                // The original game UI font keeps the new label independent of baked menu lettering.
                scene.AddTextButton("mobile-exit","Exit","font/windows/2",24,
                    new Rect(221,295.35f,278,39),Color.white,true);
            }
        }
    }
}
