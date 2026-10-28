using UnityEngine;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed class SokobanFeedback : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip move,push,goal,complete;
        private void Awake()
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.volume=.15f;
            move=Resources.Load<AudioClip>("Sokoban/Audio/move"); push=Resources.Load<AudioClip>("Sokoban/Audio/push");
            goal=Resources.Load<AudioClip>("Sokoban/Audio/goal"); complete=Resources.Load<AudioClip>("Sokoban/Audio/complete");
        }
        internal void Play(SokobanMoveResult result,bool won,bool sound,bool haptics,GameFeedback shared)
        {
            if(!result.Changed) return;
            AudioClip clip=won?complete:result.ReachedGoal?goal:result.Pushed?push:move;
            if(sound&&clip!=null) source.PlayOneShot(clip,won?1:result.Pushed?.7f:.4f);
            if(result.Pushed) shared?.Pulse(haptics);
        }
        internal void Stop() { if(source!=null) source.Stop(); }
        private void OnDisable() { Stop(); }
    }
}
