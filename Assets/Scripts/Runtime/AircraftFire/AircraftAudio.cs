using System;
using UnityEngine;

namespace XanaduYonder.Runtime.AircraftFire
{
    internal sealed class AircraftAudio : MonoBehaviour
    {
        private AudioSource music, effects;
        private AudioClip enemy, boss, hero, arrival;
        private Func<bool> enabledSetting;
        private float nextEnemySound;
        internal void Initialize(Func<bool> setting)
        {
            enabledSetting = setting;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = true; music.volume = .28f;
            music.clip = Load("bg_music");
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false; effects.volume = .5f;
            enemy = Load("enemy_blowup"); boss = Load("boss_blowup");
            hero = Load("hero_blowup"); arrival = Load("boss_appear");
        }
        internal void Tick(bool running, bool preserveEffects = false)
        {
            if (enabledSetting == null || !enabledSetting()) { Stop(); return; }
            if (!running) { music.Stop(); if (!preserveEffects) effects.Stop(); return; }
            if (!music.isPlaying) music.Play();
        }
        internal void Enemy(bool large)
        {
            if (!large && Time.unscaledTime < nextEnemySound) return;
            nextEnemySound = Time.unscaledTime + .08f;
            Play(large ? boss : enemy);
        }
        internal void Hero() { Play(hero); }
        internal void BossAppeared() { Play(arrival); }
        private void Play(AudioClip clip) { if (enabledSetting != null && enabledSetting()) effects.PlayOneShot(clip); }
        internal void Stop() { if (music != null) music.Stop(); if (effects != null) effects.Stop(); }
        private void OnDisable() { Stop(); }
        private static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("AircraftFire/Audio/" + name);
            if (clip == null) throw new InvalidOperationException("Missing Aircraft Fire audio: " + name);
            return clip;
        }
    }
}
