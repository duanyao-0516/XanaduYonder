using System;
using UnityEngine;

namespace XanaduYonder.Runtime.FlappyBird
{
    // Three original, short synthesized cues; no downloaded audio or per-game
    // settings. The host's sound switch is evaluated for every event.
    internal sealed class FlappyAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip flap, point, impact;
        internal void Initialize()
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.volume=.22f;
            flap=Make("Flappy wing",.075f,(t,p)=>Mathf.Sin(2*Mathf.PI*(420*t+1050*t*t))*.45f);
            point=Make("Flappy point",.16f,(t,p)=>(Mathf.Sin(2*Mathf.PI*880*t)+Mathf.Sin(2*Mathf.PI*1320*t)*.3f)*.36f);
            impact=Make("Flappy impact",.20f,(t,p)=>Mathf.Sin(2*Mathf.PI*(130*t-220*t*t))*.55f);
        }
        internal void Flap(bool enabled) { if(enabled) source.PlayOneShot(flap); }
        internal void Point(bool enabled) { if(enabled) source.PlayOneShot(point); }
        internal void Impact(bool enabled) { if(enabled) source.PlayOneShot(impact); }
        internal void Stop() { if(source!=null) source.Stop(); }
        private static AudioClip Make(string name,float duration,Func<float,float,float> sample)
        {
            const int rate=22050; var data=new float[Mathf.CeilToInt(rate*duration)];
            for(int i=0;i<data.Length;i++) { float p=i/(float)(data.Length-1); data[i]=sample(i/(float)rate,p)*Mathf.Sin(Mathf.PI*p)*Mathf.Exp(-p*1.2f); }
            var clip=AudioClip.Create(name,data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }
        private void OnDestroy() { Stop(); if(flap!=null) Destroy(flap); if(point!=null) Destroy(point); if(impact!=null) Destroy(impact); }
    }
}
