using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>The source menu hover tweens change exposure only; no scale tween runs on these screens.</summary>
    internal sealed class AgeOfWarTwoMenuEffects : IDisposable
    {
        private sealed class Entry
        {
            internal string Name;
            internal AgeOfWarExposureImage Effect;
            internal readonly AgeOfWarTwoExposure Tween=new AgeOfWarTwoExposure();
        }
        private readonly List<Entry> entries=new List<Entry>();
        private readonly double outDuration;
        internal AgeOfWarTwoMenuEffects(AgeOfWarSceneView scene)
        {
            if(scene==null)throw new ArgumentNullException("scene");
            string key=scene.Definition.key;string[] names;
            switch(key)
            {
                case "menu":names=new[]{"btnStart","btnAdd","btnCredits","btnPlayMoreGames","btnMaxGames","btnHowToPlay"};break;
                case "difficulty":names=new[]{"btnReturn","btnEasy","btnMedium","btnHard","btnInsane","btnInsane2","btnMaxGames"};break;
                case "credits":case "victory":names=new[]{"btnReturn","btnMaxGames"};break;
                case "defeat":names=new[]{"btnReturn","btnMaxGames","btnPlayMoreGames"};break;
                default:names=new string[0];break;
            }
            outDuration=key=="victory"||key=="defeat"?.2:.5;
            foreach(string name in names)
            {
                for(int index=0;index<scene.Images.Count;index++)
                {
                    if(scene.Definition.layers[index].name!=name)continue;
                    var effect=scene.Images[index].Image.gameObject.AddComponent<AgeOfWarExposureImage>();
                    entries.Add(new Entry{Name=name,Effect=effect});break;
                }
            }
        }
        internal void Hover(AgeOfWarHotspot hit)
        {
            double now=Time.realtimeSinceStartupAsDouble;
            foreach(var entry in entries)entry.Tween.Hover(hit!=null&&(hit.name==entry.Name||hit.action==entry.Name),now,outDuration);
            Render();
        }
        internal void Render()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            foreach(var entry in entries)if(entry.Effect!=null)entry.Effect.SetExposure((float)entry.Tween.Sample(now));
        }
        public void Dispose()
        {
            foreach(var entry in entries)if(entry.Effect!=null){entry.Effect.enabled=false;UnityEngine.Object.Destroy(entry.Effect);}
            entries.Clear();
        }
    }

    // Each UI instance (including a transition snapshot clone) owns its material.
    [DisallowMultipleComponent]
    internal sealed class AgeOfWarExposureImage : MonoBehaviour,IMaterialModifier
    {
        [SerializeField] private float exposure;
        private Material material;
        private Graphic image;
        private Graphic Image {get {if(image==null)image=GetComponent<Graphic>();return image;}}
        internal void SetExposure(float value)
        {if(exposure==value)return;exposure=value;if(material!=null)material.SetFloat("_Exposure",exposure);if(Image!=null)Image.SetMaterialDirty();}
        public Material GetModifiedMaterial(Material original)
        {
            if(!isActiveAndEnabled)return original;
            if(material==null)
            {
                var shader=Resources.Load<Shader>("AgeOfWar/Exposure");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original menu exposure shader is unavailable.");
                material=new Material(shader){name="Original menu exposure",hideFlags=HideFlags.HideAndDontSave};
            }
            material.CopyPropertiesFromMaterial(original);material.SetFloat("_Exposure",exposure);return material;
        }
        private void OnEnable(){if(Image!=null)Image.SetMaterialDirty();}
        private void OnDisable(){if(Image!=null)Image.SetMaterialDirty();}
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
