using System;
using UnityEngine;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    internal sealed class ZumaAudio : MonoBehaviour
    {
        private AudioSource music,intro,effects;
        private ZumaAssets assets;
        private Func<bool> sound,haptics;
        private GameFeedback feedback;
        private string desiredMusic;
        private bool audible,suspended;
        private int outroPitch;
        public void Initialize(ZumaAssets resources,Func<bool> soundEnabled,Func<bool> hapticsEnabled,GameFeedback common)
        {
            assets=resources;sound=soundEnabled;haptics=hapticsEnabled;feedback=common;
            music=gameObject.AddComponent<AudioSource>();intro=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();
            music.playOnAwake=intro.playOnAwake=effects.playOnAwake=false;
            music.volume=intro.volume=.45f;effects.volume=.65f;
            music.spatialBlend=intro.spatialBlend=effects.spatialBlend=0;
            audible=sound();
        }
        public void SelectMusic(string id)
        {
            if(desiredMusic==id)return;
            desiredMusic=id;outroPitch=0;StopSources();
            if(audible&&!suspended)StartMusic();
        }
        private void Update()
        {
            if(sound==null)return;
            var enabled=sound();
            if(enabled==audible)return;
            audible=enabled;
            if(!enabled)StopSources();else if(!suspended)StartMusic();
        }
        private void StartMusic()
        {
            if(string.IsNullOrEmpty(desiredMusic)||assets==null)return;
            var entry=assets.Music(desiredMusic);
            music.clip=assets.LoadMusic(desiredMusic);music.loop=entry.loop;
            var lead=assets.LoadMusicIntro(desiredMusic);
            if(lead==null){music.Play();return;}
            intro.clip=lead;intro.loop=false;
            var start=AudioSettings.dspTime+.05;
            intro.PlayScheduled(start);
            music.PlayScheduled(start+(double)lead.samples/lead.frequency);
        }
        public void Suspend(bool value)
        {
            if(suspended==value)return;suspended=value;
            if(value)StopSources();else if(audible)StartMusic();
        }
        public void Button() { Play("button1"); }
        private void Play(string id,float pitch=1)
        {
            if(!audible||suspended)return;
            effects.pitch=pitch;effects.PlayOneShot(assets.LoadSound(id));
        }
        public void Handle(ZumaEvent ev)
        {
            switch(ev.Kind)
            {
                case "fire":Play("fireball1");if(haptics())feedback.Play(true,false,true);break;
                case "swapped":Play("ballclick2");break;
                case "insert":Play("ballclick1");break;
                case "explode":case "combo":
                    Play("ballsdestroyed"+Mathf.Clamp(ev.Combo+1,1,5),Mathf.Pow(2,Mathf.Min(12,ev.Combo*2)/12f));
                    if(haptics())feedback.Play(true,false,true);break;
                case "coinAppear":Play("jewelappear");break;
                case "coinDisappear":Play("gemvanishes");break;
                case "coinCollect":Play("coingrab");break;
                case "threshold":Play("chant14");break;
                case "introEnd":Play("frogland2");break;
                case "loss":SelectMusic("game_over");Play("warning1");break;
                case "win":SelectMusic("win");break;
                case "outroPop":Play("endoflevelpop1",Mathf.Pow(2,Mathf.Min(12,outroPitch++)/12f));break;
                case "result":Play("chant2");break;
            }
        }
        private void StopSources()
        { if(music!=null)music.Stop();if(intro!=null)intro.Stop();if(effects!=null)effects.Stop(); }
        public void Release()
        {
            StopSources();
            if(music!=null){music.clip=null;Destroy(music);}
            if(intro!=null){intro.clip=null;Destroy(intro);}
            if(effects!=null){effects.clip=null;Destroy(effects);}
            assets=null;desiredMusic=null;sound=null;
        }
        private void OnDestroy() { Release(); }
    }
}
