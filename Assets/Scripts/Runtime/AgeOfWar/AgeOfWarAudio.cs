using System;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarAudio : MonoBehaviour
    {
        private AgeOfWarAssets assets;
        private Func<bool> enabledSetting;
        private AudioSource music,effects,openingStream;
        private bool suspended,musicRequested;
        internal float MusicVolume=1,SoundVolume=1;
        internal void Initialize(AgeOfWarAssets source,Func<bool> sound)
        {
            assets=source;enabledSetting=sound;music=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();openingStream=gameObject.AddComponent<AudioSource>();
            music.playOnAwake=effects.playOnAwake=openingStream.playOnAwake=false;music.loop=true;
        }
        internal void SelectMusic(string key)
        {
            var clip=string.IsNullOrEmpty(key)?null:assets.LoadSound(key);
            if(!string.IsNullOrEmpty(key)&&clip==null)throw new InvalidOperationException("Missing original music: "+key);
            if(music.clip==clip)return;music.Stop();music.clip=clip;
            musicRequested=clip!=null;if(musicRequested&&!suspended)music.Play();Refresh();
        }
        internal void Event(AgeOfWarEvent item,float cameraX)
        {
            if(suspended||enabledSetting==null||!enabledSetting()||string.IsNullOrEmpty(item.Sound))return;
            var clip=assets.LoadSound(item.Sound);if(clip==null)throw new InvalidOperationException("Missing original sound: "+item.Sound);
            float volume=assets.Edition==1?1:Mathf.Clamp01((360-Mathf.Abs((float)item.X+cameraX-360))/300);
            if(volume>0)effects.PlayOneShot(clip,volume*SoundVolume);
        }
        internal void StopMusic(){music.Stop();musicRequested=false;}
        internal void OpeningSound(string key)
        {
            var clip=assets.LoadSound(key);if(clip==null)throw new InvalidOperationException("Missing original opening sound: "+key);
            if(!suspended)effects.PlayOneShot(clip,SoundVolume);
        }
        internal void StartOpeningStream(string key)
        {
            StopOpeningStream();openingStream.clip=assets.LoadSound(key);
            if(openingStream.clip==null)throw new InvalidOperationException("Missing original opening stream: "+key);
            Refresh();if(!suspended)openingStream.Play();
        }
        internal void StopOpeningStream(){if(openingStream!=null){openingStream.Stop();openingStream.clip=null;}}
        internal void Refresh()
        {
            bool enabled=enabledSetting!=null&&enabledSetting();
            if(music!=null)music.volume=enabled?MusicVolume:0;
            if(effects!=null)effects.mute=!enabled;
            if(openingStream!=null)openingStream.volume=enabled?SoundVolume:0;
        }
        internal void Suspend(bool value)
        {
            if(suspended==value)return;suspended=value;
            if(value){music.Pause();effects.Stop();openingStream.Pause();}
            else
            {
                if(musicRequested&&music.clip!=null){music.UnPause();if(!music.isPlaying)music.Play();}
                if(openingStream.clip!=null)openingStream.UnPause();
            }
        }
        internal void Release(){if(music!=null){music.Stop();Destroy(music);}if(effects!=null){effects.Stop();Destroy(effects);}if(openingStream!=null){openingStream.Stop();Destroy(openingStream);}Destroy(this);}
    }
}
